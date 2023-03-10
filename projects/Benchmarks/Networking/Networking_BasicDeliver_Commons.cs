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
        public static async Task Publish_Hello_World(IConnection connection, uint messageCount, byte[] body)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (var channel = connection.CreateModel())
            {
                var queue = channel.QueueDeclare();
                var consumed = 0;
                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += (s, args) =>
                {
                    if (Interlocked.Increment(ref consumed) == messageCount)
                    {
                        tcs.SetResult(true);
                    }
                };
                channel.BasicConsume(queue.QueueName, true, consumer);

                for (int i = 0; i < messageCount; i++)
                {
                    channel.BasicPublish("", queue.QueueName, body);
                }

                await tcs.Task;
            }
        }
    }
}
