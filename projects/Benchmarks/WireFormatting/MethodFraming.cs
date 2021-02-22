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
        private readonly OutgoingCommand _basicAck = new OutgoingCommand(new BasicAck(ulong.MaxValue, true));

        [Benchmark]
        public ReadOnlyMemory<byte> BasicAckWrite() => _basicAck.SerializeToFrames(0, 1024 * 1024);
    }

    [Config(typeof(Config))]
    [BenchmarkCategory("Framing")]
    public class MethodFramingBasicPublish
    {
        private const string StringValue = "Exchange_OR_RoutingKey";
        private readonly OutgoingContentCommand _basicPublish = new OutgoingContentCommand(new BasicPublish(StringValue, StringValue, false, false), new Client.Framing.BasicProperties(), ReadOnlyMemory<byte>.Empty);
        private readonly OutgoingContentCommand _basicPublishMemory = new OutgoingContentCommand(new BasicPublishMemory(Encoding.UTF8.GetBytes(StringValue), Encoding.UTF8.GetBytes(StringValue), false, false), new Client.Framing.BasicProperties(), ReadOnlyMemory<byte>.Empty);

        [Benchmark]
        public ReadOnlyMemory<byte> BasicPublishWrite() => _basicPublish.SerializeToFrames(0, 1024 * 1024);

        [Benchmark]
        public ReadOnlyMemory<byte> BasicPublishMemoryWrite() => _basicPublishMemory.SerializeToFrames(0, 1024 * 1024);
    }

    [Config(typeof(Config))]
    [BenchmarkCategory("Framing")]
    public class MethodFramingChannelClose
    {
        private readonly OutgoingCommand _channelClose = new OutgoingCommand(new ChannelClose(333, string.Empty, 0099, 2999));

        [Benchmark]
        public ReadOnlyMemory<byte> ChannelCloseWrite() => _channelClose.SerializeToFrames(0, 1024 * 1024);
    }
}
