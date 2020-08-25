using BenchmarkDotNet.Attributes;
using RabbitMQ.Client.Impl;
using BasicDeliver = RabbitMQ.Client.Framing.Impl.BasicDeliver;

namespace Benchmarks.WireFormatting
{
    [ShortRunJob]
    [MemoryDiagnoser]
    public class WireFormatting_Write_BasicDeliver
    {
        private readonly byte[] _buffer = new byte[1024];
        private readonly BasicDeliver _method = new BasicDeliver(string.Empty, 0, false, string.Empty, string.Empty);

        [Benchmark]
        public int WriteArgumentsTo_MethodArgumentWriter()
        {
            var writer = new MethodArgumentWriter(_buffer);
            _method.WriteArgumentsTo(ref writer);
            return writer.Offset;
        }

        [Benchmark(Baseline = true)]
        public int WriteArgumentsTo()
        {
            return _method.WriteArgumentsTo(_buffer);
        }
    }
}
