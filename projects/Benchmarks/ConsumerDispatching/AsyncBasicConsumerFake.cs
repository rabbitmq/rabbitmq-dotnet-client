using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Benchmarks
{
    internal sealed class AsyncBasicConsumerFake : IAsyncBasicConsumer
    {
        private readonly ManualResetEventSlim _autoResetEvent;
        private int _current;

        public int Count { get; set; }

        public AsyncBasicConsumerFake(ManualResetEventSlim autoResetEvent)
        {
            _autoResetEvent = autoResetEvent;
        }

        Task IAsyncBasicConsumer.HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey,
            ReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _current) == Count)
            {
                _current = 0;
                _autoResetEvent.Set();
            }

            return Task.CompletedTask;
        }

        Task IAsyncBasicConsumer.HandleBasicCancelAsync(string consumerTag, CancellationToken _) => Task.CompletedTask;

        Task IAsyncBasicConsumer.HandleBasicCancelOkAsync(string consumerTag, CancellationToken _) => Task.CompletedTask;

        Task IAsyncBasicConsumer.HandleBasicConsumeOkAsync(string consumerTag, CancellationToken _) => Task.CompletedTask;

        Task IAsyncBasicConsumer.HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason, CancellationToken _) => Task.CompletedTask;

        public IChannel Channel { get; }

        public event AsyncEventHandler<ConsumerEventArgs> ConsumerCancelled
        {
            add { }
            remove { }
        }
    }
}
