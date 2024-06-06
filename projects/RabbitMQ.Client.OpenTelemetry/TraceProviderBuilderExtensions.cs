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
        public static TracerProviderBuilder AddRabbitMQInstrumentation(this TracerProviderBuilder builder)
        {
            RabbitMQActivitySource.ContextExtractor = OpenTelemetryContextExtractor;
            RabbitMQActivitySource.ContextInjector = OpenTelemetryContextInjector;
            builder.AddSource("RabbitMQ.Client.*");
            return builder;
        }

        private static ActivityContext OpenTelemetryContextExtractor(IReadOnlyBasicProperties props)
        {
            // Extract the PropagationContext of the upstream parent from the message headers.
            var parentContext = Propagators.DefaultTextMapPropagator.Extract(default, props.Headers, OpenTelemetryContextGetter);
            Baggage.Current = parentContext.Baggage;
            return parentContext.ActivityContext;
        }

        private static IEnumerable<string> OpenTelemetryContextGetter(IDictionary<string, object> carrier, string key)
        {
            try
            {
                if (carrier.TryGetValue(key, out object value) && value is byte[] bytes)
                {
                    return new[] { Encoding.UTF8.GetString(bytes) };
                }
            }
            catch (Exception)
            {
                //this.logger.LogError(ex, "Failed to extract trace context.");
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
