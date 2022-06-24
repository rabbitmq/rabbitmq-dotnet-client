using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Util;

namespace RabbitMQ.Client.ConsumerDispatching
{
#nullable enable
    internal abstract class ConsumerDispatcherBase
    {
        private static readonly FallbackConsumer fallbackConsumer = new FallbackConsumer();
        private readonly Dictionary<ReadOnlyMemory<byte>, (IBasicConsumer consumer, string consumerTag)> _consumers;

        public IBasicConsumer? DefaultConsumer { get; set; }

        protected ConsumerDispatcherBase()
        {
            _consumers = new Dictionary<ReadOnlyMemory<byte>, (IBasicConsumer, string)>(MemoryOfByteEqualityComparer.Instance);
        }

        protected void AddConsumer(IBasicConsumer consumer, string tag)
        {
            lock (_consumers)
            {
                var tagBytes = Encoding.UTF8.GetBytes(tag);
                _consumers[tagBytes] = (consumer, tag);
            }
        }

        protected (IBasicConsumer consumer, string consumerTag) GetConsumerOrDefault(ReadOnlyMemory<byte> tag)
        {
            lock (_consumers)
            {
                if (_consumers.TryGetValue(tag, out var consumerPair))
                {
                    return consumerPair;
                }

#if NETCOREAPP
                var consumerTag = Encoding.UTF8.GetString(tag.Span);
#else
                string consumerTag;
                unsafe
                {
                    fixed (byte* bytes = tag.Span)
                    {
                        consumerTag = Encoding.UTF8.GetString(bytes, tag.Length);
                    }
                }
#endif

                return (GetDefaultOrFallbackConsumer(), consumerTag);
            }
        }

        public IBasicConsumer GetAndRemoveConsumer(string tag)
        {
            lock (_consumers)
            {
                var utf8 = Encoding.UTF8;
#if NETCOREAPP
                var pool = ArrayPool<byte>.Shared;
                var buf = pool.Rent(utf8.GetMaxByteCount(tag.Length));
                int count = utf8.GetBytes(tag, buf);
                var memory = buf.AsMemory(0, count);
#else
                var memory = utf8.GetBytes(tag).AsMemory();
#endif
                var result = _consumers.Remove(memory, out var consumerPair) ? consumerPair.consumer : GetDefaultOrFallbackConsumer();
#if NETCOREAPP
                pool.Return(buf);
#endif
                return result;
            }
        }

        public Task ShutdownAsync(ShutdownEventArgs reason)
        {
            lock (_consumers)
            {
                foreach (KeyValuePair<ReadOnlyMemory<byte>, (IBasicConsumer consumer, string consumerTag)> pair in _consumers)
                {
                    ShutdownConsumer(pair.Value.consumer, reason);
                }
                _consumers.Clear();
            }

            return InternalShutdownAsync();
        }

        protected abstract void ShutdownConsumer(IBasicConsumer consumer, ShutdownEventArgs reason);

        protected abstract Task InternalShutdownAsync();

        // Do not inline as it's not the default case on a hot path
        [MethodImpl(MethodImplOptions.NoInlining)]
        private IBasicConsumer GetDefaultOrFallbackConsumer()
        {
            return DefaultConsumer ?? fallbackConsumer;
        }
    }
}
