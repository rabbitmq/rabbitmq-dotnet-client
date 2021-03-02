using System;
using System.Text;

using BenchmarkDotNet.Attributes;

using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Benchmarks
{
    [Config(typeof(Config))]
    [BenchmarkCategory("Framing")]
    public class MethodFramingBasicAck
    {
        private readonly BasicAck _basicAck = new BasicAck(ulong.MaxValue, true);

        [Benchmark]
        public ReadOnlyMemory<byte> BasicAckWrite() => _basicAck.SerializeToFrames(0);
    }

    [Config(typeof(Config))]
    [BenchmarkCategory("Framing")]
    public class MethodFramingBasicPublish
    {
        private const string StringValue = "Exchange_OR_RoutingKey";
        private readonly Client.Framing.BasicProperties _header = new Client.Framing.BasicProperties();
        private readonly BasicPublish _basicPublish = new BasicPublish(StringValue, StringValue, false, false);
        private readonly BasicPublishMemory _basicPublishMemory = new BasicPublishMemory(Encoding.UTF8.GetBytes(StringValue), Encoding.UTF8.GetBytes(StringValue), false, false);

        [Benchmark]
        public ReadOnlyMemory<byte> BasicPublishWrite() => _basicPublish.SerializeToFrames(_header, ReadOnlyMemory<byte>.Empty, 0, 1024 * 1024);

        [Benchmark]
        public ReadOnlyMemory<byte> BasicPublishMemoryWrite() => _basicPublishMemory.SerializeToFrames(_header, ReadOnlyMemory<byte>.Empty, 0, 1024 * 1024);
    }

    [Config(typeof(Config))]
    [BenchmarkCategory("Framing")]
    public class MethodFramingChannelClose
    {
        private readonly ChannelClose _channelClose = new ChannelClose(333, string.Empty, 0099, 2999);

        [Benchmark]
        public ReadOnlyMemory<byte> ChannelCloseWrite() => _channelClose.SerializeToFrames(0);
    }
}
