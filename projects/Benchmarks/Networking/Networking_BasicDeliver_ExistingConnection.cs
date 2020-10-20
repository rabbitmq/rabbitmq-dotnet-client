using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using RabbitMQ.Client;
using System;

namespace Benchmarks.Networking
{
    [MemoryDiagnoser]
    public class Networking_BasicDeliver_ExistingConnection
    {
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
            _connection.Dispose();
            _container.Dispose();
        }

        [Benchmark(Baseline = true)]
        public async Task Publish_Hello_World()
        {
            await Networking_BasicDeliver.Publish_Hello_World(_connection);
        }
    }
}
