using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace DeadlockRabbitMQ
{
    class Program
    {
        private static int messagesSent = 0;
        private static int messagesReceived = 0;
        private static int batchesToSend = 10;
        private static int itemsPerBatch = 500;
        static async Task Main(string[] args)
        {
            //ThreadPool.SetMinThreads(16 * Environment.ProcessorCount, 16 * Environment.ProcessorCount);
            var connectionString = new Uri("amqp://guest:guest@localhost/");

            var connectionFactory = new ConnectionFactory() { DispatchConsumersAsync = true, Uri = connectionString };
            var connection = connectionFactory.CreateConnection();
            var connection2 = connectionFactory.CreateConnection();
            var publisher = connection.CreateModel();
            var subscriber = connection2.CreateModel();
            publisher.ConfirmSelect();
            subscriber.ConfirmSelect();

            publisher.ExchangeDeclare("test", ExchangeType.Topic, true);

            subscriber.QueueDeclare("testqueue", true, false, true);
            var asyncListener = new AsyncEventingBasicConsumer(subscriber) { ConsumerTag = "testconsumer" };
            asyncListener.Received += AsyncListener_Received;
            subscriber.QueueBind("testqueue", "test", "myawesome.routing.key");
            subscriber.BasicConsume("testqueue", false, asyncListener.ConsumerTag, asyncListener);

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
                        batch.Add("test", "myawesome.routing.key", true, properties, BitConverter.GetBytes(i + messagesSent));
                    }
                    batch.Publish();
                    publisher.WaitForConfirmsOrDie();
                    messagesSent += itemsPerBatch;
                }
            });

            while (messagesReceived < batchesToSend * itemsPerBatch)
            {
                Console.WriteLine($"Messages received: {messagesReceived}");

                await Task.Delay(500);
            }

            await batchPublish;

            Console.WriteLine("Done receiving all messages.");
            Console.ReadLine();
            subscriber.Dispose();
            publisher.Dispose();
            connection.Dispose();
            connection2.Dispose();
        }

        private static async Task AsyncListener_Received(object sender, BasicDeliverEventArgs @event)
        {
            // Doing things in parallel here is what will eventually trigger the deadlock,
            // probably due to a race condition in AsyncConsumerWorkService.Loop, although
            // I've had trouble pinpointing it exactly, but due to how the code in there uses
            // a TaskCompletionSource, and elsewhere overrides it, it might cause Enqueue and Loop
            // to eventually be working with different references, or that's at least the current theory.
            // Moving to better synchronization constructs solves the issue, and using the ThreadPool
            // is standard practice as well to maximize core utilization and reduce overhead of Thread creation
            await Task.WhenAll(IncrementCounter().AsTask(), WriteToConsole(@event.BasicProperties.CorrelationId, @event.Body).AsTask());
            (sender as AsyncEventingBasicConsumer).Model.BasicAck(@event.DeliveryTag, false);
        }

        private static ValueTask IncrementCounter()
        {
            Interlocked.Increment(ref messagesReceived);
            return new ValueTask();
        }

        private static ValueTask WriteToConsole(string correlationId, byte[] body)
        {
            //Console.WriteLine($"Received event {correlationId} with value: {BitConverter.ToInt32(body, 0)}");
            return new ValueTask();
        }
    }
}
