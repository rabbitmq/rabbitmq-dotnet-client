using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client.ConsumerDispatching
{
#nullable enable
    internal abstract class ConsumerDispatcherBase
    {
        private static readonly FallbackConsumer s_fallbackConsumer = new FallbackConsumer();
        private readonly SemaphoreSlim _consumersSemaphore = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, IAsyncBasicConsumer> _consumers = new Dictionary<string, IAsyncBasicConsumer>();

        public IAsyncBasicConsumer? DefaultConsumer { get; set; }

        protected ConsumerDispatcherBase()
        {
        }

        protected void AddConsumer(IAsyncBasicConsumer consumer, string tag)
        {
            _consumersSemaphore.Wait();
            try
            {
                _consumers[tag] = consumer;
            }
            finally
            {
                _consumersSemaphore.Release();
            }
        }

        protected IAsyncBasicConsumer GetConsumerOrDefault(string tag)
        {
            _consumersSemaphore.Wait();
            try
            {
                return _consumers.TryGetValue(tag, out var consumer) ? consumer : GetDefaultOrFallbackConsumer();
            }
            finally
            {
                _consumersSemaphore.Release();
            }
        }

        public IAsyncBasicConsumer GetAndRemoveConsumer(string tag)
        {
            _consumersSemaphore.Wait();
            try
            {
                return _consumers.Remove(tag, out var consumer) ? consumer : GetDefaultOrFallbackConsumer();
            }
            finally
            {
                _consumersSemaphore.Release();
            }
        }

        public Task ShutdownAsync(ShutdownEventArgs reason, CancellationToken cancellationToken)
        {
            DoShutdownConsumers(reason);
            return InternalShutdownAsync(cancellationToken);
        }

        public virtual void Dispose() => _consumersSemaphore.Dispose();

        private void DoShutdownConsumers(ShutdownEventArgs reason)
        {
            _consumersSemaphore.Wait();
            try
            {
                foreach (KeyValuePair<string, IAsyncBasicConsumer> pair in _consumers)
                {
                    ShutdownConsumer(pair.Value, reason);
                }
                _consumers.Clear();
            }
            finally
            {
                _consumersSemaphore.Release();
            }
        }

        protected abstract void ShutdownConsumer(IAsyncBasicConsumer consumer, ShutdownEventArgs reason);

        protected abstract Task InternalShutdownAsync(CancellationToken cancellationToken);

        // Do not inline as it's not the default case on a hot path
        [MethodImpl(MethodImplOptions.NoInlining)]
        private IAsyncBasicConsumer GetDefaultOrFallbackConsumer()
        {
            return DefaultConsumer ?? s_fallbackConsumer;
        }
    }
}
