using BenchmarkDotNet.Attributes;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;

namespace Benchmarks.WireFormatting
{
    [ShortRunJob]
    [MemoryDiagnoser]
    public class WireFormatting_Write_ChannelClose
    {
        private readonly byte[] _buffer = new byte[1024];
        private readonly ChannelClose _method = new ChannelClose(333, string.Empty,  0099, 2999);

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
