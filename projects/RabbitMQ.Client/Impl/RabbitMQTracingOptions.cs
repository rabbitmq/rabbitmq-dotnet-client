using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Tracing configuration: the options that shape the spans this client produces, and the
    /// delegates that propagate trace context in and out of message headers.
    /// </summary>
    /// <remarks>
    /// Set this on <see cref="ConnectionFactory.TracingOptions"/> to own the configuration on the
    /// connection that performs the traced operations. A connection captures the options in force
    /// when it is created, so later changes to the factory do not affect connections already open.
    /// Assigned to <see cref="RabbitMQActivitySource.TracingOptions"/> instead, the same type serves
    /// as the process-wide default used by every connection whose factory set none.
    /// </remarks>
    public class RabbitMQTracingOptions
    {
        private Action<Activity, IDictionary<string, object?>> _contextInjector = RabbitMQActivitySource.DefaultContextInjector;
        private Func<IReadOnlyBasicProperties, ActivityContext> _contextExtractor = RabbitMQActivitySource.DefaultContextExtractor;

        /// <summary>
        /// When <see langword="true"/> (the default), the routing key is appended to publish and
        /// delivery span names, for example <c>publish my.routing.key</c>. Set it to
        /// <see langword="false"/> where a high-cardinality routing key would make span names
        /// unusable as an aggregation key.
        /// </summary>
        public bool UseRoutingKeyAsOperationName { get; set; } = true;

        /// <summary>
        /// When <see langword="true"/> (the default), a delivery span is parented to the trace
        /// context the publisher propagated in the message, so a publish and the deliveries it
        /// causes form a single trace.
        /// </summary>
        /// <remarks>
        /// Only parenting is affected. Whenever a context is successfully extracted from a message it
        /// is attached to the delivery span as an <see cref="System.Diagnostics.ActivityLink"/> as
        /// well, in both modes, so setting this to <see langword="false"/> does not replace a parent
        /// with a link - it drops the parent and leaves the link. Turn it off where the publisher and
        /// the consumer are better treated as separate traces, for example when one message fans out
        /// to many long-running consumers.
        /// </remarks>
        public bool UsePublisherAsParent { get; set; } = true;

        /// <summary>
        /// Injects the current <see cref="Activity"/> context into a published message's headers.
        /// Defaults to W3C trace-context propagation using
        /// <see cref="DistributedContextPropagator.Current"/>. Assigning <see langword="null"/>
        /// throws <see cref="ArgumentNullException"/>.
        /// </summary>
        public Action<Activity, IDictionary<string, object?>> ContextInjector
        {
            get => _contextInjector;
            set => _contextInjector = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Extracts the upstream <see cref="ActivityContext"/> from a received message's properties.
        /// Defaults to W3C trace-context propagation using
        /// <see cref="DistributedContextPropagator.Current"/>. Assigning <see langword="null"/>
        /// throws <see cref="ArgumentNullException"/>.
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
