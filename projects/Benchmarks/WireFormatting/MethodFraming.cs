using System;
using System.Buffers;
using System.Text;

using BenchmarkDotNet.Attributes;
using RabbitMQ.Client;
using RabbitMQ.Client.client.impl;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;

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
        public ReadOnlyMemory<byte> BasicAckWrite()
        {
            ArrayBufferWriter<byte> _writer = new ArrayBufferWriter<byte>();
            Framing.SerializeToFrames(ref _basicAck, _writer, Channel);
            return _writer.WrittenMemory;
        }
    }

    [Config(typeof(Config))]
    [BenchmarkCategory("Framing")]
    public class MethodFramingBasicPublish
    {
        private const string StringValue = "Exchange_OR_RoutingKey";
        private BasicPublish _basicPublish = new BasicPublish(StringValue, StringValue, false, false);
        private BasicPublishMemory _basicPublishMemory = new BasicPublishMemory(Encoding.UTF8.GetBytes(StringValue), Encoding.UTF8.GetBytes(StringValue), false, false);
        private EmptyBasicProperty _propertiesEmpty = new EmptyBasicProperty();
        private BasicProperties _properties = new BasicProperties { AppId = "Application id", MessageId = "Random message id" };
        private readonly ReadOnlyMemory<byte> _bodyEmpty = ReadOnlyMemory<byte>.Empty;
        private readonly ReadOnlyMemory<byte> _body = new byte[512];

        [Params(0)]
        public ushort Channel { get; set; }

        [Params(0xFFFF)]
        public int FrameMax { get; set; }

        [Benchmark]
        public ReadOnlyMemory<byte> BasicPublishWriteNonEmpty()
        {
            ArrayBufferWriter<byte> _writer = new ArrayBufferWriter<byte>();
            Framing.SerializeToFrames(ref _basicPublish, ref _properties, _body, _writer, Channel, FrameMax);
            return _writer.WrittenMemory;
        }

        [Benchmark]
        public ReadOnlyMemory<byte> BasicPublishWrite()
        {
            ArrayBufferWriter<byte> _writer = new ArrayBufferWriter<byte>();
            Framing.SerializeToFrames(ref _basicPublish, ref _propertiesEmpty, _bodyEmpty, _writer, Channel, FrameMax);
            return _writer.WrittenMemory;
        }

        [Benchmark]
        public ReadOnlyMemory<byte> BasicPublishMemoryWrite()
        {
            ArrayBufferWriter<byte> _writer = new ArrayBufferWriter<byte>();
            Framing.SerializeToFrames(ref _basicPublishMemory, ref _propertiesEmpty, _bodyEmpty, _writer, Channel, FrameMax);
            return _writer.WrittenMemory;
        }
    }

    [Config(typeof(Config))]
    [BenchmarkCategory("Framing")]
    public class MethodFramingChannelClose
    {
        private ChannelClose _channelClose = new ChannelClose(333, string.Empty, 0099, 2999);

        [Params(0)]
        public ushort Channel { get; set; }

        [Benchmark]
        public ReadOnlyMemory<byte> ChannelCloseWrite()
        {
            ArrayBufferWriter<byte> _writer = new ArrayBufferWriter<byte>();
            Framing.SerializeToFrames(ref _channelClose, _writer, Channel);
            return _writer.WrittenMemory;
        }
    }
}
