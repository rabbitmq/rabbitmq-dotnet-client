using BenchmarkDotNet.Attributes;
using BasicDeliver = RabbitMQ.Client.Framing.Impl.BasicDeliver;

namespace Benchmarks.WireFormatting
{
    [ShortRunJob]
    [MemoryDiagnoser]
    public class WireFormatting_Write_BasicDeliver
    {
        private readonly byte[] _buffer = new byte[1024];
        private readonly BasicDeliver _method = new BasicDeliver(string.Empty, 0, false, string.Empty, string.Empty);

        [Benchmark(Baseline = true)]
        public int WriteArgumentsTo()
        {
            return _method.WriteArgumentsTo(_buffer);
        }
    }
}
