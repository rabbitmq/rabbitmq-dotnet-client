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
    public class Networking_BasicDeliver_ExistingConnection
    {
        private int messageCount = 10000;

        private IDisposable _container;
        private IConnection _connection;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _container = RabbitMQBroker.Start();

            var cf = new ConnectionFactory { ConsumerDispatchConcurrency = 2 };
            _connection = cf.CreateConnection();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _container.Dispose();
            _connection.Dispose();
        }


        [Benchmark(Baseline = true)]
        public async Task Publish_Hello_World()
        {
            await Networking_BasicDeliver.Publish_Hello_World(_connection);
        }
    }
}
