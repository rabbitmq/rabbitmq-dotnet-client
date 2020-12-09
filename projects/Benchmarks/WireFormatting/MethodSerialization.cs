using System;

using BenchmarkDotNet.Attributes;

using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Benchmarks
{
    [Config(typeof(Config))]
    public class MethodSerialization
    {
        private readonly BasicAck _basicAck = new BasicAck(ulong.MaxValue, true);
        private readonly BasicDeliver _basicDeliver = new BasicDeliver(string.Empty, 0, false, string.Empty, string.Empty);
        private readonly BasicProperties _basicProperties = new BasicProperties
        {
            Persistent = true,
            AppId = "AppId",
            ContentEncoding = "content",
        };
        private readonly ChannelClose _channelClose = new ChannelClose(333, string.Empty, 0099, 2999);


        private readonly Memory<byte> _basicAckBuffer = new byte[1024];
        private readonly Memory<byte> _basicDeliverBuffer = new byte[1024];
        private readonly Memory<byte> _basicPropertiesBuffer = new byte[1024];
        private readonly Memory<byte> _channelCloseBuffer = new byte[1024];
        private readonly Memory<byte> _writeBuffer = new byte[1024];

        [GlobalSetup]
        public void SetUp()
        {
            new BasicAck(ulong.MaxValue, true).WriteArgumentsTo(_basicAckBuffer.Span);
            new BasicDeliver(string.Empty, 0, false, string.Empty, string.Empty).WriteArgumentsTo(_basicDeliverBuffer.Span);
            new BasicProperties
            {
                Persistent = true,
                AppId = "AppId",
                ContentEncoding = "content"
            }.WritePropertiesTo(_basicPropertiesBuffer.Span);
            new ChannelClose(333, string.Empty, 0099, 2999).WriteArgumentsTo(_channelCloseBuffer.Span);
        }

        [Benchmark]
        public object BasicAckRead() => new BasicAck(_basicAckBuffer.Span);

        [Benchmark]
        public int BasicAckWrite() => _basicAck.WriteArgumentsTo(_writeBuffer.Span);

        [Benchmark]
        public object BasicDeliverRead() => new BasicDeliver(_basicDeliverBuffer.Span);

        [Benchmark]
        public int BasicDeliverWrite() => _basicDeliver.WriteArgumentsTo(_writeBuffer.Span);

        [Benchmark]
        public object BasicPropertiesRead() => new BasicProperties(_basicPropertiesBuffer.Span);

        [Benchmark]
        public void BasicPropertiesWrite() => _basicProperties.WritePropertiesTo(_writeBuffer.Span);

        [Benchmark]
        public object ChannelCloseRead() => new ChannelClose(_channelCloseBuffer.Span);

        [Benchmark]
        public int ChannelCloseWrite() => _channelClose.WriteArgumentsTo(_writeBuffer.Span);
    }
}
