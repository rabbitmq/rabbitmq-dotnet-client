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

        // error.type is Stable in the messaging convention, and is Conditionally
        // Required "if and only if the messaging operation has failed".
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

        // The process-wide default a connection captures when its factory set no TracingOptions.
        // Its injector/extractor default to the client's Default* delegates; the deprecated statics
        // below read and write those same slots, so there is a single source of the fallback.
        private static RabbitMQTracingOptions s_tracingOptions = new RabbitMQTracingOptions();

        // Applied to the four members below. They are process-wide mutable state that is
        // shared across every connection, which is the wrong owner for configuration that
        // belongs to the connection performing the operation. A connection now captures
        // ConnectionFactory.TracingOptions at creation; these remain as the default a
        // connection captures when the factory set none, so existing code keeps working.
        private const string ObsoleteMessage =
            "Process-wide tracing configuration is deprecated. Configure " +
            "ConnectionFactory.TracingOptions instead, which is owned by the connection that " +
            "performs the traced operations. These members will be removed in a future major " +
            "version. See https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1981.";

        /// <summary>
        /// Delegate that injects the current <see cref="Activity"/> context into a published
        /// message's headers. Assigning <see langword="null"/> throws <see cref="ArgumentNullException"/>.
        /// </summary>
        [Obsolete(ObsoleteMessage)]
        public static Action<Activity, IDictionary<string, object?>> ContextInjector
        {
            get => s_tracingOptions.ContextInjector;
            set => s_tracingOptions.ContextInjector = value;
        }

        /// <summary>
        /// Delegate that extracts the upstream <see cref="ActivityContext"/> from a received
        /// message's properties. Assigning <see langword="null"/> throws <see cref="ArgumentNullException"/>.
        /// </summary>
        [Obsolete(ObsoleteMessage)]
        public static Func<IReadOnlyBasicProperties, ActivityContext> ContextExtractor
        {
            get => s_tracingOptions.ContextExtractor;
            set => s_tracingOptions.ContextExtractor = value;
        }

        /// <summary>
        /// When <see langword="true"/> (the default), the routing key is appended to publish and
        /// delivery span names. Shortcut for
        /// <see cref="RabbitMQTracingOptions.UseRoutingKeyAsOperationName"/> on <see cref="TracingOptions"/>.
        /// </summary>
        [Obsolete(ObsoleteMessage)]
        public static bool UseRoutingKeyAsOperationName
        {
            get => s_tracingOptions.UseRoutingKeyAsOperationName;
            set => s_tracingOptions.UseRoutingKeyAsOperationName = value;
        }

        /// <summary>
        /// The options applied to every tracing span this client produces when a connection factory
        /// set no <see cref="ConnectionFactory.TracingOptions"/>. Assigning <see langword="null"/>
        /// throws <see cref="ArgumentNullException"/>.
        /// </summary>
        /// <remarks>
        /// Assigning copies the span-shaping options
        /// (<see cref="RabbitMQTracingOptions.UseRoutingKeyAsOperationName"/> and
        /// <see cref="RabbitMQTracingOptions.UsePublisherAsParent"/>) from the given instance; it does
        /// not adopt the instance or its <see cref="RabbitMQTracingOptions.ContextInjector"/> /
        /// <see cref="RabbitMQTracingOptions.ContextExtractor"/>. Before per-connection ownership those
        /// delegates were independent process-wide statics, so assigning this property never disturbed
        /// them; the copy preserves that, so a previously configured injector or extractor keeps
        /// working. Set the delegates through <see cref="ContextInjector"/> / <see cref="ContextExtractor"/>.
        /// </remarks>
        [Obsolete(ObsoleteMessage)]
        public static RabbitMQTracingOptions TracingOptions
        {
            get => s_tracingOptions;
            set
            {
                if (value is null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                s_tracingOptions.UseRoutingKeyAsOperationName = value.UseRoutingKeyAsOperationName;
                s_tracingOptions.UsePublisherAsParent = value.UsePublisherAsParent;
            }
        }
        internal static bool PublisherHasListeners => s_publisherSource.HasListeners();
        internal static bool SubscriberHasListeners => s_subscriberSource.HasListeners();

        /*
         * A connection captures its factory's RabbitMQTracingOptions, or null when the factory set
         * none. Null resolves to the process-wide default, read live at each operation, exactly as
         * before per-connection configuration existed - this is what keeps the deprecated global path
         * working. Callers resolve once and read all options from the returned instance. See #1981.
         */
        internal static RabbitMQTracingOptions ResolveTracingOptions(RabbitMQTracingOptions? tracing)
            => tracing ?? s_tracingOptions;

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
            RabbitMQTracingOptions? tracing, ActivityContext linkedContext = default)
        {
            if (!s_publisherSource.HasListeners())
            {
                return null;
            }

            RabbitMQTracingOptions effective = ResolveTracingOptions(tracing);
            Activity? activity = linkedContext == default
                ? s_publisherSource.StartRabbitMQActivity(
                    effective.UseRoutingKeyAsOperationName ? $"{MessagingOperationNameBasicPublish} {routingKey}" : MessagingOperationNameBasicPublish,
                    ActivityKind.Producer)
                : s_publisherSource.StartLinkedRabbitMQActivity(
                    effective.UseRoutingKeyAsOperationName ? $"{MessagingOperationNameBasicPublish} {routingKey}" : MessagingOperationNameBasicPublish,
                    ActivityKind.Producer, linkedContext);
            if (activity != null && activity.IsAllDataRequested)
            {
                PopulateMessagingTags(MessagingOperationTypeSend, MessagingOperationNameBasicPublish, routingKey, exchange, 0, basicProperties, bodySize, activity);
            }

            return activity;
        }

        internal static Activity? BasicGetEmpty(string queue, RabbitMQTracingOptions? tracing)
        {
            if (!s_subscriberSource.HasListeners())
            {
                return null;
            }

            Activity? activity = s_subscriberSource.StartRabbitMQActivity(
                ResolveTracingOptions(tracing).UseRoutingKeyAsOperationName ? $"{MessagingOperationNameBasicGetEmpty} {queue}" : MessagingOperationNameBasicGetEmpty,
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
            IReadOnlyBasicProperties readOnlyBasicProperties, int bodySize, RabbitMQTracingOptions? tracing)
        {
            if (!s_subscriberSource.HasListeners())
            {
                return null;
            }

            // Extract the PropagationContext of the upstream parent from the message headers.
            RabbitMQTracingOptions effective = ResolveTracingOptions(tracing);
            ActivityContext linkedContext = effective.ContextExtractor(readOnlyBasicProperties);
            ActivityContext parentContext = effective.UsePublisherAsParent ? linkedContext : default;

            Activity? activity = s_subscriberSource.StartLinkedRabbitMQActivity(
                effective.UseRoutingKeyAsOperationName ? $"{MessagingOperationNameBasicGet} {routingKey}" : MessagingOperationNameBasicGet, ActivityKind.Consumer,
                linkedContext, parentContext);


            if (activity != null && activity.IsAllDataRequested)
            {
                PopulateMessagingTags(MessagingOperationTypeReceive, MessagingOperationNameBasicGet, routingKey, exchange, deliveryTag, readOnlyBasicProperties,
                    bodySize, activity);
            }

            return activity;
        }

        internal static Activity? Deliver(string routingKey, string exchange, ulong deliveryTag,
            IReadOnlyBasicProperties readOnlyBasicProperties, int bodySize, RabbitMQTracingOptions? tracing)
        {
            if (!s_subscriberSource.HasListeners())
            {
                return null;
            }

            // Extract the PropagationContext of the upstream parent from the message headers.
            RabbitMQTracingOptions effective = ResolveTracingOptions(tracing);
            ActivityContext linkedContext = effective.ContextExtractor(readOnlyBasicProperties);
            ActivityContext parentContext = effective.UsePublisherAsParent ? linkedContext : default;

            Activity? activity = s_subscriberSource.StartLinkedRabbitMQActivity(
                effective.UseRoutingKeyAsOperationName ? $"{MessagingOperationNameBasicDeliver} {routingKey}" : MessagingOperationNameBasicDeliver,
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
         * The OpenTelemetry "Recording errors" document prescribes exactly this set
         * for an operation that ends with an error: set the span status code to
         * Error, set error.type, and set the status description to the exception
         * message when the failure is an exception. The messaging conventions defer
         * to it ("Span status SHOULD follow the Recording Errors document"), and the
         * status code MUST be left unset when the operation succeeded, which is why
         * this is only called on failure paths.
         *
         * Tracing backends treat an unset status as success, so a span that merely
         * carries an exception event still reads as a successful operation in
         * error-rate queries. error.type is Stable in the messaging convention and
         * is what error-rate queries key off; it is set to the fully-qualified
         * exception type name, which is what the convention prescribes when there
         * is no lower-cardinality domain-specific value to use. The connection spans
         * use this same helper, so publisher, subscriber and connection spans report
         * failures uniformly.
         *
         * All three signals fire together so they stay consistent across sampling
         * levels, gated only on a null activity. AddException and SetStatus already
         * execute when IsAllDataRequested is false - a listener sampling
         * PropagationData still receives the event and the status - so gating
         * error.type alone, as an earlier version of this helper did, recorded the
         * expensive signals and dropped the cheap one that queries actually use.
         *
         * AddException is the only allocating signal (it builds an ActivityEvent) and
         * fires even on a PropagationData-sampled span. That is deliberate, not an
         * oversight: it is left unguarded rather than placed behind IsAllDataRequested
         * because failure paths are not hot, so the allocation never lands on a hot
         * path, and splitting it out would reintroduce the inconsistency above and turn
         * the logs migration below into a three-site edit instead of one.
         *
         * The exception event is the one signal here on a deprecation path: the
         * exceptions-on-spans convention is deprecated in favour of recording
         * exceptions as log records, and Activity.AddException is expected to follow.
         * The status and error.type are unaffected by that change. Keeping all three
         * in one helper is what makes the eventual migration a single edit. See
         * issues #1967 and #1992.
         */
        internal static void SetActivityError(this Activity? activity, Exception exception)
        {
            if (activity is null)
            {
                return;
            }

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

        internal static void DefaultContextInjector(Activity sendActivity, IDictionary<string, object?> props)
        {
            DistributedContextPropagator.Current.Inject(sendActivity, props, DefaultContextSetter);
        }

        internal static ActivityContext DefaultContextExtractor(IReadOnlyBasicProperties props)
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
