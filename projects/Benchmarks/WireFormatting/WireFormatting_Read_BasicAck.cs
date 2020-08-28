using System;
using BenchmarkDotNet.Attributes;
using RabbitMQ.Client.Framing.Impl;

namespace Benchmarks.WireFormatting
{
    [ShortRunJob]
    [MemoryDiagnoser]
    public class WireFormatting_Read_BasicAck
    {
        private readonly byte[] _buffer = new byte[1024];

        public WireFormatting_Read_BasicAck()
        {
            new BasicAck(ulong.MaxValue, true).WriteArgumentsTo(_buffer);
        }

        [Benchmark(Baseline = true)]
        public object ReadFromSpan()
        {
            return new BasicAck(new ReadOnlySpan<byte>(_buffer));
        }
    }
}
