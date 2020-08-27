using BenchmarkDotNet.Attributes;
using RabbitMQ.Client.Framing.Impl;

namespace Benchmarks.WireFormatting
{
    [ShortRunJob]
    [MemoryDiagnoser]
    public class WireFormatting_Write_ChannelClose
    {
        private readonly byte[] _buffer = new byte[1024];
        private readonly ChannelClose _method = new ChannelClose(333, string.Empty,  0099, 2999);

        [Benchmark(Baseline = true)]
        public int WriteArgumentsTo()
        {
            return _method.WriteArgumentsTo(_buffer);
        }
    }
}
