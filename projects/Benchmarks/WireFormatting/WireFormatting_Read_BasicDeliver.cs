using System;
using BenchmarkDotNet.Attributes;
using BasicDeliver = RabbitMQ.Client.Framing.Impl.BasicDeliver;

namespace Benchmarks.WireFormatting
{
    [ShortRunJob]
    [MemoryDiagnoser]
    public class WireFormatting_Read_BasicDeliver
    {
        private readonly byte[] _buffer = new byte[1024];

        public WireFormatting_Read_BasicDeliver()
        {
            new BasicDeliver(string.Empty, 0, false, string.Empty, string.Empty).WriteArgumentsTo(_buffer);
        }

        [Benchmark(Baseline = true)]
        public object ReadFromSpan()
        {
            return new BasicDeliver(new ReadOnlySpan<byte>(_buffer));
        }
    }
}
