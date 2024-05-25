using System;
using System.Buffers;
using System.Collections.Concurrent;
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
        private static readonly FallbackConsumer s_fallbackConsumer = new FallbackConsumer();
        private readonly ConcurrentDictionary<ReadOnlyMemory<byte>, (IBasicConsumer, string)> _consumers;

        public IBasicConsumer? DefaultConsumer { get; set; }

        protected ConsumerDispatcherBase()
        {
            var comparer = new ReadOnlyMemoryOfByteEqualityComparer();
            _consumers = new ConcurrentDictionary<ReadOnlyMemory<byte>, (IBasicConsumer, string)>(comparer);
        }

        protected void AddConsumer(IBasicConsumer consumer, string tag)
        {
            byte[] tagBytes = Encoding.UTF8.GetBytes(tag);
            _consumers[tagBytes] = (consumer, tag);
        }

        protected (IBasicConsumer consumer, string consumerTag) GetConsumerOrDefault(ReadOnlyMemory<byte> tag)
        {
            if (_consumers.TryGetValue(tag, out (IBasicConsumer consumer, string consumerTag) consumerPair))
            {
                return consumerPair;
            }

#if NET6_0_OR_GREATER
            string consumerTag = Encoding.UTF8.GetString(tag.Span);
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

        public IBasicConsumer GetAndRemoveConsumer(string tag)
        {
            ArrayPool<byte> pool = ArrayPool<byte>.Shared;
            byte[]? buf = null;
            try
            {
                buf = pool.Rent(Encoding.UTF8.GetMaxByteCount(tag.Length));
#if NET6_0_OR_GREATER
                int count = Encoding.UTF8.GetBytes(tag, buf);
#else
                int count = Encoding.UTF8.GetBytes(tag, 0, tag.Length, buf, 0);
#endif
                Memory<byte> memory = buf.AsMemory(0, count);
                IBasicConsumer result = _consumers.Remove(memory,
                    out (IBasicConsumer consumer, string consumerTag) consumerPair) ?
                        consumerPair.consumer : GetDefaultOrFallbackConsumer();
                return result;
            }
            finally
            {
                if (buf != null)
                {
                    pool.Return(buf);
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
            foreach (KeyValuePair<ReadOnlyMemory<byte>, (IBasicConsumer consumer, string consumerTag)> pair in _consumers.ToArray())
            {
                ShutdownConsumer(pair.Value.consumer, reason);
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
