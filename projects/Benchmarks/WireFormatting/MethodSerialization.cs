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
        public readonly Memory<byte> Buffer = new byte[1024];

        [GlobalSetup]
        public virtual void SetUp() { }
    }

    public class MethodBasicAck : MethodSerializationBase
    {
        private readonly BasicAck _basicAck = new BasicAck(ulong.MaxValue, true);
        public override void SetUp() => _basicAck.WriteTo(Buffer.Span);

        [Benchmark]
        public ulong BasicAckRead() => new BasicAck(Buffer.Span)._deliveryTag; // return one property to not box when returning an object instead

        [Benchmark]
        public int BasicAckWrite() => _basicAck.WriteTo(Buffer.Span);
    }

    public class MethodBasicDeliver : MethodSerializationBase
    {
        private const string StringValue = "Exchange_OR_RoutingKey";
        private readonly BasicPublish _basicPublish = new BasicPublish(StringValue, StringValue, false, false);
        private readonly BasicPublishMemory _basicPublishMemory = new BasicPublishMemory(Encoding.UTF8.GetBytes(StringValue), Encoding.UTF8.GetBytes(StringValue), false, false);

        public override void SetUp()
        {
            int offset = Client.Impl.WireFormatting.WriteShortstr(ref Buffer.Span.GetStart(), string.Empty);
            offset += Client.Impl.WireFormatting.WriteLonglong(ref Buffer.Span.GetOffset(offset), 0);
            offset += Client.Impl.WireFormatting.WriteBits(ref Buffer.Span.GetOffset(offset), false);
            offset += Client.Impl.WireFormatting.WriteShortstr(ref Buffer.Span.GetOffset(offset), string.Empty);
            Client.Impl.WireFormatting.WriteShortstr(ref Buffer.Span.GetOffset(offset), string.Empty);
        }

        [Benchmark]
        public object BasicDeliverRead() => new BasicDeliver(Buffer)._consumerTag; // return one property to not box when returning an object instead

        [Benchmark]
        public int BasicPublishWrite() => _basicPublish.WriteTo(Buffer.Span);

        [Benchmark]
        public int BasicPublishMemoryWrite() => _basicPublishMemory.WriteTo(Buffer.Span);

        [Benchmark]
        public int BasicPublishSize() => _basicPublish.GetRequiredBufferSize();

        [Benchmark]
        public int BasicPublishMemorySize() => _basicPublishMemory.GetRequiredBufferSize();
    }

    public class MethodChannelClose : MethodSerializationBase
    {
        private readonly ChannelClose _channelClose = new ChannelClose(333, string.Empty, 0099, 2999);

        public override void SetUp() => _channelClose.WriteTo(Buffer.Span);

        [Benchmark]
        public object ChannelCloseRead() => new ChannelClose(Buffer.Span)._replyText; // return one property to not box when returning an object instead

        [Benchmark]
        public int ChannelCloseWrite() => _channelClose.WriteTo(Buffer.Span);
    }

    public class MethodBasicProperties : MethodSerializationBase
    {
        private readonly IAmqpWriteable _basicProperties = new BasicProperties { Persistent = true, AppId = "AppId", ContentEncoding = "content", };
        public override void SetUp() => _basicProperties.WriteTo(Buffer.Span);

        [Benchmark]
        public ReadOnlyBasicProperties BasicPropertiesRead() => new ReadOnlyBasicProperties(Buffer.Span);

        [Benchmark]
        public int BasicPropertiesWrite() => _basicProperties.WriteTo(Buffer.Span);

        [Benchmark]
        public int BasicDeliverSize() => _basicProperties.GetRequiredBufferSize();
    }
}
