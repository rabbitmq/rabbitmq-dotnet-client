using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MassPublish
{
    public static class Program
    {
        private const int BatchesToSend = 100;
        private const int ItemsPerBatch = 500;

        private static int messagesSent;
        private static int messagesReceived;
        private static AutoResetEvent doneEvent;

        public static void Main()
        {
            ThreadPool.SetMinThreads(16 * Environment.ProcessorCount, 16 * Environment.ProcessorCount);

            doneEvent = new AutoResetEvent(false);

            var connectionFactory = new ConnectionFactory { DispatchConsumersAsync = true };
            var connection = connectionFactory.CreateConnection();
            var publisher = connection.CreateModel();
            var subscriber = connection.CreateModel();

            publisher.ConfirmSelect();
            publisher.ExchangeDeclare("test", ExchangeType.Topic, true, false);

            subscriber.QueueDeclare("testqueue", false, false, true);
            var asyncListener = new AsyncEventingBasicConsumer(subscriber);
            asyncListener.Received += AsyncListener_Received;
            subscriber.QueueBind("testqueue", "test", "myawesome.routing.key");
            subscriber.BasicConsume("testqueue", true, "testconsumer", asyncListener);

            byte[] payload = new byte[512];
            var watch = Stopwatch.StartNew();
            _ = Task.Run(async () =>
            {
                while (messagesSent < BatchesToSend * ItemsPerBatch)
                {
                    for (int i = 0; i < ItemsPerBatch; i++)
                    {
                        var properties = new BasicProperties
                        {
                            AppId = "testapp",
                        };
                        publisher.BasicPublish("test", "myawesome.routing.key", properties, payload);
                    }
                    messagesSent += ItemsPerBatch;
                    await publisher.WaitForConfirmsOrDieAsync().ConfigureAwait(false);
                }
            });

            Console.WriteLine($"Sending {BatchesToSend} batches for {ItemsPerBatch} items per batch. => Total messages: {BatchesToSend * ItemsPerBatch}");
            Console.WriteLine();
            Console.WriteLine(" Sent | Received");
            while (!doneEvent.WaitOne(500))
            {
                Console.WriteLine($"{messagesSent,5} | {messagesReceived,5}");
            }
            watch.Stop();
            Console.WriteLine($"{messagesSent,5} | {messagesReceived,5}");
            Console.WriteLine();
            Console.WriteLine($"Took {watch.Elapsed.TotalMilliseconds} ms");

            publisher.Dispose();
            subscriber.Dispose();
            connection.Dispose();
            Console.ReadLine();
        }

        private static Task AsyncListener_Received(object sender, BasicDeliverEventArgs @event)
        {
            if (Interlocked.Increment(ref messagesReceived) == BatchesToSend * ItemsPerBatch)
            {
                doneEvent.Set();
            }
            return Task.CompletedTask;
        }
    }
}
