using System;
using BenchmarkDotNet.Attributes;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;

namespace Benchmarks.WireFormatting
{
    [ShortRunJob]
    [MemoryDiagnoser]
    public class WireFormatting_Read_ChannelClose
    {
        private readonly byte[] _buffer = new byte[1024];

        public WireFormatting_Read_ChannelClose()
        {
            new ChannelClose(333, string.Empty,  0099, 2999).WriteArgumentsTo(_buffer);
        }

        [Benchmark]
        public object ReadArgumentsFrom_MethodArgumentReader()
        {
            var reader = new MethodArgumentReader(new ReadOnlySpan<byte>(_buffer));
            MethodBase basicAck = new ChannelClose();
            basicAck.ReadArgumentsFrom(ref reader);
            return basicAck;
        }

        [Benchmark(Baseline = true)]
        public object ReadFromSpan()
        {
            return new ChannelClose(new ReadOnlySpan<byte>(_buffer));
        }
    }
}
