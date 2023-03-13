using System;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Custom logic for handling topology recovery exceptions that match the specified filters.
    /// </summary>
    public class TopologyRecoveryExceptionHandler
    {
        private static readonly Func<IRecordedExchange, Exception, bool> s_defaultExchangeExceptionCondition = (e, ex) => true;
        private static readonly Func<IRecordedQueue, Exception, bool> s_defaultQueueExceptionCondition = (q, ex) => true;
        private static readonly Func<IRecordedBinding, Exception, bool> s_defaultBindingExceptionCondition = (b, ex) => true;
        private static readonly Func<IRecordedConsumer, Exception, bool> s_defaultConsumerExceptionCondition = (c, ex) => true;

        private Func<IRecordedExchange, Exception, bool> _exchangeRecoveryExceptionCondition;
        private Func<IRecordedQueue, Exception, bool> _queueRecoveryExceptionCondition;
        private Func<IRecordedBinding, Exception, bool> _bindingRecoveryExceptionCondition;
        private Func<IRecordedConsumer, Exception, bool> _consumerRecoveryExceptionCondition;
        private Action<IRecordedExchange, Exception, IConnection> _exchangeRecoveryExceptionHandler;
        private Action<IRecordedQueue, Exception, IConnection> _queueRecoveryExceptionHandler;
        private Action<IRecordedBinding, Exception, IConnection> _bindingRecoveryExceptionHandler;
        private Action<IRecordedConsumer, Exception, IConnection> _consumerRecoveryExceptionHandler;

        /// <summary>
        /// Decides which exchange recovery exceptions the custom exception handler is applied to.
        /// Default condition applies the exception handler to all exchange recovery exceptions.
        /// </summary>
        public Func<IRecordedExchange, Exception, bool> ExchangeRecoveryExceptionCondition
        {
            get => _exchangeRecoveryExceptionCondition ?? s_defaultExchangeExceptionCondition;

            set
            {
                if (_exchangeRecoveryExceptionCondition != null)
                    throw new InvalidOperationException($"Cannot modify {nameof(ExchangeRecoveryExceptionCondition)} after it has been initialized.");

                _exchangeRecoveryExceptionCondition = value ?? throw new ArgumentNullException(nameof(ExchangeRecoveryExceptionCondition));
            }
        }

        /// <summary>
        /// Decides which queue recovery exceptions the custom exception handler is applied to.
        /// Default condition applies the exception handler to all queue recovery exceptions.
        /// </summary>
        public Func<IRecordedQueue, Exception, bool> QueueRecoveryExceptionCondition
        {
            get => _queueRecoveryExceptionCondition ?? s_defaultQueueExceptionCondition;

            set
            {
                if (_queueRecoveryExceptionCondition != null)
                    throw new InvalidOperationException($"Cannot modify {nameof(QueueRecoveryExceptionCondition)} after it has been initialized.");

                _queueRecoveryExceptionCondition = value ?? throw new ArgumentNullException(nameof(QueueRecoveryExceptionCondition));
            }
        }

        /// <summary>
        /// Decides which binding recovery exceptions the custom exception handler is applied to.
        /// Default condition applies the exception handler to all binding recovery exceptions.
        /// </summary>
        public Func<IRecordedBinding, Exception, bool> BindingRecoveryExceptionCondition
        {
            get => _bindingRecoveryExceptionCondition ?? s_defaultBindingExceptionCondition;

            set
            {
                if (_bindingRecoveryExceptionCondition != null)
                    throw new InvalidOperationException($"Cannot modify {nameof(ExchangeRecoveryExceptionCondition)} after it has been initialized.");

                _bindingRecoveryExceptionCondition = value ?? throw new ArgumentNullException(nameof(ExchangeRecoveryExceptionCondition));
            }
        }

        /// <summary>
        /// Decides which consumer recovery exceptions the custom exception handler is applied to.
        /// Default condition applies the exception handler to all consumer recovery exceptions.
        /// </summary>
        public Func<IRecordedConsumer, Exception, bool> ConsumerRecoveryExceptionCondition
        {
            get => _consumerRecoveryExceptionCondition ?? s_defaultConsumerExceptionCondition;

            set
            {
                if (_consumerRecoveryExceptionCondition != null)
                    throw new InvalidOperationException($"Cannot modify {nameof(ConsumerRecoveryExceptionCondition)} after it has been initialized.");

                _consumerRecoveryExceptionCondition = value ?? throw new ArgumentNullException(nameof(ConsumerRecoveryExceptionCondition));
            }
        }

        /// <summary>
        /// Retries, or otherwise handles, an exception thrown when attempting to recover an exchange.
        /// </summary>
        public Action<IRecordedExchange, Exception, IConnection> ExchangeRecoveryExceptionHandler
        {
            get => _exchangeRecoveryExceptionHandler;

            set
            {
                if (_exchangeRecoveryExceptionHandler != null)
                    throw new InvalidOperationException($"Cannot modify {nameof(ExchangeRecoveryExceptionHandler)} after it has been initialized.");

                _exchangeRecoveryExceptionHandler = value ?? throw new ArgumentNullException(nameof(ExchangeRecoveryExceptionHandler));
            }
        }

        /// <summary>
        /// Retries, or otherwise handles, an exception thrown when attempting to recover a queue.
        /// </summary>
        public Action<IRecordedQueue, Exception, IConnection> QueueRecoveryExceptionHandler
        {
            get => _queueRecoveryExceptionHandler;

            set
            {
                if (_queueRecoveryExceptionHandler != null)
                    throw new InvalidOperationException($"Cannot modify {nameof(QueueRecoveryExceptionHandler)} after it has been initialized.");

                _queueRecoveryExceptionHandler = value ?? throw new ArgumentNullException(nameof(QueueRecoveryExceptionHandler));
            }
        }

        /// <summary>
        /// Retries, or otherwise handles, an exception thrown when attempting to recover a binding.
        /// </summary>
        public Action<IRecordedBinding, Exception, IConnection> BindingRecoveryExceptionHandler
        {
            get => _bindingRecoveryExceptionHandler;

            set
            {
                if (_bindingRecoveryExceptionHandler != null)
                    throw new InvalidOperationException($"Cannot modify {nameof(BindingRecoveryExceptionHandler)} after it has been initialized.");

                _bindingRecoveryExceptionHandler = value ?? throw new ArgumentNullException(nameof(BindingRecoveryExceptionHandler));
            }
        }

        /// <summary>
        /// Retries, or otherwise handles, an exception thrown when attempting to recover a consumer.
        /// Is only called when the exception did not cause the consumer's channel to close.
        /// </summary>
        public Action<IRecordedConsumer, Exception, IConnection> ConsumerRecoveryExceptionHandler
        {
            get => _consumerRecoveryExceptionHandler;

            set
            {
                if (_consumerRecoveryExceptionHandler != null)
                    throw new InvalidOperationException($"Cannot modify {nameof(ConsumerRecoveryExceptionHandler)} after it has been initialized.");

                _consumerRecoveryExceptionHandler = value ?? throw new ArgumentNullException(nameof(ConsumerRecoveryExceptionHandler));
            }
        }
    }
}
