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
        private const int BatchesToSend = 64;
        private const int ItemsPerBatch = 8192;

        private static int messagesSent;
        private static int messagesReceived;
        private static AutoResetEvent doneEvent;

        public static void Main()
        {
            var r = new Random();
            byte[] payload = new byte[4096];
            r.NextBytes(payload);

            ThreadPool.SetMinThreads(16 * Environment.ProcessorCount, 16 * Environment.ProcessorCount);

            doneEvent = new AutoResetEvent(false);

            var connectionFactory = new ConnectionFactory { DispatchConsumersAsync = true };
            var connection = connectionFactory.CreateConnection();
            var publisher = connection.CreateChannel();
            var subscriber = connection.CreateChannel();

            publisher.ConfirmSelect();
            publisher.ExchangeDeclare("test", ExchangeType.Topic, true, false);

            subscriber.QueueDeclare("testqueue", false, false, true);
            var asyncListener = new AsyncEventingBasicConsumer(subscriber);
            asyncListener.Received += AsyncListener_Received;
            subscriber.QueueBind("testqueue", "test", "myawesome.routing.key");
            subscriber.BasicConsume("testqueue", true, "testconsumer", asyncListener);

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
