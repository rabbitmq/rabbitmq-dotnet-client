using System;
using System.Buffers;
using System.Threading.Tasks;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.client.impl
{
    internal sealed class InlineConsumerDispatcher : IConsumerDispatcher
    {
        private readonly IModel _model;

        public InlineConsumerDispatcher(IModel model)
        {
            _model = model;
        }

        public bool IsShutdown { get; private set; }

        public void HandleBasicCancel(IBasicConsumer consumer, string consumerTag)
        {
            consumer.HandleBasicCancel(consumerTag);
        }

        public void HandleBasicCancelOk(IBasicConsumer consumer, string consumerTag)
        {
            consumer.HandleBasicCancelOk(consumerTag);
        }

        public void HandleBasicConsumeOk(IBasicConsumer consumer, string consumerTag)
        {
            consumer.HandleBasicConsumeOk(consumerTag);
        }

        public void HandleBasicDeliver(IBasicConsumer consumer, string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties basicProperties, ReadOnlyMemory<byte> body, byte[] rentedArray)
        {
            consumer.HandleBasicDeliver(consumerTag, deliveryTag, redelivered, exchange, routingKey, basicProperties, body);
            ArrayPool<byte>.Shared.Return(rentedArray);
        }

        public void HandleModelShutdown(IBasicConsumer consumer, ShutdownEventArgs reason)
        {
            consumer.HandleModelShutdown(_model, reason);
        }

        public void Quiesce()
        {
            IsShutdown = true;
        }

        public Task Shutdown(IModel model)
        {
            return Task.CompletedTask;
        }
    }
}
