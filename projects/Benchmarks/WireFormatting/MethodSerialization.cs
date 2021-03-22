using System;
using System.Text;

using BenchmarkDotNet.Attributes;

using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Benchmarks
{
    [Config(typeof(Config))]
    [BenchmarkCategory("Methods")]
    public class MethodSerializationBase
    {
        protected readonly Memory<byte> _buffer = new byte[1024];

        [GlobalSetup]
        public virtual void SetUp() { }
    }

    public class MethodBasicAck : MethodSerializationBase
    {
        private readonly BasicAck _basicAck = new BasicAck(ulong.MaxValue, true);
        public override void SetUp() => _basicAck.WriteArgumentsTo(_buffer.Span);

        [Benchmark]
        public object BasicAckRead() => new BasicAck(_buffer.Span);

        [Benchmark]
        public int BasicAckWrite() => _basicAck.WriteArgumentsTo(_buffer.Span);
    }

    public class MethodBasicDeliver : MethodSerializationBase
    {
        private const string StringValue = "Exchange_OR_RoutingKey";
        private readonly BasicPublish _basicPublish = new BasicPublish(StringValue, StringValue, false, false);
        private readonly BasicPublishMemory _basicPublishMemory = new BasicPublishMemory(Encoding.UTF8.GetBytes(StringValue), Encoding.UTF8.GetBytes(StringValue), false, false);

        public override void SetUp()
        {
            int length = _buffer.Length;
            int offset = Client.Impl.WireFormatting.WriteShortstr(ref _buffer.Span.GetStart(), string.Empty);
            offset += Client.Impl.WireFormatting.WriteLonglong(ref _buffer.Span.GetOffset(offset), 0);
            offset += Client.Impl.WireFormatting.WriteBits(ref _buffer.Span.GetOffset(offset), false);
            offset += Client.Impl.WireFormatting.WriteShortstr(ref _buffer.Span.GetOffset(offset), string.Empty);
            Client.Impl.WireFormatting.WriteShortstr(ref _buffer.Span.GetOffset(offset), string.Empty);
        }

        [Benchmark]
        public object BasicDeliverRead() => new BasicDeliver(_buffer.Span);

        [Benchmark]
        public int BasicPublishWrite() => _basicPublish.WriteArgumentsTo(_buffer.Span);

        [Benchmark]
        public int BasicPublishMemoryWrite() => _basicPublishMemory.WriteArgumentsTo(_buffer.Span);

        [Benchmark]
        public int BasicPublishSize() => _basicPublish.GetRequiredBufferSize();

        [Benchmark]
        public int BasicPublishMemorySize() => _basicPublishMemory.GetRequiredBufferSize();
    }

    public class MethodChannelClose : MethodSerializationBase
    {
        private readonly ChannelClose _channelClose = new ChannelClose(333, string.Empty, 0099, 2999);

        public override void SetUp() => _channelClose.WriteArgumentsTo(_buffer.Span);

        [Benchmark]
        public object ChannelCloseRead() => new ChannelClose(_buffer.Span);

        [Benchmark]
        public int ChannelCloseWrite() => _channelClose.WriteArgumentsTo(_buffer.Span);
    }

    public class MethodBasicProperties : MethodSerializationBase
    {
        private readonly BasicProperties _basicProperties = new BasicProperties { Persistent = true, AppId = "AppId", ContentEncoding = "content", };
        public override void SetUp() => _basicProperties.WritePropertiesTo(_buffer.Span);

        [Benchmark]
        public object BasicPropertiesRead() => new BasicProperties(_buffer.Span);

        [Benchmark]
        public int BasicPropertiesWrite() => _basicProperties.WritePropertiesTo(_buffer.Span);

        [Benchmark]
        public int BasicDeliverSize() => _basicProperties.GetRequiredPayloadBufferSize();
    }
}
