using System;

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
        private readonly BasicDeliver _basicDeliver = new BasicDeliver(string.Empty, 0, false, string.Empty, string.Empty);
        public override void SetUp() => _basicDeliver.WriteArgumentsTo(_buffer.Span);

        [Benchmark]
        public object BasicDeliverRead() => new BasicDeliver(_buffer.Span);

        [Benchmark]
        public int BasicDeliverWrite() => _basicDeliver.WriteArgumentsTo(_buffer.Span);

        [Benchmark]
        public int BasicDeliverSize() => _basicDeliver.GetRequiredBufferSize();
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
