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
        private IConnection _defaultconnection;
        private IConnection _inlineconnection;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _container = RabbitMQBroker.Start();

            var cf = new ConnectionFactory { ConsumerDispatchConcurrency = 2 };
            _defaultconnection = cf.CreateConnection();
            cf.DispatchConsumerInline = true;
            _inlineconnection = cf.CreateConnection();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _inlineconnection.Dispose();
            _defaultconnection.Dispose();
            _container.Dispose();
        }

        [Benchmark(Baseline = true)]
        public async Task Publish_Hello_World()
        {
            await Networking_BasicDeliver.Publish_Hello_World(_defaultconnection);
        }

        [Benchmark()]
        public async Task Publish_Hello_World_Inline()
        {
            await Networking_BasicDeliver.Publish_Hello_World(_inlineconnection);
        }
    }
}
