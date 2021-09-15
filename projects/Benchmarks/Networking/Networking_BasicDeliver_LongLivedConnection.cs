using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using RabbitMQ.Client;
using System;
using System.Text;

namespace Benchmarks.Networking
{
    [MemoryDiagnoser]
    public class Networking_BasicDeliver_LongLivedConnection
    {
        private IDisposable _container;
        private IConnection _connection;

        private const int messageCount = 10000;
        private static byte[] _body = Encoding.UTF8.GetBytes("hello world");

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
        public Task Publish_Hello_World()
        {
            return Networking_BasicDeliver_Commons.Publish_Hello_World(_connection, messageCount, _body);
        }
    }
}
