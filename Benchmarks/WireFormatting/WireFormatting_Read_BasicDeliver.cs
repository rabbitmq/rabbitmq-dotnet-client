using System;
using BenchmarkDotNet.Attributes;
using RabbitMQ.Client.Impl;
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

        [Benchmark]
        public object ReadArgumentsFrom_MethodArgumentReader()
        {
            var reader = new MethodArgumentReader(new ReadOnlySpan<byte>(_buffer));
            MethodBase basicAck = new BasicDeliver();
            basicAck.ReadArgumentsFrom(ref reader);
            return basicAck;
        }

        [Benchmark(Baseline = true)]
        public object ReadFromSpan()
        {
            return new BasicDeliver(new ReadOnlySpan<byte>(_buffer));
        }
    }
}
