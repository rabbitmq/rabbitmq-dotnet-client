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
        private readonly Dictionary<string, IBasicConsumer> _consumers = new Dictionary<string, IBasicConsumer>();

        public IBasicConsumer? DefaultConsumer { get; set; }

        protected ConsumerDispatcherBase()
        {
        }

        protected void AddConsumer(IBasicConsumer consumer, string tag)
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

        protected IBasicConsumer GetConsumerOrDefault(string tag)
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

        public IBasicConsumer GetAndRemoveConsumer(string tag)
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
                foreach (KeyValuePair<string, IBasicConsumer> pair in _consumers)
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

        protected abstract void ShutdownConsumer(IBasicConsumer consumer, ShutdownEventArgs reason);

        protected abstract Task InternalShutdownAsync(CancellationToken cancellationToken);

        // Do not inline as it's not the default case on a hot path
        [MethodImpl(MethodImplOptions.NoInlining)]
        private IBasicConsumer GetDefaultOrFallbackConsumer()
        {
            return DefaultConsumer ?? s_fallbackConsumer;
        }
    }
}
