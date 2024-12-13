using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using RabbitMQ.Client;

namespace Benchmarks.Networking
{
    [MemoryDiagnoser]
    public class Networking_BasicDeliver_LongLivedConnection
    {
        private IConnection _connection;

        private const int messageCount = 10000;
        private static byte[] _body = Encoding.UTF8.GetBytes("hello world");

        [GlobalSetup]
        public void GlobalSetup()
        {
            var cf = new ConnectionFactory { ConsumerDispatchConcurrency = 2 };
            // NOTE: https://github.com/dotnet/BenchmarkDotNet/issues/1738
            _connection = EnsureCompleted(cf.CreateConnectionAsync());
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _connection.Dispose();
        }

        [Benchmark(Baseline = true)]
        public Task Publish_Hello_World()
        {
            return Networking_BasicDeliver_Commons.Publish_Hello_World(_connection, messageCount, _body);
        }

        private static T EnsureCompleted<T>(Task<T> task) => task.GetAwaiter().GetResult();
    }
}
