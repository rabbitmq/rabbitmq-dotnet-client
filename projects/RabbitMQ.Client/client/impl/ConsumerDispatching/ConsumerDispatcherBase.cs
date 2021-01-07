using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabbitMQ.Client.client.impl.ConsumerDispatching
{
    internal abstract class ConsumerDispatcherBase
    {
        private readonly Dictionary<string, IBasicConsumer> _consumers;

        public IBasicConsumer DefaultConsumer { get; set; }

        protected ConsumerDispatcherBase()
        {
            _consumers = new Dictionary<string, IBasicConsumer>();
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
                return _consumers.TryGetValue(tag, out var consumer) ? consumer : GetDefaultConsumerOrThrow();
            }
        }

        public IBasicConsumer GetAndRemoveConsumer(string tag)
        {
            lock (_consumers)
            {
#if NETSTANDARD
                var consumer = _consumers[tag];
                _consumers.Remove(tag);
                return consumer;
#else
                _consumers.Remove(tag, out var consumer);
                return consumer;
#endif
            }
        }

        public Task ShutdownAsync(ShutdownEventArgs reason)
        {
            lock (_consumers)
            {
                foreach (KeyValuePair<string, IBasicConsumer> pair in _consumers)
                {
                    ShutdownConsumer(pair.Value, reason);
                }
                _consumers.Clear();
            }

            return InternalShutdownAsync();
        }

        protected abstract void ShutdownConsumer(IBasicConsumer consumer, ShutdownEventArgs reason);

        protected abstract Task InternalShutdownAsync();

        private IBasicConsumer GetDefaultConsumerOrThrow()
        {
            return DefaultConsumer ?? throw new InvalidOperationException("Unsolicited delivery - see IModel.DefaultConsumer to handle this case.");
        }
    }
}
