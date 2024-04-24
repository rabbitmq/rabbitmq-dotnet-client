using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.ConsumerDispatching
{
#nullable enable
    internal sealed class FallbackConsumer : IAsyncBasicConsumer
    {
        public IChannel? Channel { get; } = null;

        event AsyncEventHandler<ConsumerEventArgs> IAsyncBasicConsumer.ConsumerCancelled
        {
            add { }
            remove { }
        }

        Task IAsyncBasicConsumer.HandleBasicCancelAsync(string consumerTag)
        {
            ESLog.Info($"Unhandled {nameof(IAsyncBasicConsumer.HandleBasicCancelAsync)} for tag {consumerTag}");
            return Task.CompletedTask;
        }

        Task IAsyncBasicConsumer.HandleBasicCancelOkAsync(string consumerTag)
        {
            ESLog.Info($"Unhandled {nameof(IAsyncBasicConsumer.HandleBasicCancelOkAsync)} for tag {consumerTag}");
            return Task.CompletedTask;
        }

        Task IAsyncBasicConsumer.HandleBasicConsumeOkAsync(string consumerTag)
        {
            ESLog.Info($"Unhandled {nameof(IAsyncBasicConsumer.HandleBasicConsumeOkAsync)} for tag {consumerTag}");
            return Task.CompletedTask;
        }

        Task IAsyncBasicConsumer.HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey,
            ReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken)
        {
            ESLog.Info($"Unhandled {nameof(IAsyncBasicConsumer.HandleBasicDeliverAsync)} for tag {consumerTag}");
            return Task.CompletedTask;
        }

        Task IAsyncBasicConsumer.HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason)
        {
            ESLog.Info($"Unhandled {nameof(IAsyncBasicConsumer.HandleChannelShutdownAsync)}");
            return Task.CompletedTask;
        }
    }
}
