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
        internal const string MessagingOperationNameBasicPublish = "publish";
        internal const string MessagingOperationType = "messaging.operation.type";
        internal const string MessagingOperationTypeSend = "send";
        internal const string MessagingOperationTypeProcess = "process";
        internal const string MessagingOperationTypeReceive = "receive";
        internal const string MessagingSystem = "messaging.system";
        internal const string MessagingDestination = "messaging.destination.name";
        internal const string MessagingDestinationRoutingKey = "messaging.rabbitmq.destination.routing_key";
        internal const string MessagingDestinationAnonymous = "messaging.destination.anonymous";
        internal const string MessagingBodySize = "messaging.message.body.size";
        internal const string MessagingEnvelopeSize = "messaging.message.envelope.size";
        internal const string NetworkTransport = "network.transport";
        internal const string ProtocolName = "network.protocol.name";
        internal const string ProtocolVersion = "network.protocol.version";
        internal const string RabbitMQDeliveryTag = "messaging.rabbitmq.message.delivery_tag";

        // error.type is Stable in the messaging convention, and is Conditionally
        // Required "if and only if the messaging operation has failed".
        internal const string ErrorType = "error.type";

        // Specific to this client: the convention has no attribute for an empty basic.get, and
        // encoding the outcome in messaging.operation.name instead is not valid.
        internal const string RabbitMQMessageReceived = "messaging.rabbitmq.message.received";

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

        /*
         * Applied to the four members below. What is deprecated is mutating the fallback through
         * these unowned statics, not the existence of a process-wide default: the default is a
         * legitimate layer, and RabbitMQ.Client.OpenTelemetry's AddRabbitMQInstrumentation still
         * sets it. A caller writing these slots directly reconfigures every connection that did not
         * opt out, with no owner and no way to undo it, which is what #1981 reported. Configuration
         * that belongs to a connection now goes on ConnectionFactory.TracingOptions.
         *
         * TracingOptions assigns by reference, as it always has, so replacing the instance replaces
         * the delegates with it. That is confined to this process-wide layer now: a connection
         * inherits member by member from it, so setting one thing on a factory no longer discards
         * the rest. See docs/internal/opentelemetry-tracing-review.md.
         */
        private const string ObsoleteMessage =
            "Setting tracing configuration directly on these process-wide statics is deprecated. " +
            "Set ConnectionFactory.TracingOptions instead, which is owned by the connection that " +
            "performs the traced operations; for propagation set ConnectionTracingOptions.Propagator " +
            "rather than these delegates, or call AddRabbitMQInstrumentation from the " +
            "RabbitMQ.Client.OpenTelemetry package to set the process-wide default. These members " +
            "will be removed in a future major version. " +
            "See https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1981.";

        /// <summary>
        /// Delegate that injects the current <see cref="Activity"/> context into a published
        /// message's headers. Defaults to W3C trace-context propagation using
        /// <see cref="DistributedContextPropagator.Current"/>.
        /// </summary>
        /// <remarks>
        /// Process-wide: used by every connection that set no
        /// <see cref="ConnectionTracingOptions.Propagator"/>, so the last assignment wins and nothing
        /// restores a previous value. Assigning <see langword="null"/> throws
        /// <see cref="ArgumentNullException"/>. Prefer
        /// <see cref="ConnectionTracingOptions.Propagator"/>, which a library can express without an
        /// OpenTelemetry dependency.
        /// </remarks>
        [Obsolete(ObsoleteMessage)]
        public static Action<Activity, IDictionary<string, object?>> ContextInjector
        {
            get => s_tracingOptions.ContextInjector;
            set => s_tracingOptions.ContextInjector = value;
        }

        /// <summary>
        /// Delegate that extracts the upstream <see cref="ActivityContext"/> from a received
        /// message's properties. Defaults to W3C trace-context propagation using
        /// <see cref="DistributedContextPropagator.Current"/>.
        /// </summary>
        /// <remarks>
        /// Process-wide: used by every connection that set no
        /// <see cref="ConnectionTracingOptions.Propagator"/>, so the last assignment wins and nothing
        /// restores a previous value. Assigning <see langword="null"/> throws
        /// <see cref="ArgumentNullException"/>. Prefer
        /// <see cref="ConnectionTracingOptions.Propagator"/>, which a library can express without an
        /// OpenTelemetry dependency.
        /// </remarks>
        [Obsolete(ObsoleteMessage)]
        public static Func<IReadOnlyBasicProperties, ActivityContext> ContextExtractor
        {
            get => s_tracingOptions.ContextExtractor;
            set => s_tracingOptions.ContextExtractor = value;
        }

        /// <summary>
        /// Shortcut for <see cref="RabbitMQTracingOptions.UseRoutingKeyAsOperationName"/> on
        /// <see cref="TracingOptions"/>. Reads and writes that instance, so it is affected by
        /// replacing it.
        /// </summary>
        [Obsolete(ObsoleteMessage)]
        public static bool UseRoutingKeyAsOperationName
        {
            get => s_tracingOptions.UseRoutingKeyAsOperationName;
            set => s_tracingOptions.UseRoutingKeyAsOperationName = value;
        }

        /// <summary>
        /// The options applied to every tracing span this client produces when a connection factory
        /// set no <see cref="ConnectionFactory.TracingOptions"/>.
        /// </summary>
        /// <remarks>
        /// Process-wide: these options shape the spans of every connection that captured this
        /// default, so the last assignment wins and disposing a <c>TracerProvider</c> that configured
        /// them restores nothing. Assigning replaces the instance, so a reference obtained from this
        /// property beforehand is detached and no longer affects the client when mutated - and,
        /// because the instance also carries <see cref="RabbitMQTracingOptions.ContextInjector"/> and
        /// <see cref="RabbitMQTracingOptions.ContextExtractor"/>, assigning replaces those too.
        /// Assigning <see langword="null"/> throws <see cref="ArgumentNullException"/>.
        /// </remarks>
        [Obsolete(ObsoleteMessage)]
        public static RabbitMQTracingOptions TracingOptions
        {
            get => s_tracingOptions;
            set => s_tracingOptions = value ?? throw new ArgumentNullException(nameof(value));
        }
        internal static bool PublisherHasListeners => s_publisherSource.HasListeners();
        internal static bool SubscriberHasListeners => s_subscriberSource.HasListeners();

        /*
         * Resolve per member, not per object. A whole-object fallback meant that setting one
         * span-shaping flag on a factory also replaced the propagation delegates with a fresh
         * object's defaults, silently switching that connection off whatever the process-wide layer
         * had installed. See #1981 and docs/internal/opentelemetry-tracing-review.md.
         *
         * A propagator set by the owner wins over process-wide delegates: the more specific owner
         * beats the process default, as it does for the flags.
         */
        internal static ResolvedTracingOptions ResolveTracingOptions(ConnectionTracingOptions? tracing)
        {
            RabbitMQTracingOptions process = s_tracingOptions;

            bool useRoutingKey = tracing?.UseRoutingKeyAsOperationName ?? process.UseRoutingKeyAsOperationName;
            bool usePublisherAsParent = tracing?.UsePublisherAsParent ?? process.UsePublisherAsParent;
            bool includeMessageSizes = tracing?.IncludeMessageSizes ?? process.IncludeMessageSizes;

            if (tracing?.Propagator is not null)
            {
                PropagatorAdapter adapter = tracing.GetOrCreateAdapter();
                return new ResolvedTracingOptions(useRoutingKey, usePublisherAsParent, includeMessageSizes,
                    adapter.Injector, adapter.Extractor);
            }

            return new ResolvedTracingOptions(useRoutingKey, usePublisherAsParent, includeMessageSizes,
                process.ContextInjector, process.ContextExtractor);
        }

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

        internal static readonly KeyValuePair<string, object?>[] CreationTags = new[]
        {
            new KeyValuePair<string, object?>(MessagingSystem, "rabbitmq"),
            new KeyValuePair<string, object?>(ProtocolName, "amqp"),
            new KeyValuePair<string, object?>(ProtocolVersion, "0.9.1"),
            new KeyValuePair<string, object?>(NetworkTransport, "tcp")
        };

        /*
         * rabbitmq.md names eight attributes that "SHOULD be provided at span creation time (if
         * provided at all)" because a sampler can only see what creation carries. These are the
         * three of them this client knows at that point; messaging.system is already in
         * CreationTags, server.address and server.port are not reachable here (#2064), and the
         * partition and template attributes do not apply. Composed from CreationTags rather than
         * repeated, so the two cannot drift.
         */
        private static KeyValuePair<string, object?>[] MessagingCreationTags(string operationName,
            string operationType, string destination)
        {
            bool haveDestination = false == string.IsNullOrEmpty(destination);
            var tags = new KeyValuePair<string, object?>[CreationTags.Length + (haveDestination ? 3 : 2)];
            Array.Copy(CreationTags, tags, CreationTags.Length);

            int i = CreationTags.Length;
            tags[i++] = new KeyValuePair<string, object?>(MessagingOperationName, operationName);
            tags[i++] = new KeyValuePair<string, object?>(MessagingOperationType, operationType);
            if (haveDestination)
            {
                tags[i] = new KeyValuePair<string, object?>(MessagingDestination, destination);
            }

            return tags;
        }

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
            ResolvedTracingOptions tracing, ActivityContext linkedContext = default)
        {
            if (!s_publisherSource.HasListeners())
            {
                return null;
            }

            string destination = BuildDestinationName(MessagingRole.Producer, exchange, routingKey, null);
            bool anonymous = IsAnonymousDestination(MessagingRole.Producer, exchange, routingKey, null);
            string spanName = SpanName(MessagingOperationNameBasicPublish, destination, anonymous, tracing);

            Activity? activity = linkedContext == default
                ? s_publisherSource.StartRabbitMQActivity(spanName, ActivityKind.Producer,
                    tags: MessagingCreationTags(MessagingOperationNameBasicPublish, MessagingOperationTypeSend, destination))
                : s_publisherSource.StartLinkedRabbitMQActivity(spanName, ActivityKind.Producer, linkedContext,
                    tags: MessagingCreationTags(MessagingOperationNameBasicPublish, MessagingOperationTypeSend, destination));
            if (activity != null && activity.IsAllDataRequested)
            {
                PopulateMessagingTags(MessagingOperationTypeSend, MessagingOperationNameBasicPublish, routingKey, destination, 0, basicProperties, bodySize,
                    tracing.IncludeMessageSizes, activity);
                SetAnonymous(activity, anonymous);
            }

            return activity;
        }

        internal static Activity? BasicGetEmpty(string queue, ResolvedTracingOptions tracing)
        {
            if (!s_subscriberSource.HasListeners())
            {
                return null;
            }

            // No BuildDestinationName call: with no exchange and no routing key its answer is
            // the queue. A hit names all three parts, so hit and miss differ; both conform.
            string destination = queue ?? string.Empty;
            bool anonymous = IsGeneratedName(queue);

            Activity? activity = s_subscriberSource.StartRabbitMQActivity(
                SpanName(MessagingOperationNameBasicGet, destination, anonymous, tracing),
                ActivityKind.Client,
                tags: MessagingCreationTags(MessagingOperationNameBasicGet, MessagingOperationTypeReceive, destination));
            if (activity != null && activity.IsAllDataRequested)
            {
                // operation.name is the same as a fetch that returned a message, and is a creation
                // tag: an empty result is an outcome, and encoding an outcome in it is not valid.
                activity.SetTag(RabbitMQMessageReceived, false);

                SetAnonymous(activity, anonymous);

                if (false == string.IsNullOrEmpty(destination))
                {
                    activity.SetTag(MessagingDestination, destination);
                }
            }

            return activity;
        }

        internal static Activity? BasicGet(string queue, string routingKey, string exchange, ulong deliveryTag,
            IReadOnlyBasicProperties readOnlyBasicProperties, int bodySize, ResolvedTracingOptions tracing)
        {
            if (!s_subscriberSource.HasListeners())
            {
                return null;
            }

            // Extract the PropagationContext of the upstream parent from the message headers.
            ActivityContext linkedContext = tracing.ContextExtractor(readOnlyBasicProperties);
            ActivityContext parentContext = tracing.UsePublisherAsParent ? linkedContext : default;

            string destination = BuildDestinationName(MessagingRole.Consumer, exchange, routingKey, queue);
            bool anonymous = IsAnonymousDestination(MessagingRole.Consumer, exchange, routingKey, queue);

            Activity? activity = s_subscriberSource.StartLinkedRabbitMQActivity(
                SpanName(MessagingOperationNameBasicGet, destination, anonymous, tracing), ActivityKind.Client,
                linkedContext, parentContext,
                tags: MessagingCreationTags(MessagingOperationNameBasicGet, MessagingOperationTypeReceive, destination));


            if (activity != null && activity.IsAllDataRequested)
            {
                PopulateMessagingTags(MessagingOperationTypeReceive, MessagingOperationNameBasicGet, routingKey,
                    destination, deliveryTag, readOnlyBasicProperties, bodySize, tracing.IncludeMessageSizes, activity);
                activity.SetTag(RabbitMQMessageReceived, true);
                SetAnonymous(activity, anonymous);
            }

            return activity;
        }

        internal static Activity? Deliver(string routingKey, string exchange, ulong deliveryTag,
            IReadOnlyBasicProperties readOnlyBasicProperties, int bodySize, ResolvedTracingOptions tracing)
        {
            if (!s_subscriberSource.HasListeners())
            {
                return null;
            }

            // Extract the PropagationContext of the upstream parent from the message headers.
            ActivityContext linkedContext = tracing.ContextExtractor(readOnlyBasicProperties);
            ActivityContext parentContext = tracing.UsePublisherAsParent ? linkedContext : default;

            // No queue: a delivery frame never carries the one it came from, so the two-part
            // form is all that is available. Carrying it is #2055.
            string destination = BuildDestinationName(MessagingRole.Consumer, exchange, routingKey, null);

            Activity? activity = s_subscriberSource.StartLinkedRabbitMQActivity(
                // anonymous: false unavoidably, since a delivery frame carries no queue (#2055).
                SpanName(MessagingOperationNameBasicDeliver, destination, anonymous: false, tracing),
                ActivityKind.Consumer, linkedContext, parentContext,
                tags: MessagingCreationTags(MessagingOperationNameBasicDeliver, MessagingOperationTypeProcess, destination));
            if (activity != null && activity.IsAllDataRequested)
            {
                PopulateMessagingTags(MessagingOperationTypeProcess, MessagingOperationNameBasicDeliver, routingKey,
                    destination, deliveryTag, readOnlyBasicProperties, bodySize, tracing.IncludeMessageSizes, activity);
            }

            return activity;
        }

        private static Activity? StartRabbitMQActivity(this ActivitySource source, string name, ActivityKind kind,
            ActivityContext parentContext = default, IEnumerable<KeyValuePair<string, object?>>? tags = null)
        {
            return source.CreateActivity(name, kind, parentContext, idFormat: ActivityIdFormat.W3C,
                tags: tags ?? CreationTags)?.Start();
        }

        private static Activity? StartLinkedRabbitMQActivity(this ActivitySource source, string name, ActivityKind kind,
            ActivityContext linkedContext = default, ActivityContext parentContext = default,
            IEnumerable<KeyValuePair<string, object?>>? tags = null)
        {
            List<ActivityLink>? links = null;
            if (linkedContext != default)
            {
                links = new List<ActivityLink>();
                links.Add(new ActivityLink(linkedContext));
            }
            return source.CreateActivity(name, kind, parentContext: parentContext,
                    links: links, idFormat: ActivityIdFormat.W3C,
                    tags: tags ?? CreationTags)
                ?.Start();
        }

        private static void PopulateMessagingTags(string operationType, string operationName, string routingKey,
            string destination, ulong deliveryTag, IReadOnlyBasicProperties readOnlyBasicProperties, int bodySize, bool includeMessageSizes,
            Activity activity)
        {
            PopulateMessagingTags(operationType, operationName, routingKey, destination, deliveryTag, bodySize,
                includeMessageSizes, activity);

            if (!string.IsNullOrEmpty(readOnlyBasicProperties.CorrelationId))
            {
                activity.SetTag(MessageConversationId, readOnlyBasicProperties.CorrelationId);
            }

            if (!string.IsNullOrEmpty(readOnlyBasicProperties.MessageId))
            {
                activity.SetTag(MessageId, readOnlyBasicProperties.MessageId);
            }
        }

        // {messaging.operation.name} {destination}, where the destination is the destination
        // name and not the routing key. UseRoutingKeyAsOperationName decides whether it appears
        // at all; the flag's name predates that meaning.
        /*
         * A queue name the broker minted, which is new on every declaration and so would put one
         * span name per queue into a backend. That cardinality, not anonymity for its own sake, is
         * what the {destination} placeholder rule guards against; the full name stays on
         * messaging.destination.name, which is a tag and tolerates it.
         *
         * Two known holes, both of which need the provenance of the name rather than its text.
         * A client that passes `x-name-prefix` to queue.declare gets `<prefix>-<base64url>` from
         * the same generator (rabbit_amqqueue:derive_name_prefix/1 falls back to "amq.gen"), which
         * is equally unique per declaration and is not matched here. And a queue an application
         * deliberately named to look generated would be matched when it should not be. Tracking
         * what the broker handed back at declare time fixes both; that is #2055.
         */
        internal static bool IsGeneratedName(string? name) =>
            name?.StartsWith("amq.gen-", StringComparison.Ordinal) == true;

        /*
         * Whether the destination as a whole is anonymous, which the {destination} placeholder rule
         * turns into "leave it out of the span name". The rule differs by side: a publish is only
         * anonymous when the default exchange leaves the generated routing key as the sole
         * identifier, while a consumer's destination is anonymous whenever its queue is generated.
         * A delivery carries no queue, so it can never be marked; that is #2055.
         */
        internal static bool IsAnonymousDestination(MessagingRole role, string exchange, string routingKey, string? queue)
        {
            if (role == MessagingRole.Producer)
            {
                return string.IsNullOrEmpty(exchange) && IsGeneratedName(routingKey);
            }

            return IsGeneratedName(queue);
        }

        /*
         * {messaging.operation.name} {destination}. The destination is left out when it is
         * anonymous: rule 2 of the placeholder allows messaging.destination.name only for a
         * destination "neither temporary nor anonymous", rule 1 needs a template RabbitMQ does not
         * have, and rule 3 is for operations with no specific destination. So nothing applies and
         * the name is the bare operation, rather than one span name per generated queue.
         */
        private static string SpanName(string operationName, string destination, bool anonymous,
            ResolvedTracingOptions tracing)
        {
            if (false == tracing.UseRoutingKeyAsOperationName || anonymous || string.IsNullOrEmpty(destination))
            {
                return operationName;
            }

            return $"{operationName} {destination}";
        }

        // Conditionally Required "If value is true", so set it or omit it, never false.
        private static void SetAnonymous(Activity activity, bool anonymous)
        {
            if (anonymous)
            {
                activity.SetTag(MessagingDestinationAnonymous, true);
            }
        }

        internal enum MessagingRole
        {
            Producer,
            Consumer
        }

        /*
         * messaging.destination.name per the RabbitMQ convention: {exchange}:{routing key} for a
         * producer, {exchange}:{routing key}:{queue} for a consumer, empty parts omitted, and
         * amq.default on the producer side only.
         * https://opentelemetry.io/docs/specs/semconv/messaging/rabbitmq/
         */
        internal static string BuildDestinationName(MessagingRole role, string exchange, string routingKey, string? queue)
        {
            bool haveExchange = false == string.IsNullOrEmpty(exchange);
            bool haveRoutingKey = false == string.IsNullOrEmpty(routingKey);

            // The role decides the shape, not just the fallback: a producer passing a queue must
            // not get the three-part form. Equal routing key and queue are one component.
            bool haveQueue = role == MessagingRole.Consumer
                             && false == string.IsNullOrEmpty(queue)
                             && false == string.Equals(routingKey, queue, StringComparison.Ordinal);

            if (haveExchange)
            {
                if (haveRoutingKey)
                {
                    return haveQueue
                        ? string.Concat(string.Concat(exchange, ":", routingKey), ":", queue)
                        : string.Concat(exchange, ":", routingKey);
                }

                return haveQueue ? string.Concat(exchange, ":", queue) : exchange;
            }

            if (haveRoutingKey)
            {
                return haveQueue ? string.Concat(routingKey, ":", queue) : routingKey;
            }

            if (haveQueue)
            {
                return queue!;
            }

            return role == MessagingRole.Producer ? "amq.default" : string.Empty;
        }

        private static void PopulateMessagingTags(string operationType, string operationName, string routingKey,
            string destination, ulong deliveryTag, int bodySize, bool includeMessageSizes, Activity activity)
        {
            // operation.type, operation.name and destination.name are creation tags now, so a
            // sampler can see them; see MessagingCreationTags.

            // Opt-In: populate if and only if the user asked for it.
            if (includeMessageSizes)
            {
                activity.SetTag(MessagingBodySize, bodySize);
            }

            // routing_key is Conditionally Required "If not empty".
            if (false == string.IsNullOrEmpty(routingKey))
            {
                activity.SetTag(MessagingDestinationRoutingKey, routingKey);
            }

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
        internal static void PopulateMessageEnvelopeSizeOnAmbientPublisherActivity(int size,
            ConnectionTracingOptions? tracing)
        {
            if (!PublisherHasListeners)
            {
                return;
            }

            // Opt-In, so resolve the flag here rather than per frame above the guard.
            if (false == (tracing?.IncludeMessageSizes ?? s_tracingOptions.IncludeMessageSizes))
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
         * exceptions-on-spans convention is deprecated in favor of recording
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
