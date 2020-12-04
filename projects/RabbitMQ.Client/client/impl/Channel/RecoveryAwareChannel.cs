using System;
using System.Threading.Tasks;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.client.impl.Channel
{
    #nullable enable
    /// <summary>
    /// A recovery aware channel created by the <see cref="ConnectionFactory"/> when <see cref="ConnectionFactory.AutomaticRecoveryEnabled"/> is set.
    /// </summary>
    public sealed class RecoveryAwareChannel : Channel
    {
        private ulong _activeDeliveryTagOffset;
        private ulong _maxSeenDeliveryTag;

        internal RecoveryAwareChannel(ISession session, ConsumerWorkService workService)
            : base(session, workService)
        {
        }

        internal void TakeOverChannel(RecoveryAwareChannel channel)
        {
            base.TakeOverChannel(channel);
            _activeDeliveryTagOffset = channel._activeDeliveryTagOffset + channel._maxSeenDeliveryTag;
            _maxSeenDeliveryTag = 0;
        }

        private ulong OffsetDeliveryTag(ulong deliveryTag)
        {
            return deliveryTag + _activeDeliveryTagOffset;
        }

        protected override void HandleBasicGetOk(ulong deliveryTag, bool redelivered, string exchange, string routingKey, uint messageCount, IBasicProperties basicProperties, ReadOnlyMemory<byte> body, byte[] rentedArray)
        {
            if (deliveryTag > _maxSeenDeliveryTag)
            {
                _maxSeenDeliveryTag = deliveryTag;
            }

            base.HandleBasicGetOk(OffsetDeliveryTag(deliveryTag), redelivered, exchange, routingKey, messageCount, basicProperties, body, rentedArray);
        }

        protected override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties basicProperties, ReadOnlyMemory<byte> body, byte[] rentedArray)
        {
            if (deliveryTag > _maxSeenDeliveryTag)
            {
                _maxSeenDeliveryTag = deliveryTag;
            }

            base.HandleBasicDeliver(consumerTag, OffsetDeliveryTag(deliveryTag), redelivered, exchange, routingKey, basicProperties, body, rentedArray);
        }

        /// <inheritdoc />
        public override ValueTask AckMessageAsync(ulong deliveryTag, bool multiple)
        {
            ulong realTag = deliveryTag - _activeDeliveryTagOffset;
            if (realTag > 0 && realTag <= deliveryTag)
            {
                return base.AckMessageAsync(realTag, multiple);
            }

            return default;
        }

        /// <inheritdoc />
        public override ValueTask NackMessageAsync(ulong deliveryTag, bool multiple, bool requeue)
        {
            ulong realTag = deliveryTag - _activeDeliveryTagOffset;
            if (realTag > 0 && realTag <= deliveryTag)
            {
                base.NackMessageAsync(realTag, multiple, requeue);
            }

            return default;
        }
    }
}
