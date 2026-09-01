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
        public static TracerProviderBuilder AddRabbitMQInstrumentation(this TracerProviderBuilder builder, Action<RabbitMQTracingOptions> configure)
        {
            var options = new RabbitMQTracingOptions();
            configure?.Invoke(options);
            RabbitMQActivitySource.TracingOptions = options;

            RabbitMQActivitySource.ContextExtractor = OpenTelemetryContextExtractor;
            RabbitMQActivitySource.ContextInjector = OpenTelemetryContextInjector;
            builder.AddSource("RabbitMQ.Client.*");
            return builder;
        }

        public static TracerProviderBuilder AddRabbitMQInstrumentation(this TracerProviderBuilder builder)
        {
            return AddRabbitMQInstrumentation(builder, null);
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
