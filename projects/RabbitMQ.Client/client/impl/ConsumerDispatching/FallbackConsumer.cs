using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.ConsumerDispatching
{
    internal sealed class FallbackConsumer : IAsyncBasicConsumer
    {
        public IChannel? Channel { get; } = null;

        Task IAsyncBasicConsumer.HandleBasicCancelAsync(string consumerTag, CancellationToken cancellationToken)
        {
            ESLog.Info($"Unhandled {nameof(IAsyncBasicConsumer.HandleBasicCancelAsync)} for tag {consumerTag}");
            return Task.CompletedTask;
        }

        Task IAsyncBasicConsumer.HandleBasicCancelOkAsync(string consumerTag, CancellationToken cancellationToken)
        {
            ESLog.Info($"Unhandled {nameof(IAsyncBasicConsumer.HandleBasicCancelOkAsync)} for tag {consumerTag}");
            return Task.CompletedTask;
        }

        Task IAsyncBasicConsumer.HandleBasicConsumeOkAsync(string consumerTag, CancellationToken cancellationToken)
        {
            ESLog.Info($"Unhandled {nameof(IAsyncBasicConsumer.HandleBasicConsumeOkAsync)} for tag {consumerTag}");
            return Task.CompletedTask;
        }

        Task IAsyncBasicConsumer.HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey,
            IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
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
