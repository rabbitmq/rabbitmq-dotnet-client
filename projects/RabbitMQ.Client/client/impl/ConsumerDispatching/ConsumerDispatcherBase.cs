using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RabbitMQ.Client.ConsumerDispatching
{
    internal abstract class ConsumerDispatcherBase
    {
        private static readonly FallbackConsumer s_fallbackConsumer = new FallbackConsumer();
        private readonly ConcurrentDictionary<string, IBasicConsumer> _consumers = new ConcurrentDictionary<string, IBasicConsumer>();

        public IBasicConsumer? DefaultConsumer { get; set; }

        protected ConsumerDispatcherBase()
        {
        }

        protected void AddConsumer(IBasicConsumer consumer, string tag)
        {
            _consumers[tag] = consumer;
        }

        protected IBasicConsumer GetConsumerOrDefault(string tag)
        {
            return _consumers.TryGetValue(tag, out IBasicConsumer? consumer) ? consumer : GetDefaultOrFallbackConsumer();
        }

        public IBasicConsumer GetAndRemoveConsumer(string tag)
        {
            return _consumers.Remove(tag, out IBasicConsumer? consumer) ? consumer : GetDefaultOrFallbackConsumer();
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
            foreach (KeyValuePair<string, IBasicConsumer> pair in _consumers.ToArray())
            {
                ShutdownConsumer(pair.Value, reason);
            }
            _consumers.Clear();
        }

        protected abstract void ShutdownConsumer(IBasicConsumer consumer, ShutdownEventArgs reason);

        protected abstract void InternalShutdown();

        protected abstract Task InternalShutdownAsync();

        // Do not inline as it's not the default case on a hot path
        [MethodImpl(MethodImplOptions.NoInlining)]
        private IBasicConsumer GetDefaultOrFallbackConsumer()
        {
            return DefaultConsumer ?? s_fallbackConsumer;
        }
    }
}
