using System;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Filter to know which entities (exchanges, queues, bindings, consumers) should be recovered by topology recovery.
    /// By default, allows all entities to be recovered.
    /// </summary>
    public class TopologyRecoveryFilter
    {
        private Func<IRecordedExchange, bool> _exchangeFilter = exchange => true;
        private Func<IRecordedQueue, bool> _queueFilter = queue => true;
        private Func<IRecordedBinding, bool> _bindingFilter = binding => true;
        private Func<IRecordedConsumer, bool> _consumerFilter = consumer => true;

        /// <summary>
        /// Decides whether an exchange is recovered or not.
        /// </summary>
        public Func<IRecordedExchange, bool> ExchangeFilter
        {
            get => _exchangeFilter;

            init
            {
                _exchangeFilter = value ?? throw new ArgumentNullException(nameof(ExchangeFilter));
            }
        }

        /// <summary>
        /// Decides whether a queue is recovered or not.
        /// </summary>
        public Func<IRecordedQueue, bool> QueueFilter
        {
            get => _queueFilter;

            init
            {
                _queueFilter = value ?? throw new ArgumentNullException(nameof(QueueFilter));
            }
        }

        /// <summary>
        /// Decides whether a binding is recovered or not.
        /// </summary>
        public Func<IRecordedBinding, bool> BindingFilter
        {
            get => _bindingFilter;

            init
            {
                _bindingFilter = value ?? throw new ArgumentNullException(nameof(BindingFilter));
            }
        }

        /// <summary>
        /// Decides whether a consumer is recovered or not.
        /// </summary>
        public Func<IRecordedConsumer, bool> ConsumerFilter
        {
            get => _consumerFilter;

            init
            {
                _consumerFilter = value ?? throw new ArgumentNullException(nameof(ConsumerFilter));
            }
        }
    }
}
