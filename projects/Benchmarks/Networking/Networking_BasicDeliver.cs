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
        private int messageCount = 10000;

        private IDisposable container;

        [GlobalSetup]
        public void GlobalSetup()
        {
            container = RabbitMqBroker.Start(); 
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            container.Dispose();
        }


        [Benchmark(Baseline = true)]
        public async Task Publish_Hello_World()
        {
            var cf = new ConnectionFactory { ConsumerDispatchConcurrency = 2 };
            using (var connection = cf.CreateConnection())
            using (var model = connection.CreateModel())
            {
                var queue = model.QueueDeclare();
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
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

                const string publish1 = "hello world";
                byte[] body = Encoding.UTF8.GetBytes(publish1);
                var bp = model.CreateBasicProperties();
                for (int i = 0; i < messageCount; i++)
                {
                    model.BasicPublish("", queue.QueueName, bp, body);
                }

                await tcs.Task;
            }
        }
    }
}
