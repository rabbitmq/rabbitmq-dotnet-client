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

        // error.type is the only Stable attribute in the messaging convention, and is
        // Conditionally Required "if and only if the messaging operation has failed".
        internal const string ErrorType = "error.type";

        // These constants are specific to this client - the OpenTelemetry messaging
        // conventions do not (yet) cover connection establishment.
        internal const string RabbitMQConnectionIsReconnection = "messaging.rabbitmq.connection.is_reconnection";
        internal const string RabbitMQConnectionAutomaticRecovery = "messaging.rabbitmq.connection.automatic_recovery";

        private static readonly string AssemblyVersion = typeof(RabbitMQActivitySource).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "";

        private static readonly ActivitySource s_publisherSource =
            new ActivitySource(PublisherSourceName, AssemblyVersion);

        private static readonly ActivitySource s_subscriberSource =
            new ActivitySource(SubscriberSourceName, AssemblyVersion);

        private static readonly ActivitySource s_connectionSource =
            new ActivitySource(ConnectionSourceName, AssemblyVersion);

        public const string PublisherSourceName = "RabbitMQ.Client.Publisher";
        public const string SubscriberSourceName = "RabbitMQ.Client.Subscriber";
        public const string ConnectionSourceName = "RabbitMQ.Client.Connection";

        public static Action<Activity, IDictionary<string, object?>> ContextInjector { get; set; } =
            DefaultContextInjector;

        public static Func<IReadOnlyBasicProperties, ActivityContext> ContextExtractor { get; set; } =
            DefaultContextExtractor;

        public static bool UseRoutingKeyAsOperationName
        {
            get => TracingOptions.UseRoutingKeyAsOperationName;
            set => TracingOptions.UseRoutingKeyAsOperationName = value;
        }
        public static RabbitMQTracingOptions TracingOptions { get; set; } = new RabbitMQTracingOptions();
        internal static bool PublisherHasListeners => s_publisherSource.HasListeners();

        /*
         * Both PopulateMessageEnvelopeSize and Connection.WriteAsync tag whatever
         * Activity.Current happens to be, because the frame-writing path has no
         * reference to the publish activity it belongs to. Without this check, any
         * AMQP method issued inside an unrelated ambient activity stamps messaging
         * and network tags onto a span this library does not own - an app's own
         * span, or an ASP.NET request span, picking up server.address and friends
         * from an incidental QueueDeclare.
         *
         * The test is publisher-source ownership specifically, not "any activity
         * from this library". The connection spans are ours too, but they are not
         * publish operations: gating on the library as a whole would leave the
         * "connection attempt" span carrying messaging.message.envelope.size from
         * the handshake frames. Connection spans get their network tags from the
         * direct SetNetworkTags calls at the three connection call sites.
         */
        private static bool IsPublisherActivity(Activity? activity)
        {
            return activity is not null && ReferenceEquals(activity.Source, s_publisherSource);
        }

        internal static readonly IEnumerable<KeyValuePair<string, object?>> CreationTags = new[]
        {
            new KeyValuePair<string, object?>(MessagingSystem, "rabbitmq"),
            new KeyValuePair<string, object?>(ProtocolName, "amqp"),
            new KeyValuePair<string, object?>(ProtocolVersion, "0.9.1")
        };

        internal static Activity? OpenConnection(bool isReconnection)
        {
            if (!s_connectionSource.HasListeners())
            {
                return null;
            }

            Activity? connectionActivity =
                s_connectionSource.StartRabbitMQActivity("connection attempt", ActivityKind.Client);
            if (connectionActivity is { IsAllDataRequested: true })
            {
                connectionActivity.SetTag(RabbitMQConnectionIsReconnection, isReconnection);
            }

            return connectionActivity;
        }

        internal static Activity? OpenTcpConnection(AmqpTcpEndpoint endpoint)
        {
            if (!s_connectionSource.HasListeners())
            {
                return null;
            }

            Activity? activity =
                s_connectionSource.StartRabbitMQActivity("tcp connection attempt", ActivityKind.Client);
            if (activity is { IsAllDataRequested: true })
            {
                activity.SetServerTags(endpoint);
            }

            return activity;
        }

        internal static Activity? BasicPublish(string routingKey, string exchange, int bodySize, IReadOnlyBasicProperties basicProperties,
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
                PopulateMessagingTags(MessagingOperationTypeSend, MessagingOperationNameBasicPublish, routingKey, exchange, 0, basicProperties, bodySize, activity);
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
            ActivityContext linkedContext = ContextExtractor(readOnlyBasicProperties);
            ActivityContext parentContext = TracingOptions.UsePublisherAsParent ? linkedContext : default;

            Activity? activity = s_subscriberSource.StartLinkedRabbitMQActivity(
                UseRoutingKeyAsOperationName ? $"{MessagingOperationNameBasicGet} {routingKey}" : MessagingOperationNameBasicGet, ActivityKind.Consumer,
                linkedContext, parentContext);


            if (activity != null && activity.IsAllDataRequested)
            {
                PopulateMessagingTags(MessagingOperationTypeReceive, MessagingOperationNameBasicGet, routingKey, exchange, deliveryTag, readOnlyBasicProperties,
                    bodySize, activity);
            }

            return activity;
        }

        internal static Activity? Deliver(string routingKey, string exchange, ulong deliveryTag,
            IReadOnlyBasicProperties readOnlyBasicProperties, int bodySize)
        {
            if (!s_subscriberSource.HasListeners())
            {
                return null;
            }

            // Extract the PropagationContext of the upstream parent from the message headers.
            ActivityContext linkedContext = ContextExtractor(readOnlyBasicProperties);
            ActivityContext parentContext = TracingOptions.UsePublisherAsParent ? linkedContext : default;

            Activity? activity = s_subscriberSource.StartLinkedRabbitMQActivity(
                UseRoutingKeyAsOperationName ? $"{MessagingOperationNameBasicDeliver} {routingKey}" : MessagingOperationNameBasicDeliver,
                ActivityKind.Consumer, linkedContext, parentContext);
            if (activity != null && activity.IsAllDataRequested)
            {
                PopulateMessagingTags(MessagingOperationTypeProcess, MessagingOperationNameBasicDeliver, routingKey, exchange,
                    deliveryTag, readOnlyBasicProperties, bodySize, activity);
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
            List<ActivityLink>? links = null;
            if (linkedContext != default)
            {
                links = new List<ActivityLink>();
                links.Add(new ActivityLink(linkedContext));
            }
            return source.CreateActivity(name, kind, parentContext: parentContext,
                    links: links, idFormat: ActivityIdFormat.W3C,
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

        /*
         * As with SetNetworkTagsOnAmbientPublisherActivity, this reads Activity.Current
         * itself so the cheap HasListeners() test guards the AsyncLocal read on a path
         * that runs for every AMQP method transmitted.
         */
        internal static void PopulateMessageEnvelopeSizeOnAmbientPublisherActivity(int size)
        {
            if (!PublisherHasListeners)
            {
                return;
            }

            Activity? activity = Activity.Current;
            if (activity != null && activity.IsAllDataRequested && IsPublisherActivity(activity))
            {
                activity.SetTag(MessagingEnvelopeSize, size);
            }
        }

        /*
         * Tag the ambient activity from the frame-writing path. Unlike SetNetworkTags,
         * this must not touch an activity this library does not own. See
         * IsPublisherActivity.
         *
         * This reads Activity.Current itself rather than taking it as an argument:
         * that is an AsyncLocal read on a path that runs for every frame written, so
         * the cheap HasListeners() test comes first. With no publisher listeners the
         * source cannot have produced the ambient activity anyway.
         */
        internal static void SetNetworkTagsOnAmbientPublisherActivity(IFrameHandler frameHandler)
        {
            if (!PublisherHasListeners)
            {
                return;
            }

            Activity? activity = Activity.Current;
            if (IsPublisherActivity(activity))
            {
                activity.SetNetworkTags(frameHandler);
            }
        }

        /*
         * Record a failed messaging operation on its span.
         *
         * Tracing backends treat an unset status as success, so a span that merely
         * carries an exception event still reads as a successful operation in
         * error-rate queries. Every failure path therefore needs all three of:
         * the exception event, an Error status, and error.type.
         *
         * error.type is the fully-qualified exception type name, which is what the
         * convention prescribes when there is no lower-cardinality domain-specific
         * value to use. The connection spans do the same thing via SetActivityError,
         * so publisher, subscriber and connection spans report failures uniformly.
         *
         * All three are behind one IsAllDataRequested test. Splitting them - as an
         * earlier version of this helper did, gating only error.type - inverts the
         * cost: AddException allocates an ActivityEvent with a tag list, while
         * error.type is a single string already in hand, so the expensive signal
         * was recorded on spans the listener had asked not to fill in and the cheap
         * one was dropped. A span that is not AllData is not exported, so nothing
         * observable is lost by skipping all three. This also keeps the allocation
         * off the per-delivery consumer path when no one is recording.
         */
        internal static void SetActivityError(this Activity? activity, Exception exception)
        {
            if (activity is null || !activity.IsAllDataRequested)
            {
                return;
            }

            /*
             * All three signals fire together so they stay consistent across sampling
             * levels. AddException and SetStatus are cheap and already execute when
             * IsAllDataRequested is false (a listener sampling PropagationData still
             * receives the event and the status), so gating error.type - a single
             * string tag - would record the expensive signals and drop the cheap one.
             * That left a span marked Error with an exception event but no error.type,
             * which is the only Stable attribute in the messaging convention. See
             * issue #1967.
             */
            activity.AddException(exception);
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.SetTag(ErrorType, exception.GetType().FullName);
        }

        internal static void SetNetworkTags(this Activity? activity, IFrameHandler frameHandler)
        {
            if (activity?.IsAllDataRequested ?? false)
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
                activity.SetServerTags(frameHandler.Endpoint);

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

        internal static void SetServerTags(this Activity activity, AmqpTcpEndpoint endpoint)
        {
            if (!string.IsNullOrEmpty(endpoint.HostName))
            {
                activity
                    .SetTag("server.address", endpoint.HostName);
            }

            activity
                .SetTag("server.port", endpoint.Port);
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

            /*
             * Overwrite unconditionally. The client's own context is the authoritative
             * one for the span it just created, so a caller-supplied traceparent in the
             * same header table is replaced rather than preserved.
             */
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
