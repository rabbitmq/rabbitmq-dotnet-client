using System;
using System.Threading.Tasks;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Custom logic for handling topology recovery exceptions that match the specified filters.
    /// </summary>
    /// <remarks>
    /// A handler runs when recovering an entity fails and the matching condition accepts the
    /// exception, and returning normally does <b>not</b> unconditionally mean the failure is
    /// resolved. Once the handler has run, the failure is classified: a refusal the broker will
    /// simply give again, which in practice means only <c>precondition-failed</c>, is treated as
    /// final and recovery carries on, while a failure the broker may still act on, such as a
    /// timeout, a request that was never transmitted, or anything seen once the connection has
    /// gone, fails the recovery attempt so it is retried. Before this, a configured handler
    /// swallowed the exception outright and recovery reported success even when the entity was
    /// never recovered. See https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1995.
    /// <para>
    /// Two consequences for handler authors. A handler must be idempotent: because the attempt can
    /// be retried, it can be invoked again for the same entity, and there is no return value with
    /// which to report "handled, do not retry". Throwing from a handler still forces a retry
    /// directly, which is the long-standing way to ask for one explicitly.
    /// </para>
    /// </remarks>
    public class TopologyRecoveryExceptionHandler
    {
        private static readonly Func<IRecordedExchange, Exception, bool> s_defaultExchangeExceptionCondition = (e, ex) => true;
        private static readonly Func<IRecordedQueue, Exception, bool> s_defaultQueueExceptionCondition = (q, ex) => true;
        private static readonly Func<IRecordedBinding, Exception, bool> s_defaultBindingExceptionCondition = (b, ex) => true;
        private static readonly Func<IRecordedConsumer, Exception, bool> s_defaultConsumerExceptionCondition = (c, ex) => true;

        private Func<IRecordedExchange, Exception, bool>? _exchangeRecoveryExceptionCondition;
        private Func<IRecordedQueue, Exception, bool>? _queueRecoveryExceptionCondition;
        private Func<IRecordedBinding, Exception, bool>? _bindingRecoveryExceptionCondition;
        private Func<IRecordedConsumer, Exception, bool>? _consumerRecoveryExceptionCondition;
        private Func<IRecordedExchange, Exception, IConnection, Task>? _exchangeRecoveryExceptionHandlerAsync;
        private Func<IRecordedQueue, Exception, IConnection, Task>? _queueRecoveryExceptionHandlerAsync;
        private Func<IRecordedBinding, Exception, IConnection, Task>? _bindingRecoveryExceptionHandlerAsync;
        private Func<IRecordedConsumer, Exception, IConnection, Task>? _consumerRecoveryExceptionHandlerAsync;

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
                {
                    throw new InvalidOperationException($"Cannot modify {nameof(ExchangeRecoveryExceptionCondition)} after it has been initialized.");
                }

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
                {
                    throw new InvalidOperationException($"Cannot modify {nameof(QueueRecoveryExceptionCondition)} after it has been initialized.");
                }

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
                {
                    throw new InvalidOperationException($"Cannot modify {nameof(BindingRecoveryExceptionCondition)} after it has been initialized.");
                }

                _bindingRecoveryExceptionCondition = value ?? throw new ArgumentNullException(nameof(BindingRecoveryExceptionCondition));
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
                {
                    throw new InvalidOperationException($"Cannot modify {nameof(ConsumerRecoveryExceptionCondition)} after it has been initialized.");
                }

                _consumerRecoveryExceptionCondition = value ?? throw new ArgumentNullException(nameof(ConsumerRecoveryExceptionCondition));
            }
        }

        /// <summary>
        /// Retries, or otherwise handles, an exception thrown when attempting to recover an exchange.
        /// </summary>
        public Func<IRecordedExchange, Exception, IConnection, Task>? ExchangeRecoveryExceptionHandlerAsync
        {
            get => _exchangeRecoveryExceptionHandlerAsync;

            set
            {
                if (_exchangeRecoveryExceptionHandlerAsync != null)
                {
                    throw new InvalidOperationException($"Cannot modify {nameof(ExchangeRecoveryExceptionHandlerAsync)} after it has been initialized.");
                }

                _exchangeRecoveryExceptionHandlerAsync = value ?? throw new ArgumentNullException(nameof(ExchangeRecoveryExceptionHandlerAsync));
            }
        }

        /// <summary>
        /// Retries, or otherwise handles, an exception thrown when attempting to recover a queue.
        /// </summary>
        public Func<IRecordedQueue, Exception, IConnection, Task>? QueueRecoveryExceptionHandlerAsync
        {
            get => _queueRecoveryExceptionHandlerAsync;

            set
            {
                if (_queueRecoveryExceptionHandlerAsync != null)
                {
                    throw new InvalidOperationException($"Cannot modify {nameof(QueueRecoveryExceptionHandlerAsync)} after it has been initialized.");
                }

                _queueRecoveryExceptionHandlerAsync = value ?? throw new ArgumentNullException(nameof(QueueRecoveryExceptionHandlerAsync));
            }
        }

        /// <summary>
        /// Retries, or otherwise handles, an exception thrown when attempting to recover a binding.
        /// </summary>
        public Func<IRecordedBinding, Exception, IConnection, Task>? BindingRecoveryExceptionHandlerAsync
        {
            get => _bindingRecoveryExceptionHandlerAsync;

            set
            {
                if (_bindingRecoveryExceptionHandlerAsync != null)
                {
                    throw new InvalidOperationException($"Cannot modify {nameof(BindingRecoveryExceptionHandlerAsync)} after it has been initialized.");
                }

                _bindingRecoveryExceptionHandlerAsync = value ?? throw new ArgumentNullException(nameof(BindingRecoveryExceptionHandlerAsync));
            }
        }

        /// <summary>
        /// Retries, or otherwise handles, an exception thrown when attempting to recover a consumer.
        /// </summary>
        public Func<IRecordedConsumer, Exception, IConnection, Task>? ConsumerRecoveryExceptionHandlerAsync
        {
            get => _consumerRecoveryExceptionHandlerAsync;

            set
            {
                if (_consumerRecoveryExceptionHandlerAsync != null)
                {
                    throw new InvalidOperationException($"Cannot modify {nameof(ConsumerRecoveryExceptionHandlerAsync)} after it has been initialized.");
                }

                _consumerRecoveryExceptionHandlerAsync = value ?? throw new ArgumentNullException(nameof(ConsumerRecoveryExceptionHandlerAsync));
            }
        }
    }
}
