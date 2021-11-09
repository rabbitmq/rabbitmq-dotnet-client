using System;
using System.Text;

using BenchmarkDotNet.Attributes;

using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;

using BasicProperties = RabbitMQ.Client.Framing.BasicProperties;

namespace RabbitMQ.Benchmarks
{
    [Config(typeof(Config))]
    [BenchmarkCategory("Framing")]
    public class MethodFramingBasicAck
    {
        private BasicAck _basicAck = new BasicAck(ulong.MaxValue, true);

        [Params(0)]
        public ushort Channel { get; set; }

        [Benchmark]
        public ReadOnlyMemory<byte> BasicAckWrite() => Framing.SerializeToFrames(ref _basicAck, Channel);
    }

    [Config(typeof(Config))]
    [BenchmarkCategory("Framing")]
    public class MethodFramingBasicPublish
    {
        private const string StringValue = "Exchange_OR_RoutingKey";
        private BasicPublish _basicPublish = new BasicPublish(StringValue, StringValue, false, false);
        private BasicPublishMemory _basicPublishMemory = new BasicPublishMemory(Encoding.UTF8.GetBytes(StringValue), Encoding.UTF8.GetBytes(StringValue), false, false);
        private readonly BasicProperties _propertiesEmpty = new BasicProperties();
        private readonly BasicProperties _properties = new BasicProperties { AppId = "Application id", MessageId = "Random message id" };
        private readonly ReadOnlyMemory<byte> _bodyEmpty = ReadOnlyMemory<byte>.Empty;
        private readonly ReadOnlyMemory<byte> _body = new byte[512];

        [Params(0)]
        public ushort Channel { get; set; }

        [Params(0xFFFF)]
        public int FrameMax { get; set; }

        [Benchmark]
        public ReadOnlyMemory<byte> BasicPublishWriteNonEmpty() => Framing.SerializeToFrames(ref _basicPublish, _properties, _body, Channel, FrameMax);

        [Benchmark]
        public ReadOnlyMemory<byte> BasicPublishWrite() => Framing.SerializeToFrames(ref _basicPublish, _propertiesEmpty, _bodyEmpty, Channel, FrameMax);

        [Benchmark]
        public ReadOnlyMemory<byte> BasicPublishMemoryWrite() => Framing.SerializeToFrames(ref _basicPublishMemory, _propertiesEmpty, _bodyEmpty, Channel, FrameMax);
    }

    [Config(typeof(Config))]
    [BenchmarkCategory("Framing")]
    public class MethodFramingChannelClose
    {
        private ChannelClose _channelClose = new ChannelClose(333, string.Empty, 0099, 2999);

        [Params(0)]
        public ushort Channel { get; set; }

        [Benchmark]
        public ReadOnlyMemory<byte> ChannelCloseWrite() => Framing.SerializeToFrames(ref _channelClose, Channel);
    }
}
