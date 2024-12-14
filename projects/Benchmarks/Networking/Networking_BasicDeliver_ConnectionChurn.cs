using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using RabbitMQ.Client;

namespace Benchmarks.Networking
{
    [MemoryDiagnoser]
    public class Networking_BasicDeliver
    {
        private const int messageCount = 10000;

        private static byte[] _body = Encoding.UTF8.GetBytes("hello world");

        [Benchmark(Baseline = true)]
        public async Task Publish_Hello_World()
        {
            var cf = new ConnectionFactory { ConsumerDispatchConcurrency = 2 };
            using (IConnection connection = await cf.CreateConnectionAsync())
            {
                await Publish_Hello_World(connection);
            }
        }

        public static async Task Publish_Hello_World(IConnection connection)
        {
            await Networking_BasicDeliver_Commons.Publish_Hello_World(connection, messageCount, _body);
        }
    }
}
