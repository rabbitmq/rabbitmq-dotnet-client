using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Tracing configuration for a connection: the span-shaping options and the delegates that
    /// propagate trace context in and out of message headers.
    /// </summary>
    /// <remarks>
    /// Set this on <see cref="ConnectionFactory.TracingOptions"/> to own the configuration on the
    /// connection that actually performs the traced operations, rather than through the process-wide
    /// statics on <see cref="RabbitMQActivitySource"/>. A connection captures the options in force
    /// when it is created, so later changes to the factory do not affect connections already open.
    /// </remarks>
    public class RabbitMQTracingOptions
    {
        private Action<Activity, IDictionary<string, object?>> _contextInjector = RabbitMQActivitySource.DefaultContextInjector;
        private Func<IReadOnlyBasicProperties, ActivityContext> _contextExtractor = RabbitMQActivitySource.DefaultContextExtractor;

        /// <summary>
        /// When <see langword="true"/> (the default), the routing key is appended to publish and
        /// delivery span names, for example <c>publish my.routing.key</c>.
        /// </summary>
        public bool UseRoutingKeyAsOperationName { get; set; } = true;

        /// <summary>
        /// When <see langword="true"/> (the default), a delivery span is parented to the publisher's
        /// propagated context; otherwise that context is attached as a link instead.
        /// </summary>
        public bool UsePublisherAsParent { get; set; } = true;

        /// <summary>
        /// Injects the current <see cref="Activity"/> context into a published message's headers.
        /// Assigning <see langword="null"/> throws <see cref="ArgumentNullException"/>.
        /// </summary>
        public Action<Activity, IDictionary<string, object?>> ContextInjector
        {
            get => _contextInjector;
            set => _contextInjector = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Extracts the upstream <see cref="ActivityContext"/> from a received message's properties.
        /// Assigning <see langword="null"/> throws <see cref="ArgumentNullException"/>.
        /// </summary>
        public Func<IReadOnlyBasicProperties, ActivityContext> ContextExtractor
        {
            get => _contextExtractor;
            set => _contextExtractor = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Returns an independent copy, so a connection can capture the options in force at its
        /// creation without being affected by later changes to the source.
        /// </summary>
        internal RabbitMQTracingOptions Clone()
        {
            return new RabbitMQTracingOptions
            {
                UseRoutingKeyAsOperationName = UseRoutingKeyAsOperationName,
                UsePublisherAsParent = UsePublisherAsParent,
                // Assign through the properties, not the fields, so the copy keeps the non-null
                // guarantee even if a future path could make the source fields null.
                ContextInjector = _contextInjector,
                ContextExtractor = _contextExtractor
            };
        }
    }
}
