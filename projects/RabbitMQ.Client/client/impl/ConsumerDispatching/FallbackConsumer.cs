using System;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.ConsumerDispatching
{
#nullable enable
    internal sealed class FallbackConsumer : IBasicConsumer, IAsyncBasicConsumer
    {
        public IChannel? Channel { get; } = null;

        event AsyncEventHandler<ConsumerEventArgs> IAsyncBasicConsumer.ConsumerCancelled
        {
            add { }
            remove { }
        }

        event EventHandler<ConsumerEventArgs> IBasicConsumer.ConsumerCancelled
        {
            add { }
            remove { }
        }

        void IBasicConsumer.HandleBasicCancel(ConsumerTag consumerTag)
        {
            ESLog.Info($"Unhandled {nameof(IBasicConsumer.HandleBasicCancel)} for tag {consumerTag}");
        }

        void IBasicConsumer.HandleBasicCancelOk(ConsumerTag consumerTag)
        {
            ESLog.Info($"Unhandled {nameof(IBasicConsumer.HandleBasicCancelOk)} for tag {consumerTag}");
        }

        void IBasicConsumer.HandleBasicConsumeOk(ConsumerTag consumerTag)
        {
            ESLog.Info($"Unhandled {nameof(IBasicConsumer.HandleBasicConsumeOk)} for tag {consumerTag}");
        }

        Task IBasicConsumer.HandleBasicDeliverAsync(ConsumerTag consumerTag, ulong deliveryTag, bool redelivered,
            ExchangeName exchange, RoutingKey routingKey,
            ReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            ESLog.Info($"Unhandled {nameof(IBasicConsumer.HandleBasicDeliverAsync)} for tag {consumerTag}");
            return Task.CompletedTask;
        }

        void IBasicConsumer.HandleChannelShutdown(object channel, ShutdownEventArgs reason)
        {
            ESLog.Info($"Unhandled {nameof(IBasicConsumer.HandleChannelShutdown)}");
        }

        Task IAsyncBasicConsumer.HandleBasicCancel(ConsumerTag consumerTag)
        {
            ((IBasicConsumer)this).HandleBasicCancel(consumerTag);
            return Task.CompletedTask;
        }

        Task IAsyncBasicConsumer.HandleBasicCancelOk(ConsumerTag consumerTag)
        {
            ((IBasicConsumer)this).HandleBasicCancelOk(consumerTag);
            return Task.CompletedTask;
        }

        Task IAsyncBasicConsumer.HandleBasicConsumeOk(ConsumerTag consumerTag)
        {
            ((IBasicConsumer)this).HandleBasicConsumeOk(consumerTag);
            return Task.CompletedTask;
        }

        Task IAsyncBasicConsumer.HandleBasicDeliver(ConsumerTag consumerTag, ulong deliveryTag, bool redelivered,
            ExchangeName exchange, RoutingKey routingKey,
            in ReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            return ((IBasicConsumer)this).HandleBasicDeliverAsync(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body);
        }

        Task IAsyncBasicConsumer.HandleChannelShutdown(object channel, ShutdownEventArgs reason)
        {
            ((IBasicConsumer)this).HandleChannelShutdown(channel, reason);
            return Task.CompletedTask;
        }
    }
}
