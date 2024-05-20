using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Benchmarks
{
    internal sealed class AsyncBasicConsumerFake : IAsyncBasicConsumer, IBasicConsumer
    {
        private readonly ManualResetEventSlim _autoResetEvent;
        private int _current;

        public int Count { get; set; }

        public AsyncBasicConsumerFake(ManualResetEventSlim autoResetEvent)
        {
            _autoResetEvent = autoResetEvent;
        }

        public Task HandleBasicDeliver(ConsumerTag consumerTag, ulong deliveryTag, bool redelivered,
            ExchangeName exchange, RoutingKey routingKey,
            in ReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            if (Interlocked.Increment(ref _current) == Count)
            {
                _current = 0;
                _autoResetEvent.Set();
            }
            return Task.CompletedTask;
        }

        Task IBasicConsumer.HandleBasicDeliverAsync(ConsumerTag consumerTag, ulong deliveryTag, bool redelivered,
            ExchangeName exchange, RoutingKey routingKey,
            ReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            if (Interlocked.Increment(ref _current) == Count)
            {
                _current = 0;
                _autoResetEvent.Set();
            }
            return Task.CompletedTask;
        }

        public Task HandleBasicCancel(ConsumerTag consumerTag) => Task.CompletedTask;

        public Task HandleBasicCancelOk(ConsumerTag consumerTag) => Task.CompletedTask;

        public Task HandleBasicConsumeOk(ConsumerTag consumerTag) => Task.CompletedTask;

        public Task HandleChannelShutdown(object channel, ShutdownEventArgs reason) => Task.CompletedTask;

        public IChannel Channel { get; }

        event EventHandler<ConsumerEventArgs> IBasicConsumer.ConsumerCancelled
        {
            add { }
            remove { }
        }

        public event AsyncEventHandler<ConsumerEventArgs> ConsumerCancelled
        {
            add { }
            remove { }
        }

        void IBasicConsumer.HandleBasicCancelOk(ConsumerTag consumerTag)
        {
        }

        void IBasicConsumer.HandleBasicConsumeOk(ConsumerTag consumerTag)
        {
        }

        void IBasicConsumer.HandleChannelShutdown(object channel, ShutdownEventArgs reason)
        {
        }

        void IBasicConsumer.HandleBasicCancel(ConsumerTag consumerTag)
        {
        }
    }
}
