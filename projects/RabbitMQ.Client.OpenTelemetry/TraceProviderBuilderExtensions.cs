using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.OpenTelemetry;

namespace OpenTelemetry.Trace
{
    public static class OpenTelemetryExtensions
    {
        internal static TextMapPropagator s_propagator = Propagators.DefaultTextMapPropagator;

        public static TracerProviderBuilder AddRabbitMQ(this TracerProviderBuilder builder,
            RabbitMQOpenTelemetryConfiguration configuration)
        {
            if (configuration.PropagateBaggage)
            {
                s_propagator = new CompositeTextMapPropagator(new TextMapPropagator[]
                {
                    new TraceContextPropagator(), new BaggagePropagator()
                });
            }
            else
            {
                s_propagator = new TraceContextPropagator();
            }

            RabbitMQActivitySource.UseRoutingKeyAsOperationName = configuration.UseRoutingKeyAsOperationName;
            RabbitMQActivitySource.ContextExtractor = OpenTelemetryContextExtractor;
            RabbitMQActivitySource.ContextInjector = OpenTelemetryContextInjector;

            if (configuration.IncludeSubscribers)
            {
                builder.AddSource(RabbitMQActivitySource.SubscriberSourceName);
            }

            if (configuration.IncludePublishers)
            {
                builder.AddSource(RabbitMQActivitySource.PublisherSourceName);
            }

            return builder;
        }

        private static ActivityContext OpenTelemetryContextExtractor(IReadOnlyBasicProperties props)
        {
            // Extract the PropagationContext of the upstream parent from the message headers.
            var parentContext = s_propagator.Extract(default, props.Headers, OpenTelemetryContextGetter);
            Baggage.Current = parentContext.Baggage;
            return parentContext.ActivityContext;
        }

        private static IEnumerable<string> OpenTelemetryContextGetter(IDictionary<string, object> carrier, string key)
        {
            try
            {
                if (carrier.TryGetValue(key, out object value))
                {
                    byte[] bytes = value as byte[];
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
            s_propagator.Inject(new PropagationContext(activity.Context, Baggage.Current), props, OpenTelemetryContextSetter);
        }

        private static void OpenTelemetryContextSetter(IDictionary<string, object> carrier, string key, string value)
        {
            carrier[key] = Encoding.UTF8.GetBytes(value);
        }
    }
}
