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
        private const string ActivitySourceNamePattern = "RabbitMQ.Client.*";

        /// <summary>
        /// Configures <paramref name="connectionFactory"/> so that connections it creates propagate
        /// trace context with OpenTelemetry. Pair this with
        /// <see cref="AddRabbitMQInstrumentation(TracerProviderBuilder)"/> on the
        /// <see cref="TracerProviderBuilder"/> to observe the resulting spans.
        /// </summary>
        /// <remarks>
        /// This is the preferred way to configure propagation, because the configuration ends up owned
        /// by the connection that performs the traced operations rather than by process-wide state.
        /// Unlike the <see cref="TracerProviderBuilder"/> overloads it needs no
        /// <see cref="TracerProvider"/>, so it can be called wherever the factory is built - including
        /// inside a dependency-injection registration, where the factory instance does not yet exist at
        /// the point <c>WithTracing</c> configures the builder.
        /// <para>
        /// Installs the OpenTelemetry inject/extract delegates on the factory's
        /// <see cref="ConnectionFactory.TracingOptions"/>, replacing any custom propagation delegates
        /// already set (installing OpenTelemetry propagation is this method's purpose), while carrying
        /// over the factory's other tracing options. <paramref name="configure"/> then lets the caller
        /// adjust the result, including replacing the delegates again with its own. A fresh options
        /// instance is assigned to the factory, so any instance the caller already held is not mutated,
        /// and connections created by the factory after this call capture the configuration; connections
        /// created before it are unaffected.
        /// </para>
        /// </remarks>
        public static ConnectionFactory UseOpenTelemetryTracing(this ConnectionFactory connectionFactory,
            Action<RabbitMQTracingOptions> configure = null)
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

            return connectionFactory;
        }

        /// <summary>
        /// Calls <see cref="UseOpenTelemetryTracing"/> on <paramref name="connectionFactory"/> and
        /// subscribes this builder to the client's activity sources. A convenience shortcut for when
        /// both objects are on hand; where they are not, configure the factory and the builder
        /// separately.
        /// </summary>
        public static TracerProviderBuilder AddRabbitMQInstrumentation(this TracerProviderBuilder builder,
            ConnectionFactory connectionFactory, Action<RabbitMQTracingOptions> configure = null)
        {
            connectionFactory.UseOpenTelemetryTracing(configure);

            builder.AddSource(ActivitySourceNamePattern);
            return builder;
        }

        /// <summary>
        /// Subscribes this builder to the client's activity sources and installs the OpenTelemetry
        /// propagation delegates as the process-wide tracing default.
        /// </summary>
        /// <remarks>
        /// The process-wide default applies to every connection whose factory set no
        /// <see cref="ConnectionFactory.TracingOptions"/>; a factory that sets its own options - for
        /// example through <see cref="UseOpenTelemetryTracing"/> - overrides the default for the
        /// connections it creates.
        /// <para>
        /// Because this default is process-wide rather than per-<see cref="TracerProvider"/>, the last
        /// call wins when several providers configure it with different <paramref name="configure"/>
        /// actions, and disposing a provider does not restore the previous values. That is inherent
        /// rather than an implementation choice: one <see cref="System.Diagnostics.ActivitySource"/>
        /// produces a single <see cref="System.Diagnostics.Activity"/> shared by every provider, and one
        /// publish injects a single set of headers, so span shape and propagation cannot differ per
        /// provider. Configure the factory instead when the configuration needs an owner. See
        /// https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1981.
        /// </para>
        /// </remarks>
        public static TracerProviderBuilder AddRabbitMQInstrumentation(this TracerProviderBuilder builder, Action<RabbitMQTracingOptions> configure)
        {
            /*
             * The OpenTelemetry delegates are applied before `configure` runs, so a caller that sets
             * ContextInjector or ContextExtractor in `configure` replaces them - matching
             * UseOpenTelemetryTracing. Applying them afterwards would silently discard a custom
             * delegate, because assigning RabbitMQActivitySource.TracingOptions copies only the
             * span-shaping flags out of the instance.
             */
            var options = new RabbitMQTracingOptions
            {
                ContextInjector = OpenTelemetryContextInjector,
                ContextExtractor = OpenTelemetryContextExtractor
            };
            configure?.Invoke(options);

#pragma warning disable CS0618 // the statics are the process-wide default this overload exists to set
            RabbitMQActivitySource.TracingOptions = options;
            RabbitMQActivitySource.ContextInjector = options.ContextInjector;
            RabbitMQActivitySource.ContextExtractor = options.ContextExtractor;
#pragma warning restore CS0618

            builder.AddSource(ActivitySourceNamePattern);
            return builder;
        }

        /// <summary>
        /// Subscribes this builder to the client's activity sources and installs the OpenTelemetry
        /// propagation delegates as the process-wide tracing default, leaving the span-shaping options
        /// at their defaults. See
        /// <see cref="AddRabbitMQInstrumentation(TracerProviderBuilder, Action{RabbitMQTracingOptions})"/>
        /// for what "process-wide" means here.
        /// </summary>
        public static TracerProviderBuilder AddRabbitMQInstrumentation(this TracerProviderBuilder builder)
        {
            return AddRabbitMQInstrumentation(builder, (Action<RabbitMQTracingOptions>)null);
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
