using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Benchmarks
{
    public sealed class AsyncBasicConsumerFake : IAsyncBasicConsumer
    {
        private readonly ManualResetEventSlim _autoResetEvent;
        private int _current;

        public int Count { get; set; }

        public AsyncBasicConsumerFake(ManualResetEventSlim autoResetEvent)
        {
            _autoResetEvent = autoResetEvent;
        }

        public Task HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey,
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

        public Task HandleBasicCancelAsync(string consumerTag) => Task.CompletedTask;

        public Task HandleBasicCancelOkAsync(string consumerTag) => Task.CompletedTask;

        public Task HandleBasicConsumeOkAsync(string consumerTag) => Task.CompletedTask;

        public Task HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason) => Task.CompletedTask;

        public IChannel Channel { get; }

        public event AsyncEventHandler<ConsumerEventArgs> ConsumerCancelled
        {
            add { }
            remove { }
        }
    }
}
