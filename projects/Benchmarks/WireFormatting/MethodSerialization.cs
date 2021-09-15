using System;
using System.Text;

using BenchmarkDotNet.Attributes;
using RabbitMQ.Client;
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
        public override void SetUp() => _basicAck.WriteTo(_buffer.Span);

        [Benchmark]
        public ulong BasicAckRead() => new BasicAck(_buffer.Span)._deliveryTag; // return one property to not box when returning an object instead

        [Benchmark]
        public int BasicAckWrite() => _basicAck.WriteTo(_buffer.Span);
    }

    public class MethodBasicDeliver : MethodSerializationBase
    {
        private const string StringValue = "Exchange_OR_RoutingKey";
        private readonly BasicPublish _basicPublish = new BasicPublish(StringValue, StringValue, false, false);
        private readonly BasicPublishMemory _basicPublishMemory = new BasicPublishMemory(Encoding.UTF8.GetBytes(StringValue), Encoding.UTF8.GetBytes(StringValue), false, false);

        public override void SetUp()
        {
            int offset = Client.Impl.WireFormatting.WriteShortstr(ref _buffer.Span.GetStart(), string.Empty);
            offset += Client.Impl.WireFormatting.WriteLonglong(ref _buffer.Span.GetOffset(offset), 0);
            offset += Client.Impl.WireFormatting.WriteBits(ref _buffer.Span.GetOffset(offset), false);
            offset += Client.Impl.WireFormatting.WriteShortstr(ref _buffer.Span.GetOffset(offset), string.Empty);
            Client.Impl.WireFormatting.WriteShortstr(ref _buffer.Span.GetOffset(offset), string.Empty);
        }

        [Benchmark]
        public object BasicDeliverRead() => new BasicDeliver(_buffer.Span)._consumerTag; // return one property to not box when returning an object instead

        [Benchmark]
        public int BasicPublishWrite() => _basicPublish.WriteTo(_buffer.Span);

        [Benchmark]
        public int BasicPublishMemoryWrite() => _basicPublishMemory.WriteTo(_buffer.Span);

        [Benchmark]
        public int BasicPublishSize() => _basicPublish.GetRequiredBufferSize();

        [Benchmark]
        public int BasicPublishMemorySize() => _basicPublishMemory.GetRequiredBufferSize();
    }

    public class MethodChannelClose : MethodSerializationBase
    {
        private readonly ChannelClose _channelClose = new ChannelClose(333, string.Empty, 0099, 2999);

        public override void SetUp() => _channelClose.WriteTo(_buffer.Span);

        [Benchmark]
        public object ChannelCloseRead() => new ChannelClose(_buffer.Span)._replyText; // return one property to not box when returning an object instead

        [Benchmark]
        public int ChannelCloseWrite() => _channelClose.WriteTo(_buffer.Span);
    }

    public class MethodBasicProperties : MethodSerializationBase
    {
        private readonly IAmqpWriteable _basicProperties = new BasicProperties { Persistent = true, AppId = "AppId", ContentEncoding = "content", };
        public override void SetUp() => _basicProperties.WriteTo(_buffer.Span);

        [Benchmark]
        public ReadOnlyBasicProperties BasicPropertiesRead() => new ReadOnlyBasicProperties(_buffer.Span);

        [Benchmark]
        public int BasicPropertiesWrite() => _basicProperties.WriteTo(_buffer.Span);

        [Benchmark]
        public int BasicDeliverSize() => _basicProperties.GetRequiredBufferSize();
    }
}
