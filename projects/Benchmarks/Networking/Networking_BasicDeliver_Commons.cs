using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Benchmarks.Networking
{
    [MemoryDiagnoser]
    public class Networking_BasicDeliver_Commons
    {
        private const int messageCount = 10000;


        public static async Task Publish_Hello_World(IConnection connection, uint n, byte[] body)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (var model = connection.CreateModel())
            {
                var queue = model.QueueDeclare();
                var consumed = 0;
                var consumer = new EventingBasicConsumer(model);
                consumer.Received += (s, args) =>
                {
                    if (Interlocked.Increment(ref consumed) == messageCount)
                    {
                        tcs.SetResult(true);
                    }
                };
                model.BasicConsume(queue.QueueName, true, consumer);

                for (int i = 0; i < messageCount; i++)
                {
                    model.BasicPublish("", queue.QueueName, null, body);
                }

                await tcs.Task;
            }
        }
    }
}
