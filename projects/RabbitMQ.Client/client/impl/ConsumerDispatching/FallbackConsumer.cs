using System;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.ConsumerDispatching
{
#nullable enable
    internal sealed class FallbackConsumer : IBasicConsumer, IAsyncBasicConsumer
    {
        public IModel? Model { get; } = null;

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

        void IBasicConsumer.HandleBasicCancel(string consumerTag)
        {
            ESLog.Info($"Unhandled {nameof(IBasicConsumer.HandleBasicCancel)} for tag {consumerTag}");
        }

        void IBasicConsumer.HandleBasicCancelOk(string consumerTag)
        {
            ESLog.Info($"Unhandled {nameof(IBasicConsumer.HandleBasicCancelOk)} for tag {consumerTag}");
        }

        void IBasicConsumer.HandleBasicConsumeOk(string consumerTag)
        {
            ESLog.Info($"Unhandled {nameof(IBasicConsumer.HandleBasicConsumeOk)} for tag {consumerTag}");
        }

        void IBasicConsumer.HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, in ReadOnlyBasicProperties properties,
            ReadOnlyMemory<byte> body)
        {
            ESLog.Info($"Unhandled {nameof(IBasicConsumer.HandleBasicDeliver)} for tag {consumerTag}");
        }

        void IBasicConsumer.HandleModelShutdown(object model, ShutdownEventArgs reason)
        {
            ESLog.Info($"Unhandled {nameof(IBasicConsumer.HandleModelShutdown)}");
        }

        Task IAsyncBasicConsumer.HandleBasicCancel(string consumerTag)
        {
            ((IBasicConsumer)this).HandleBasicCancel(consumerTag);
            return Task.CompletedTask;
        }

        Task IAsyncBasicConsumer.HandleBasicCancelOk(string consumerTag)
        {
            ((IBasicConsumer)this).HandleBasicCancelOk(consumerTag);
            return Task.CompletedTask;
        }

        Task IAsyncBasicConsumer.HandleBasicConsumeOk(string consumerTag)
        {
            ((IBasicConsumer)this).HandleBasicConsumeOk(consumerTag);
            return Task.CompletedTask;
        }

        Task IAsyncBasicConsumer.HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, in ReadOnlyBasicProperties properties,
            ReadOnlyMemory<byte> body)
        {
            ((IBasicConsumer)this).HandleBasicDeliver(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body);
            return Task.CompletedTask;
        }

        Task IAsyncBasicConsumer.HandleModelShutdown(object model, ShutdownEventArgs reason)
        {
            ((IBasicConsumer)this).HandleModelShutdown(model, reason);
            return Task.CompletedTask;
        }
    }
}
