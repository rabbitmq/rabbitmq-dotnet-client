
using BenchmarkDotNet.Attributes;

using BasicProperties = RabbitMQ.Client.Framing.BasicProperties;

namespace Benchmarks.WireFormatting
{
    [ShortRunJob]
    [MemoryDiagnoser]
    public class WireFormatting_Read_BasicProperties
    {
        private readonly byte[] _buffer = new byte[1024];

        public WireFormatting_Read_BasicProperties()
        {
            new BasicProperties
            {
                Persistent = true,
                AppId = "AppId",
                ContentEncoding = "content"
            }.WritePropertiesTo(_buffer);
        }

        [Benchmark(Baseline = true)]
        public object ReadFromSpan()
        {
            return new BasicProperties(_buffer);
        }
    }
}
