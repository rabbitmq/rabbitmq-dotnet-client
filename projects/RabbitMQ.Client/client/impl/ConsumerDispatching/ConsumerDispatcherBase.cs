using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RabbitMQ.Client.ConsumerDispatching
{
#nullable enable
    internal abstract class ConsumerDispatcherBase
    {
        private static readonly FallbackConsumer fallbackConsumer = new();
        private readonly Dictionary<string, IBasicConsumer> _consumers = new();

        public IBasicConsumer? DefaultConsumer { get; set; }

        protected ConsumerDispatcherBase()
        {
        }

        protected void AddConsumer(IBasicConsumer consumer, string tag)
        {
            lock (_consumers)
            {
                _consumers[tag] = consumer;
            }
        }

        protected IBasicConsumer GetConsumerOrDefault(string tag)
        {
            lock (_consumers)
            {
                return _consumers.TryGetValue(tag, out var consumer) ? consumer : GetDefaultOrFallbackConsumer();
            }
        }

        public IBasicConsumer GetAndRemoveConsumer(string tag)
        {
            lock (_consumers)
            {
                return _consumers.Remove(tag, out var consumer) ? consumer : GetDefaultOrFallbackConsumer();
            }
        }

        public void Shutdown(ShutdownEventArgs reason)
        {
            DoShutdownConsumers(reason);
            InternalShutdown();
        }

        public Task ShutdownAsync(ShutdownEventArgs reason)
        {
            DoShutdownConsumers(reason);
            return InternalShutdownAsync();
        }

        private void DoShutdownConsumers(ShutdownEventArgs reason)
        {
            lock (_consumers)
            {
                foreach (KeyValuePair<string, IBasicConsumer> pair in _consumers)
                {
                    ShutdownConsumer(pair.Value, reason);
                }
                _consumers.Clear();
            }
        }

        protected abstract void ShutdownConsumer(IBasicConsumer consumer, ShutdownEventArgs reason);

        protected abstract void InternalShutdown();

        protected abstract Task InternalShutdownAsync();

        // Do not inline as it's not the default case on a hot path
        [MethodImpl(MethodImplOptions.NoInlining)]
        private IBasicConsumer GetDefaultOrFallbackConsumer()
        {
            return DefaultConsumer ?? fallbackConsumer;
        }
    }
}
