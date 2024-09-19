using System;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Filter to know which entities (exchanges, queues, bindings, consumers) should be recovered by topology recovery.
    /// By default, allows all entities to be recovered.
    /// </summary>
    public class TopologyRecoveryFilter
    {
        private static readonly Func<IRecordedExchange, bool> s_defaultExchangeFilter = exchange => true;
        private static readonly Func<IRecordedQueue, bool> s_defaultQueueFilter = queue => true;
        private static readonly Func<IRecordedBinding, bool> s_defaultBindingFilter = binding => true;
        private static readonly Func<IRecordedConsumer, bool> s_defaultConsumerFilter = consumer => true;

        private Func<IRecordedExchange, bool>? _exchangeFilter;
        private Func<IRecordedQueue, bool>? _queueFilter;
        private Func<IRecordedBinding, bool>? _bindingFilter;
        private Func<IRecordedConsumer, bool>? _consumerFilter;

        /// <summary>
        /// Decides whether an exchange is recovered or not.
        /// </summary>
        public Func<IRecordedExchange, bool> ExchangeFilter
        {
            get => _exchangeFilter ?? s_defaultExchangeFilter;

            set
            {
                if (_exchangeFilter != null)
                    throw new InvalidOperationException($"Cannot modify {nameof(ExchangeFilter)} after it has been initialized.");

                _exchangeFilter = value ?? throw new ArgumentNullException(nameof(ExchangeFilter));
            }
        }

        /// <summary>
        /// Decides whether a queue is recovered or not.
        /// </summary>
        public Func<IRecordedQueue, bool> QueueFilter
        {
            get => _queueFilter ?? s_defaultQueueFilter;

            set
            {
                if (_queueFilter != null)
                    throw new InvalidOperationException($"Cannot modify {nameof(QueueFilter)} after it has been initialized.");

                _queueFilter = value ?? throw new ArgumentNullException(nameof(QueueFilter));
            }
        }

        /// <summary>
        /// Decides whether a binding is recovered or not.
        /// </summary>
        public Func<IRecordedBinding, bool> BindingFilter
        {
            get => _bindingFilter ?? s_defaultBindingFilter;

            set
            {
                if (_bindingFilter != null)
                    throw new InvalidOperationException($"Cannot modify {nameof(BindingFilter)} after it has been initialized.");

                _bindingFilter = value ?? throw new ArgumentNullException(nameof(BindingFilter));
            }
        }

        /// <summary>
        /// Decides whether a consumer is recovered or not.
        /// </summary>
        public Func<IRecordedConsumer, bool> ConsumerFilter
        {
            get => _consumerFilter ?? s_defaultConsumerFilter;

            set
            {
                if (_consumerFilter != null)
                    throw new InvalidOperationException($"Cannot modify {nameof(ConsumerFilter)} after it has been initialized.");

                _consumerFilter = value ?? throw new ArgumentNullException(nameof(ConsumerFilter));
            }
        }
    }
}
