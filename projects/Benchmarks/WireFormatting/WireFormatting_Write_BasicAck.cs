using BenchmarkDotNet.Attributes;
using RabbitMQ.Client.Framing.Impl;

namespace Benchmarks.WireFormatting
{
    [ShortRunJob]
    [MemoryDiagnoser]
    public class WireFormatting_Write_BasicAck
    {
        private readonly byte[] _buffer = new byte[1024];
        private readonly BasicAck _method = new BasicAck(ulong.MaxValue, true);

        [Benchmark(Baseline = true)]
        public int WriteArgumentsTo()
        {
            return _method.WriteArgumentsTo(_buffer);
        }
    }
}
