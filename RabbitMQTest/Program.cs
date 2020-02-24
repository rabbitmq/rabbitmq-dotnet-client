using System;
using System.Threading;
using System.Threading.Tasks;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DeadlockRabbitMQ
{
    class Program
    {
        private static int messagesSent = 0;
        private static int messagesReceived = 0;
        private static int batchesToSend = 100;
        private static int itemsPerBatch = 500;
        static async Task Main(string[] args)
        {
            Console.ReadLine();
            var connectionString = new Uri("amqp://guest:guest@localhost/");

            var connectionFactory = new ConnectionFactory() { DispatchConsumersAsync = true, Uri = connectionString };
            var connection = connectionFactory.CreateConnection();
            var connection2 = connectionFactory.CreateConnection();
            var publisher = connection.CreateModel();
            var subscriber = connection2.CreateModel();
            publisher.ConfirmSelect();
            //subscriber.ConfirmSelect();

            publisher.ExchangeDeclare("test", ExchangeType.Topic, true);

            subscriber.QueueDeclare("testqueue", true, false, true);
            var asyncListener = new AsyncEventingBasicConsumer(subscriber);
            asyncListener.Received += AsyncListener_Received;
            subscriber.QueueBind("testqueue", "test", "myawesome.routing.key");
            subscriber.BasicConsume("testqueue", false, "testconsumer", asyncListener);

            byte[] payload = new byte[16384];
            var batchPublish = Task.Run(() =>
            {
                while (messagesSent < batchesToSend * itemsPerBatch)
                {
                    var batch = publisher.CreateBasicPublishBatch();
                    for (int i = 0; i < itemsPerBatch; i++)
                    {
                        var properties = publisher.CreateBasicProperties();
                        properties.AppId = "testapp";
                        properties.CorrelationId = Guid.NewGuid().ToString();
                        batch.Add("test", "myawesome.routing.key", false, properties, payload);
                    }
                    batch.Publish();
                    messagesSent += itemsPerBatch;
                    publisher.WaitForConfirmsOrDie();
                }
            });

            var sentTask = Task.Run(async () =>
            {
                while (messagesSent < batchesToSend * itemsPerBatch)
                {
                    Console.WriteLine($"Messages sent: {messagesSent}");

                    await Task.Delay(500);
                }

                Console.WriteLine("Done sending messages!");
            });

            var receivedTask = Task.Run(async () =>
            {
                while (messagesReceived < batchesToSend * itemsPerBatch)
                {
                    Console.WriteLine($"Messages received: {messagesReceived}");

                    await Task.Delay(500);
                }

                Console.WriteLine("Done receiving all messages.");
            });

            await Task.WhenAll(sentTask, receivedTask);
            Console.ReadLine();
        }

        private static ValueTask AsyncListener_Received(object sender, BasicDeliverEventArgs @event)
        {
            // Doing things in parallel here is what will eventually trigger the deadlock,
            // probably due to a race condition in AsyncConsumerWorkService.Loop, although
            // I've had trouble pinpointing it exactly, but due to how the code in there uses
            // a TaskCompletionSource, and elsewhere overrides it, it might cause Enqueue and Loop
            // to eventually be working with different references, or that's at least the current theory.
            // Moving to better synchronization constructs solves the issue, and using the ThreadPool
            // is standard practice as well to maximize core utilization and reduce overhead of Thread creation
            Interlocked.Increment(ref messagesReceived);
            (sender as AsyncDefaultBasicConsumer).Model.BasicAck(@event.DeliveryTag, true);
            return default;
        }
    }
}
