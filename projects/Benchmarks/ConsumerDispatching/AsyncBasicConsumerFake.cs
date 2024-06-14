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

        public Task HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey,
            ReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            if (Interlocked.Increment(ref _current) == Count)
            {
                _current = 0;
                _autoResetEvent.Set();
            }
            return Task.CompletedTask;
        }

        Task IBasicConsumer.HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey,
            ReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            if (Interlocked.Increment(ref _current) == Count)
            {
                _current = 0;
                _autoResetEvent.Set();
            }
            return Task.CompletedTask;
        }

        public Task HandleBasicCancel(string consumerTag) => Task.CompletedTask;

        public Task HandleBasicCancelOk(string consumerTag) => Task.CompletedTask;

        public Task HandleBasicConsumeOk(string consumerTag) => Task.CompletedTask;

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

        void IBasicConsumer.HandleBasicCancelOk(string consumerTag)
        {
        }

        void IBasicConsumer.HandleBasicConsumeOk(string consumerTag)
        {
        }

        void IBasicConsumer.HandleChannelShutdown(object channel, ShutdownEventArgs reason)
        {
        }

        void IBasicConsumer.HandleBasicCancel(string consumerTag)
        {
        }
    }
}
