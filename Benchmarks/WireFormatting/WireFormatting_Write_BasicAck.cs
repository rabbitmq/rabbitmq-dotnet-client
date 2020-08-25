using BenchmarkDotNet.Attributes;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;

namespace Benchmarks.WireFormatting
{
    [ShortRunJob]
    [MemoryDiagnoser]
    public class WireFormatting_Write_BasicAck
    {
        private readonly byte[] _buffer = new byte[1024];
        private readonly BasicAck _method = new BasicAck(ulong.MaxValue, true);

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
