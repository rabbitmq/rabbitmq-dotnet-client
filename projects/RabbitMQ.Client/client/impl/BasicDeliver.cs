using System;
using System.Buffers;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    internal sealed class BasicDeliver : Work
    {
        private readonly string _consumerTag;
        private readonly ulong _deliveryTag;
        private readonly bool _redelivered;
        private readonly string _exchange;
        private readonly string _routingKey;
        private readonly IBasicProperties _basicProperties;
        private readonly ReadOnlyMemory<byte> _body;
        private readonly byte[] _rentedBytes;

        public override string Context => "HandleBasicDeliver";

        public BasicDeliver(IBasicConsumer consumer,
            string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body,
            byte[] rentedBytes) : base(consumer)
        {
            _consumerTag = consumerTag;
            _deliveryTag = deliveryTag;
            _redelivered = redelivered;
            _exchange = exchange;
            _routingKey = routingKey;
            _basicProperties = basicProperties;
            _body = body;
            _rentedBytes = rentedBytes;
        }

        protected override Task Execute(IAsyncBasicConsumer consumer)
        {
             return consumer.HandleBasicDeliver(_consumerTag,
                     _deliveryTag,
                     _redelivered,
                     _exchange,
                     _routingKey,
                     _basicProperties,
                     _body);
        }

        public override void PostExecute()
        {
            ArrayPool<byte>.Shared.Return(_rentedBytes);
        }
    }
}
