using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using RabbitMQ.Util;

namespace RabbitMQ.Client.ConsumerDispatching
{
#nullable enable
    internal abstract class ConsumerDispatcherBase
    {
        private static readonly FallbackConsumer s_fallbackConsumer = new FallbackConsumer();
        private readonly Dictionary<ReadOnlyMemory<byte>, (IBasicConsumer consumer, ConsumerTag consumerTag)> _consumers;

        public IBasicConsumer? DefaultConsumer { get; set; }

        protected ConsumerDispatcherBase()
        {
            _consumers = new Dictionary<ReadOnlyMemory<byte>, (IBasicConsumer, ConsumerTag)>(MemoryOfByteEqualityComparer.Instance);
        }

        protected void AddConsumer(IBasicConsumer consumer, ConsumerTag consumerTag)
        {
            lock (_consumers)
            {
                var mem = (ReadOnlyMemory<byte>)consumerTag;
                _consumers[mem] = (consumer, consumerTag);
            }
        }

        protected (IBasicConsumer consumer, ConsumerTag consumerTag) GetConsumerOrDefault(ReadOnlyMemory<byte> tag)
        {
            lock (_consumers)
            {
                if (_consumers.TryGetValue(tag, out (IBasicConsumer consumer, ConsumerTag consumerTag) consumerPair))
                {
                    return consumerPair;
                }

                return (GetDefaultOrFallbackConsumer(), new ConsumerTag(tag));
            }
        }

        public IBasicConsumer GetAndRemoveConsumer(ConsumerTag tag)
        {
            lock (_consumers)
            {
                var tagMem = (ReadOnlyMemory<byte>)tag;
                if (_consumers.Remove(tagMem, out (IBasicConsumer consumer, ConsumerTag consumerTag) consumerPair))
                {
                    return consumerPair.consumer;
                }
                else
                {
                    return GetDefaultOrFallbackConsumer();
                }
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
                foreach (KeyValuePair<ReadOnlyMemory<byte>, (IBasicConsumer consumer, ConsumerTag consumerTag)> pair in _consumers)
                {
                    ShutdownConsumer(pair.Value.consumer, reason);
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
            return DefaultConsumer ?? s_fallbackConsumer;
        }
    }
}
