using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;

namespace OpenTelemetry.Trace
{

    public static class OpenTelemetryExtensions
    {
        private const string ProcessWideObsoleteMessage =
            "This overload installs OpenTelemetry propagation on the deprecated process-wide tracing " +
            "statics on RabbitMQActivitySource, which are shared across every connection. Use " +
            "AddRabbitMQInstrumentation(TracerProviderBuilder, ConnectionFactory, Action<RabbitMQTracingOptions>), " +
            "which owns the configuration on the connection that performs the traced operations. " +
            "See https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1981.";

        /// <summary>
        /// Configures a <see cref="ConnectionFactory"/> so that connections it creates propagate
        /// trace context with OpenTelemetry, and subscribes this builder to the client's activity
        /// sources. This is the preferred path: the configuration is owned by the connection that
        /// performs the traced operations rather than by process-wide state.
        /// </summary>
        /// <remarks>
        /// Installs the OpenTelemetry inject/extract delegates on the factory's
        /// <see cref="ConnectionFactory.TracingOptions"/>, replacing any custom propagation delegates
        /// already set (installing OpenTelemetry propagation is this method's purpose), while carrying
        /// over the factory's other tracing options. <paramref name="configure"/> then lets the caller
        /// adjust the result, and the builder is subscribed to <c>RabbitMQ.Client.*</c>. A fresh options
        /// instance is assigned to the factory, so any instance the caller already held is not mutated,
        /// and connections created by the factory after this call capture the configuration; connections
        /// created before it are unaffected.
        /// </remarks>
        public static TracerProviderBuilder AddRabbitMQInstrumentation(this TracerProviderBuilder builder,
            ConnectionFactory connectionFactory, Action<RabbitMQTracingOptions> configure = null)
        {
            if (connectionFactory is null)
            {
                throw new ArgumentNullException(nameof(connectionFactory));
            }

            RabbitMQTracingOptions existing = connectionFactory.TracingOptions;
            var options = new RabbitMQTracingOptions
            {
                ContextInjector = OpenTelemetryContextInjector,
                ContextExtractor = OpenTelemetryContextExtractor
            };
            if (existing != null)
            {
                options.UseRoutingKeyAsOperationName = existing.UseRoutingKeyAsOperationName;
                options.UsePublisherAsParent = existing.UsePublisherAsParent;
            }
            configure?.Invoke(options);
            connectionFactory.TracingOptions = options;

            builder.AddSource("RabbitMQ.Client.*");
            return builder;
        }

        /// <summary>
        /// Subscribes this builder to the client's activity sources and installs the OpenTelemetry
        /// propagation delegates as the process-wide default.
        /// </summary>
        /// <remarks>
        /// This overload configures the deprecated process-wide statics on
        /// <see cref="RabbitMQActivitySource"/>, which every connection that has not been given its
        /// own <see cref="ConnectionFactory.TracingOptions"/> will capture. Prefer the overload that
        /// takes a <see cref="ConnectionFactory"/>, which owns the configuration on the connection
        /// itself. See https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1981.
        /// </remarks>
        [Obsolete(ProcessWideObsoleteMessage)]
        public static TracerProviderBuilder AddRabbitMQInstrumentation(this TracerProviderBuilder builder, Action<RabbitMQTracingOptions> configure)
        {
            var options = new RabbitMQTracingOptions();
            configure?.Invoke(options);

#pragma warning disable CS0618 // deprecated process-wide configuration, kept working for back-compat
            RabbitMQActivitySource.TracingOptions = options;
            RabbitMQActivitySource.ContextInjector = OpenTelemetryContextInjector;
            RabbitMQActivitySource.ContextExtractor = OpenTelemetryContextExtractor;
#pragma warning restore CS0618

            builder.AddSource("RabbitMQ.Client.*");
            return builder;
        }

        [Obsolete(ProcessWideObsoleteMessage)]
        public static TracerProviderBuilder AddRabbitMQInstrumentation(this TracerProviderBuilder builder)
        {
#pragma warning disable CS0618 // this overload is itself the deprecated process-wide path
            return AddRabbitMQInstrumentation(builder, (Action<RabbitMQTracingOptions>)null);
#pragma warning restore CS0618
        }

        private static ActivityContext OpenTelemetryContextExtractor(IReadOnlyBasicProperties props)
        {
            /*
             * A message with no headers at all has nothing to extract. Returning early
             * matters: without it the getter below is called once per propagator field
             * with a null carrier, and the correct result depends entirely on its
             * catch block swallowing a NullReferenceException. This mirrors the
             * null check in RabbitMQActivitySource.DefaultContextExtractor.
             *
             * Baggage.Current is reset first: it is AsyncLocal-backed and the consumer
             * dispatcher processes deliveries sequentially on one async flow, so a
             * header-less delivery must not inherit the previous message's baggage.
             * The non-early path below resets it via parentContext.Baggage; this branch
             * has to do it explicitly. See issue #1967.
             */
            if (props.Headers is null)
            {
                Baggage.Current = default;
                return default;
            }

            // Extract the PropagationContext of the upstream parent from the message headers.
            var parentContext = Propagators.DefaultTextMapPropagator.Extract(default, props.Headers, OpenTelemetryContextGetter);
            Baggage.Current = parentContext.Baggage;
            return parentContext.ActivityContext;
        }

        private static IEnumerable<string> OpenTelemetryContextGetter(IDictionary<string, object> carrier, string key)
        {
            /*
             * Defensive only. The caller null-checks Headers, and a malformed value is
             * handled by the `is byte[]` test rather than by throwing, so this catch is
             * no longer load-bearing for any known input. It stays because a custom
             * IDictionary implementation supplied through a header table could throw
             * from TryGetValue, and a failed context extraction must not fail the
             * delivery.
             */
            try
            {
                if (carrier != null && carrier.TryGetValue(key, out object value) && value is byte[] bytes)
                {
                    return new[] { Encoding.UTF8.GetString(bytes) };
                }
            }
            catch (Exception)
            {
                // Ignored: an unparseable carrier yields an unparented span, which is
                // strictly better than propagating the failure to the consumer.
            }

            return Enumerable.Empty<string>();
        }

        private static void OpenTelemetryContextInjector(Activity activity, IDictionary<string, object> props)
        {
            // Inject the current Activity's context into the message headers.
            Propagators.DefaultTextMapPropagator.Inject(new PropagationContext(activity.Context, Baggage.Current), props, OpenTelemetryContextSetter);
        }

        private static void OpenTelemetryContextSetter(IDictionary<string, object> carrier, string key, string value)
        {
            carrier[key] = Encoding.UTF8.GetBytes(value);
        }
    }
}
