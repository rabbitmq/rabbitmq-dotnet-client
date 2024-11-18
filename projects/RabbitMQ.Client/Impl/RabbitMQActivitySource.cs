using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client
{
    public static class RabbitMQActivitySource
    {
        // These constants are defined in the OpenTelemetry specification:
        // https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/#messaging-attributes
        internal const string MessageId = "messaging.message.id";
        internal const string MessageConversationId = "messaging.message.conversation_id";
        internal const string MessagingOperationName = "messaging.operation.name";
        internal const string MessagingOperationNameBasicDeliver = "deliver";
        internal const string MessagingOperationNameBasicGet = "fetch";
        internal const string MessagingOperationNameBasicGetEmpty = "fetch (empty)";
        internal const string MessagingOperationNameBasicPublish = "publish";
        internal const string MessagingOperationType = "messaging.operation.type";
        internal const string MessagingOperationTypeSend = "send";
        internal const string MessagingOperationTypeProcess = "process";
        internal const string MessagingOperationTypeReceive = "receive";
        internal const string MessagingSystem = "messaging.system";
        internal const string MessagingDestination = "messaging.destination.name";
        internal const string MessagingDestinationRoutingKey = "messaging.rabbitmq.destination.routing_key";
        internal const string MessagingBodySize = "messaging.message.body.size";
        internal const string MessagingEnvelopeSize = "messaging.message.envelope.size";
        internal const string ProtocolName = "network.protocol.name";
        internal const string ProtocolVersion = "network.protocol.version";
        internal const string RabbitMQDeliveryTag = "messaging.rabbitmq.delivery_tag";

        private static readonly string AssemblyVersion = typeof(RabbitMQActivitySource).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "";

        private static readonly ActivitySource s_publisherSource =
            new ActivitySource(PublisherSourceName, AssemblyVersion);

        private static readonly ActivitySource s_subscriberSource =
            new ActivitySource(SubscriberSourceName, AssemblyVersion);

        public const string PublisherSourceName = "RabbitMQ.Client.Publisher";
        public const string SubscriberSourceName = "RabbitMQ.Client.Subscriber";

        public static Action<Activity, IDictionary<string, object?>> ContextInjector { get; set; } = DefaultContextInjector;

        public static Func<IReadOnlyBasicProperties, ActivityContext> ContextExtractor { get; set; } =
            DefaultContextExtractor;

        public static bool UseRoutingKeyAsOperationName { get; set; } = true;
        internal static bool PublisherHasListeners => s_publisherSource.HasListeners();

        internal static readonly IEnumerable<KeyValuePair<string, object?>> CreationTags = new[]
        {
            new KeyValuePair<string, object?>(MessagingSystem, "rabbitmq"),
            new KeyValuePair<string, object?>(ProtocolName, "amqp"),
            new KeyValuePair<string, object?>(ProtocolVersion, "0.9.1")
        };

        internal static Activity? BasicPublish(string routingKey, string exchange, int bodySize,
            ActivityContext linkedContext = default)
        {
            if (!s_publisherSource.HasListeners())
            {
                return null;
            }

            Activity? activity = linkedContext == default
                ? s_publisherSource.StartRabbitMQActivity(
                    UseRoutingKeyAsOperationName ? $"{MessagingOperationNameBasicPublish} {routingKey}" : MessagingOperationNameBasicPublish,
                    ActivityKind.Producer)
                : s_publisherSource.StartLinkedRabbitMQActivity(
                    UseRoutingKeyAsOperationName ? $"{MessagingOperationNameBasicPublish} {routingKey}" : MessagingOperationNameBasicPublish,
                    ActivityKind.Producer, linkedContext);
            if (activity != null && activity.IsAllDataRequested)
            {
                PopulateMessagingTags(MessagingOperationTypeSend, MessagingOperationNameBasicPublish, routingKey, exchange, 0, bodySize, activity);
            }

            return activity;

        }

        internal static Activity? BasicGetEmpty(string queue)
        {
            if (!s_subscriberSource.HasListeners())
            {
                return null;
            }

            Activity? activity = s_subscriberSource.StartRabbitMQActivity(
                UseRoutingKeyAsOperationName ? $"{MessagingOperationNameBasicGetEmpty} {queue}" : MessagingOperationNameBasicGetEmpty,
                ActivityKind.Consumer);
            if (activity != null && activity.IsAllDataRequested)
            {
                activity
                    .SetTag(MessagingOperationType, MessagingOperationTypeReceive)
                    .SetTag(MessagingOperationName, MessagingOperationNameBasicGetEmpty)
                    .SetTag(MessagingDestination, "amq.default");
            }

            return activity;
        }

        internal static Activity? BasicGet(string routingKey, string exchange, ulong deliveryTag,
            IReadOnlyBasicProperties readOnlyBasicProperties, int bodySize)
        {
            if (!s_subscriberSource.HasListeners())
            {
                return null;
            }

            // Extract the PropagationContext of the upstream parent from the message headers.
            Activity? activity = s_subscriberSource.StartLinkedRabbitMQActivity(
                UseRoutingKeyAsOperationName ? $"{MessagingOperationNameBasicGet} {routingKey}" : MessagingOperationNameBasicGet, ActivityKind.Consumer,
                ContextExtractor(readOnlyBasicProperties));
            if (activity != null && activity.IsAllDataRequested)
            {
                PopulateMessagingTags(MessagingOperationTypeReceive, MessagingOperationNameBasicGet, routingKey, exchange, deliveryTag, readOnlyBasicProperties,
                    bodySize, activity);
            }

            return activity;
        }

        internal static Activity? Deliver(string routingKey, string exchange, ulong deliveryTag,
            IReadOnlyBasicProperties basicProperties, int bodySize)
        {
            if (!s_subscriberSource.HasListeners())
            {
                return null;
            }

            // Extract the PropagationContext of the upstream parent from the message headers.
            Activity? activity = s_subscriberSource.StartLinkedRabbitMQActivity(
                UseRoutingKeyAsOperationName ? $"{MessagingOperationNameBasicDeliver} {routingKey}" : MessagingOperationNameBasicDeliver,
                ActivityKind.Consumer, ContextExtractor(basicProperties));
            if (activity != null && activity.IsAllDataRequested)
            {
                PopulateMessagingTags(MessagingOperationTypeProcess, MessagingOperationNameBasicDeliver, routingKey, exchange,
                    deliveryTag, basicProperties, bodySize, activity);
            }

            return activity;
        }

        private static Activity? StartRabbitMQActivity(this ActivitySource source, string name, ActivityKind kind,
            ActivityContext parentContext = default)
        {
            return source.CreateActivity(name, kind, parentContext, idFormat: ActivityIdFormat.W3C, tags: CreationTags)?.Start();
        }

        private static Activity? StartLinkedRabbitMQActivity(this ActivitySource source, string name, ActivityKind kind,
            ActivityContext linkedContext = default, ActivityContext parentContext = default)
        {
            return source.CreateActivity(name, kind, parentContext: parentContext,
                    links: new[] { new ActivityLink(linkedContext) }, idFormat: ActivityIdFormat.W3C,
                    tags: CreationTags)
                ?.Start();
        }

        private static void PopulateMessagingTags(string operationType, string operationName, string routingKey, string exchange,
            ulong deliveryTag, IReadOnlyBasicProperties readOnlyBasicProperties, int bodySize, Activity activity)
        {
            PopulateMessagingTags(operationType, operationName, routingKey, exchange, deliveryTag, bodySize, activity);

            if (!string.IsNullOrEmpty(readOnlyBasicProperties.CorrelationId))
            {
                activity.SetTag(MessageConversationId, readOnlyBasicProperties.CorrelationId);
            }

            if (!string.IsNullOrEmpty(readOnlyBasicProperties.MessageId))
            {
                activity.SetTag(MessageId, readOnlyBasicProperties.MessageId);
            }
        }

        private static void PopulateMessagingTags(string operationType, string operationName, string routingKey, string exchange,
            ulong deliveryTag, int bodySize, Activity activity)
        {
            activity
                .SetTag(MessagingOperationType, operationType)
                .SetTag(MessagingOperationName, operationName)
                .SetTag(MessagingDestination, string.IsNullOrEmpty(exchange) ? "amq.default" : exchange)
                .SetTag(MessagingDestinationRoutingKey, routingKey)
                .SetTag(MessagingBodySize, bodySize);

            if (deliveryTag > 0)
            {
                activity.SetTag(RabbitMQDeliveryTag, deliveryTag);
            }
        }

        internal static void PopulateMessageEnvelopeSize(Activity? activity, int size)
        {
            if (activity != null && activity.IsAllDataRequested && PublisherHasListeners)
            {
                activity.SetTag(MessagingEnvelopeSize, size);
            }
        }

        internal static void SetNetworkTags(this Activity? activity, IFrameHandler frameHandler)
        {
            if (PublisherHasListeners && activity != null && activity.IsAllDataRequested)
            {
                switch (frameHandler.RemoteEndPoint.AddressFamily)
                {
                    case AddressFamily.InterNetworkV6:
                        activity.SetTag("network.type", "ipv6");
                        break;
                    case AddressFamily.InterNetwork:
                        activity.SetTag("network.type", "ipv4");
                        break;
                }

                if (!string.IsNullOrEmpty(frameHandler.Endpoint.HostName))
                {
                    activity
                        .SetTag("server.address", frameHandler.Endpoint.HostName);
                }

                activity
                    .SetTag("server.port", frameHandler.Endpoint.Port);

                if (frameHandler.RemoteEndPoint is IPEndPoint ipEndpoint)
                {
                    string remoteAddress = ipEndpoint.Address.ToString();
                    if (activity.GetTagItem("server.address") == null)
                    {
                        activity
                            .SetTag("server.address", remoteAddress);
                    }

                    activity
                        .SetTag("network.peer.address", remoteAddress)
                        .SetTag("network.peer.port", ipEndpoint.Port);
                }

                if (frameHandler.LocalEndPoint is IPEndPoint localEndpoint)
                {
                    string localAddress = localEndpoint.Address.ToString();
                    activity
                        .SetTag("client.address", localAddress)
                        .SetTag("client.port", localEndpoint.Port)
                        .SetTag("network.local.address", localAddress)
                        .SetTag("network.local.port", localEndpoint.Port);
                }
            }
        }

        private static void DefaultContextInjector(Activity sendActivity, IDictionary<string, object?> props)
        {
            DistributedContextPropagator.Current.Inject(sendActivity, props, DefaultContextSetter);
        }

        private static ActivityContext DefaultContextExtractor(IReadOnlyBasicProperties props)
        {
            if (props.Headers == null)
            {
                return default;
            }

            bool hasHeaders = false;
            foreach (string header in DistributedContextPropagator.Current.Fields)
            {
                if (props.Headers.ContainsKey(header))
                {
                    hasHeaders = true;
                    break;
                }
            }


            if (!hasHeaders)
            {
                return default;
            }

            DistributedContextPropagator.Current.ExtractTraceIdAndState(props.Headers, DefaultContextGetter, out string? traceParent, out string? traceState);
            return ActivityContext.TryParse(traceParent, traceState, out ActivityContext context) ? context : default;
        }

        private static void DefaultContextSetter(object? carrier, string name, string value)
        {
            if (!(carrier is IDictionary<string, object> carrierDictionary))
            {
                return;
            }

            // Only propagate headers if they haven't already been set
            carrierDictionary[name] = value;
        }

        private static void DefaultContextGetter(object? carrier, string name, out string? value, out IEnumerable<string>? values)
        {
            if (carrier is IDictionary<string, object> carrierDict &&
                carrierDict.TryGetValue(name, out object? propsVal) && propsVal is byte[] bytes)
            {
                value = Encoding.UTF8.GetString(bytes);
                values = default;
            }
            else
            {
                value = default;
                values = default;
            }
        }
    }
}
