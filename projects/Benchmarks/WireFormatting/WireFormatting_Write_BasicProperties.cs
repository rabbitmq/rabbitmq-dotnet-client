using System;
using BenchmarkDotNet.Attributes;
using BasicProperties = RabbitMQ.Client.Framing.BasicProperties;

namespace Benchmarks.WireFormatting
{
    [ShortRunJob]
    [MemoryDiagnoser]
    public class WireFormatting_Write_BasicProperties
    {
        private readonly byte[] _buffer = new byte[1024];
        private readonly BasicProperties _properties = new BasicProperties
        {
            Persistent = true,
            AppId = "AppId",
            ContentEncoding = "content",
        };

        [Benchmark(Baseline = true)]
        public void WritePropertiesToSpan()
        {
            _properties.WritePropertiesTo(new Span<byte>(_buffer));
        }
    }
}
