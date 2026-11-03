using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Process-wide tracing configuration: the options that shape the spans this client produces,
    /// and the propagation used where a connection sets none.
    /// </summary>
    /// <remarks>
    /// Configured through the <c>AddRabbitMQInstrumentation</c> overload that takes no factory, in the
    /// <c>RabbitMQ.Client.OpenTelemetry</c> package, or the deprecated <see cref="RabbitMQActivitySource.TracingOptions"/>. A connection
    /// reads each member its factory's <see cref="ConnectionTracingOptions"/> leaves
    /// <see langword="null"/> from here, at each operation. A new instance carries the client's
    /// default propagation, so assigning one also replaces any propagation set before.
    /// </remarks>
    public class RabbitMQTracingOptions
    {
        private Action<Activity, IDictionary<string, object?>> _contextInjector = RabbitMQActivitySource.DefaultContextInjector;
        private Func<IReadOnlyBasicProperties, ActivityContext> _contextExtractor = RabbitMQActivitySource.DefaultContextExtractor;

        /// <summary>
        /// When <see langword="true"/> (the default), the destination is appended to publish,
        /// delivery and fetch span names, for example <c>publish my-exchange:my.routing.key</c>.
        /// Set it to <see langword="false"/> where a high-cardinality routing key or a server-named
        /// queue would make span names unusable as an aggregation key.
        /// <para>
        /// The name predates the behavior: before 7.3.0 only the routing key was appended, which
        /// left the exchange out of span names entirely. The appended value is now
        /// <c>messaging.destination.name</c>, of which the routing key is one component.
        /// </para>
        /// </summary>
        public bool UseRoutingKeyAsOperationName { get; set; } = true;

        /// <summary>
        /// When <see langword="true"/> (the default), a delivery span is parented to the trace
        /// context the publisher propagated in the message, so a publish and the deliveries it
        /// causes form a single trace. Fetch spans from <c>BasicGetAsync</c> are parented the same way.
        /// </summary>
        /// <remarks>
        /// Only parenting is affected. Whenever a context is successfully extracted from a message it
        /// is attached to the delivery or fetch span as an <see cref="System.Diagnostics.ActivityLink"/> as
        /// well, in both modes, so setting this to <see langword="false"/> does not replace a parent
        /// with a link - it drops the parent and leaves the link. Turn it off where the publisher and
        /// the consumer are better treated as separate traces, for example when one message fans out
        /// to many long-running consumers.
        /// </remarks>
        public bool UsePublisherAsParent { get; set; } = true;

        /// <summary>
        /// When <see langword="true"/>, publish, delivery and fetch spans carry
        /// <c>messaging.message.body.size</c>, and publish spans also carry
        /// <c>messaging.message.envelope.size</c>. Defaults to <see langword="false"/>.
        /// </summary>
        /// <remarks>
        /// The semantic convention marks both attributes Opt-In, which it defines as "instrumentations
        /// SHOULD populate the attribute if and only if the user configures the instrumentation to do
        /// so". They are cheap to compute here, so the only cost of turning them on is the bytes they
        /// add to every exported span.
        /// </remarks>
        public bool IncludeMessageSizes { get; set; }

        /// <summary>
        /// Injects the current <see cref="Activity"/> context into a published message's headers.
        /// Defaults to W3C trace-context propagation using
        /// <see cref="DistributedContextPropagator.Current"/>. Assigning <see langword="null"/>
        /// throws <see cref="ArgumentNullException"/>.
        /// </summary>
        internal Action<Activity, IDictionary<string, object?>> ContextInjector
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
        internal Func<IReadOnlyBasicProperties, ActivityContext> ContextExtractor
        {
            get => _contextExtractor;
            set => _contextExtractor = value ?? throw new ArgumentNullException(nameof(value));
        }
    }
}
