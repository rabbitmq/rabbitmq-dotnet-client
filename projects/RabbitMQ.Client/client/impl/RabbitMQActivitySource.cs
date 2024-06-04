﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client
{
    public static class RabbitMQActivitySource
    {
        // These constants are defined in the OpenTelemetry specification:
        // https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/#messaging-attributes
        internal const string MessageId = "messaging.message.id";
        internal const string MessageConversationId = "messaging.message.conversation_id";
        internal const string MessagingOperation = "messaging.operation";
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

        private static readonly ActivitySource s_publisherSource = new ActivitySource(PublisherSourceName, AssemblyVersion);
        private static readonly ActivitySource s_subscriberSource = new ActivitySource(SubscriberSourceName, AssemblyVersion);

        public const string PublisherSourceName = "RabbitMQ.Client.Publisher";
        public const string SubscriberSourceName = "RabbitMQ.Client.Subscriber";

        public static bool UseRoutingKeyAsOperationName { get; set; } = true;
        internal static bool PublisherHasListeners => s_publisherSource.HasListeners();
        internal static bool SubscriberHasListeners => s_subscriberSource.HasListeners();

        internal static readonly IEnumerable<KeyValuePair<string, object>> CreationTags = new[]
        {
            new KeyValuePair<string, object>(MessagingSystem, "rabbitmq"),
            new KeyValuePair<string, object>(ProtocolName, "amqp"),
            new KeyValuePair<string, object>(ProtocolVersion, "0.9.1")
        };

        internal static Activity Send(string routingKey, string exchange, int bodySize,
            ActivityContext linkedContext = default)
        {
            if (s_publisherSource.HasListeners())
            {
                Activity activity = linkedContext == default
                    ? s_publisherSource.StartRabbitMQActivity(UseRoutingKeyAsOperationName ? $"{routingKey} publish" : "publish",
                        ActivityKind.Producer)
                    : s_publisherSource.StartLinkedRabbitMQActivity(UseRoutingKeyAsOperationName ? $"{routingKey} publish" : "publish",
                        ActivityKind.Producer, linkedContext);
                if (activity?.IsAllDataRequested == true)
                {
                    PopulateMessagingTags("publish", routingKey, exchange, 0, bodySize, activity);
                }

                return activity;
            }

            return null;
        }

        internal static Activity ReceiveEmpty(string queue)
        {
            if (!s_subscriberSource.HasListeners())
            {
                return null;
            }

            Activity activity = s_subscriberSource.StartRabbitMQActivity(UseRoutingKeyAsOperationName ? $"{queue} receive" : "receive",
                ActivityKind.Consumer);
            if (activity.IsAllDataRequested)
            {
                activity
                    .SetTag(MessagingOperation, "receive")
                    .SetTag(MessagingDestination, "amq.default");
            }

            return activity;
        }

        internal static Activity Receive(string routingKey, string exchange, ulong deliveryTag,
            in ReadOnlyBasicProperties readOnlyBasicProperties, int bodySize)
        {
            if (!s_subscriberSource.HasListeners())
            {
                return null;
            }

            // Extract the PropagationContext of the upstream parent from the message headers.
            DistributedContextPropagator.Current.ExtractTraceIdAndState(readOnlyBasicProperties.Headers,
                ExtractTraceIdAndState, out string traceParent, out string traceState);
            ActivityContext.TryParse(traceParent, traceState, out ActivityContext linkedContext);
            Activity activity = s_subscriberSource.StartLinkedRabbitMQActivity(
                UseRoutingKeyAsOperationName ? $"{routingKey} receive" : "receive", ActivityKind.Consumer,
                linkedContext);
            if (activity.IsAllDataRequested)
            {
                PopulateMessagingTags("receive", routingKey, exchange, deliveryTag, readOnlyBasicProperties,
                    bodySize, activity);
            }

            return activity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetString(ReadOnlySpan<byte> span)
        {
#if NETSTANDARD
            if (span.Length == 0)
            {
                return string.Empty;
            }

            unsafe
            {
                fixed (byte* bytesPtr = span)
                {
                    return Encoding.UTF8.GetString(bytesPtr, span.Length);
                }
            }
#else
            return Encoding.UTF8.GetString(span);
#endif
        }


        internal static Activity Deliver(BasicDeliverEventArgs deliverEventArgs)
        {
            if (!s_subscriberSource.HasListeners())
            {
                return null;
            }

            // Extract the PropagationContext of the upstream parent from the message headers.
            DistributedContextPropagator.Current.ExtractTraceIdAndState(deliverEventArgs.BasicProperties.Headers,
                ExtractTraceIdAndState, out string traceparent, out string traceState);

            ActivityContext.TryParse(traceparent, traceState, out ActivityContext parentContext);

            string routingKey = UseRoutingKeyAsOperationName ? GetString(deliverEventArgs.RoutingKey.Span) : null;
            Activity activity = s_subscriberSource.StartLinkedRabbitMQActivity(
                UseRoutingKeyAsOperationName ? $"{routingKey} deliver" : "deliver",
                ActivityKind.Consumer, parentContext);

            if (activity != null && activity.IsAllDataRequested)
            {
                string exchange = GetString(deliverEventArgs.Exchange.Span);
                if (routingKey == null)
                {
                    routingKey = GetString(deliverEventArgs.RoutingKey.Span);
                }

                PopulateMessagingTags("deliver",
                    routingKey,
                    exchange,
                    deliverEventArgs.DeliveryTag,
                    deliverEventArgs.BasicProperties,
                    deliverEventArgs.Body.Length,
                    activity);
            }

            return activity;

        }

        private static Activity StartRabbitMQActivity(this ActivitySource source, string name, ActivityKind kind,
            ActivityContext parentContext = default)
        {
            Activity activity = source
                .CreateActivity(name, kind, parentContext, idFormat: ActivityIdFormat.W3C, tags: CreationTags)?.Start();
            return activity;
        }

        private static Activity StartLinkedRabbitMQActivity(this ActivitySource source, string name, ActivityKind kind,
            ActivityContext linkedContext = default, ActivityContext parentContext = default)
        {
            Activity activity = source.CreateActivity(name, kind, parentContext: parentContext,
                    links: new[] { new ActivityLink(linkedContext) }, idFormat: ActivityIdFormat.W3C,
                    tags: CreationTags)
                ?.Start();
            return activity;
        }

        private static void PopulateMessagingTags(string operation, string routingKey, string exchange,
            ulong deliveryTag, in ReadOnlyBasicProperties readOnlyBasicProperties, int bodySize, Activity activity)
        {
            PopulateMessagingTags(operation, routingKey, exchange, deliveryTag, bodySize, activity);

            if (!string.IsNullOrEmpty(readOnlyBasicProperties.CorrelationId))
            {
                activity.SetTag(MessageConversationId, readOnlyBasicProperties.CorrelationId);
            }

            if (!string.IsNullOrEmpty(readOnlyBasicProperties.MessageId))
            {
                activity.SetTag(MessageId, readOnlyBasicProperties.MessageId);
            }
        }

        private static void PopulateMessagingTags(string operation, string routingKey, string exchange,
            ulong deliveryTag, int bodySize, Activity activity)
        {
            activity
                .SetTag(MessagingOperation, operation)
                .SetTag(MessagingDestination, string.IsNullOrEmpty(exchange) ? "amq.default" : exchange)
                .SetTag(MessagingDestinationRoutingKey, routingKey)
                .SetTag(MessagingBodySize, bodySize);

            if (deliveryTag > 0)
            {
                activity.SetTag(RabbitMQDeliveryTag, deliveryTag);
            }
        }

        internal static void PopulateMessageEnvelopeSize(Activity activity, int size)
        {
            if (activity != null && activity.IsAllDataRequested && PublisherHasListeners)
            {
                activity.SetTag(MessagingEnvelopeSize, size);
            }
        }

        internal static bool TryGetExistingContext<T>(T props, out ActivityContext context)
            where T : IReadOnlyBasicProperties
        {
            if (props.Headers == null)
            {
                context = default;
                return false;
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

            if (hasHeaders)
            {
                DistributedContextPropagator.Current.ExtractTraceIdAndState(props.Headers, ExtractTraceIdAndState,
                    out string traceParent, out string traceState);
                return ActivityContext.TryParse(traceParent, traceState, out context);
            }

            context = default;
            return false;
        }

        private static void ExtractTraceIdAndState(object props, string name, out string value,
            out IEnumerable<string> values)
        {
            if (props is Dictionary<string, object> headers && headers.TryGetValue(name, out object propsVal) &&
                propsVal is byte[] bytes)
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

        internal static void SetNetworkTags(this Activity activity, IFrameHandler frameHandler)
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
    }
}
