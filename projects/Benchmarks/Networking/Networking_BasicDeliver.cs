using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;

namespace Benchmarks.Networking
{
    [MemoryDiagnoser]
    public class Networking_BasicDeliver
    {
        private const int messageCount = 10000;

        private IDisposable _container;
        private static byte[] _body = Encoding.UTF8.GetBytes("hello world");

        [GlobalSetup]
        public void GlobalSetup()
        {
            _container = RabbitMQBroker.Start(); 
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _container.Dispose();
        }

        [Benchmark(Baseline = true)]
        public async Task Publish_Hello_World()
        {
            var cf = new ConnectionFactory { ConsumerDispatchConcurrency = 2 };
            using (var connection = cf.CreateConnection())
            {
                await Publish_Hello_World(connection);
            }
        }

        public static async Task Publish_Hello_World(IConnection connection)
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
                    model.BasicPublish("", queue.QueueName, null, _body);
                }

                await tcs.Task;
            }
        }
    }
}
