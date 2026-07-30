# OpenTelemetry Tracing: Implementation Review

This document records a full review of the client's OpenTelemetry tracing implementation, carried out for issue #1967 before #1923 locks the public tracing API for 7.3.0. Every claim below that is marked *verified* was settled by driving the real SDK pipeline against a live broker, not by reading code.

Read this before changing anything under `RabbitMQActivitySource`, `RabbitMQ.Client.OpenTelemetry`, or the `Activity.Current` call sites in `SessionBase` / `Connection`.

## Status

The findings were split into three groups, because they carry very different risk:

| Group | Content | Status |
|---|---|---|
| A | Behavioural defects: ambient-span pollution, failures never recorded, untagged `tcp connection attempt`, the null-`Headers` extractor path, a wrong comment | **Fixed.** Sections below are marked `FIXED` individually. |
| B | Semantic-convention conformance. Each one changes emitted span names or attributes, and several break existing test assertions. | Open |
| C | Public API: per-provider tracing configuration. Must land before #1923. | Open |

Group A was separated out precisely because none of it changes a conforming attribute value or span name, so it can ship without a downstream consumer having to re-key anything. Groups B and C build on this branch as stacked PRs.

One observable change did come with Group A: the `publish` span now starts before flow control rather than after, so its duration includes any flow-control blocking. Nothing became slower, but publish-latency dashboards will shift.

## The three activity sources

| Source | Spans | Created in |
|---|---|---|
| `RabbitMQ.Client.Connection` | `connection attempt`, `tcp connection attempt` | `ConnectionFactory`, `AutorecoveringConnection`, `IEndpointResolverExtensions` |
| `RabbitMQ.Client.Publisher` | `publish` | `Channel.BasicPublish.cs` |
| `RabbitMQ.Client.Subscriber` | `fetch`, `fetch (empty)`, `deliver` | `Channel.cs` (`BasicGetAsync`), `AsyncConsumerDispatcher` |

`ConnectionSourceName` is the only tracing member still in `PublicAPI.Unshipped.txt`. Everything else - `TracingOptions`, `ContextInjector`, `ContextExtractor`, `UseRoutingKeyAsOperationName`, and all of `RabbitMQTracingOptions` - shipped in 7.2.1.

## Guard pattern: the shape that matters

Each activity factory tests `HasListeners()` on **its own** source before creating anything, then tests `IsAllDataRequested` before setting tags. That part is correct and consistent.

The subtle part is `SetNetworkTags`. It has *no* listener check of its own:

```csharp
// projects/RabbitMQ.Client/Impl/RabbitMQActivitySource.cs
internal static void SetNetworkTags(this Activity? activity, IFrameHandler frameHandler)
{
    if (activity?.IsAllDataRequested ?? false)
    {
```

In 7.2.1 that check was `PublisherHasListeners && activity != null && activity.IsAllDataRequested`. It was moved out to the call site in `Connection.WriteAsync` deliberately: connection spans are created when the `Publisher` source may have no listeners at all, so a publisher gate inside the helper would have silently dropped network tags from every connection span. The three connection-side callers (`ConnectionFactory.cs:572`, `AutorecoveringConnection.cs:98`, `AutorecoveringConnection.Recovery.cs:263`) therefore call it directly on a known-owned activity, and only `Connection.WriteAsync` retains a `PublisherHasListeners` test.

That is why `TestCreateConnectionRegisterAnActivity` passes while subscribing to `RabbitMQ.Client.Connection` alone. It is working by design, not by luck. Do not "restore" the publisher gate inside `SetNetworkTags`.

Group A kept that shape. The publisher gate now lives in `SetNetworkTagsOnAmbientPublisherActivity`, a separate wrapper for the one ambient caller, so `SetNetworkTags` itself stays usable by the connection sites on a known-owned activity.

Group A also made `OpenTcpConnection` take its `AmqpTcpEndpoint` and set the server tags itself. It was the only activity factory returning a bare activity and relying on its call site to tag it, which made it the one place a new caller could silently produce an untagged span. Covered by `TestTcpConnectionActivityHasServerTags_GH1967`.

## Defect: ambient-span pollution (`Activity.Current`) - FIXED

There are exactly three `Activity.Current` reads in the client:

```
projects/RabbitMQ.Client/Impl/SessionBase.cs:133   PopulateMessageEnvelopeSize(Activity.Current, bytes.Size)
projects/RabbitMQ.Client/Impl/SessionBase.cs:160   PopulateMessageEnvelopeSize(Activity.Current, bytes.Size)
projects/RabbitMQ.Client/Impl/Connection.cs:558    Activity.Current.SetNetworkTags(_frameHandler)
```

None of them checks whether the ambient activity belongs to this library. The intent is to decorate the `publish` span, which *is* `Activity.Current` at the moment its frames are transmitted. But `SessionBase.TransmitAsync` is on the path of **every** AMQP method, not just `basic.publish`.

**Verified.** With the `Publisher` source listened and an application-owned `ActivitySource("MyApp")` span current, each of `QueueDeclarePassiveAsync`, `ExchangeDeclareAsync`, `BasicQosAsync`, `QueueBindAsync`, `BasicGetAsync`, and `BasicAckAsync` wrote ten tags onto the caller's span:

```
messaging.message.envelope.size
network.type
server.address, server.port
network.peer.address, network.peer.port
client.address, client.port
network.local.address, network.local.port
```

The same mechanism puts `messaging.message.envelope.size` on the client's own `connection attempt` span, since that span is current while the handshake frames go out - a messaging attribute on a connection span.

With no listener on the `Publisher` source the caller's span stays clean, so the blast radius is exactly "applications that enable publisher tracing", which is to say all of them.

This is **pre-existing**, not a regression from the connection-tracing work. `v7.2.1` already had `Activity.Current.SetNetworkTags(_frameHandler)` unconditionally in `Connection.WriteAsync` and both `Activity.Current` reads in `SessionBase`.

The fix has to distinguish "this is my span" from "this is the caller's span". Passing the owned activity down from `Channel.BasicPublish.cs` is the direct route; checking `activity.Source` against the client's sources is the cheaper one.

**Fixed** by the cheaper route: `RabbitMQActivitySource.IsPublisherActivity` tests `ReferenceEquals(activity.Source, s_publisherSource)`, and the two ambient call sites now go through `SetNetworkTagsOnAmbientPublisherActivity` / `PopulateMessageEnvelopeSizeOnAmbientPublisherActivity`, which read `Activity.Current` internally so the cheap `HasListeners()` test guards the `AsyncLocal` read.

The check is against the **publisher source specifically**, not "any activity from this library". That distinction is load-bearing: the connection spans are ours too, but they are not publish operations, so gating on the library as a whole would leave `connection attempt` still carrying `messaging.message.envelope.size` from the handshake frames. Connection spans keep getting their network tags from the direct `SetNetworkTags` calls described above.

Regression coverage: `TestAmqpOperationsDoNotTagAnUnrelatedAmbientActivity_GH1967` (asserts 11 absent tags on an app-owned span, then asserts the library's own `publish` span still gets them - this is an ownership check, not a blanket removal) and `TestConnectionActivityHasNoMessagingEnvelopeSize_GH1967`.

## Defect: failed operations never record an error - FIXED

Activity **disposal** is correct everywhere - every creation site uses `using`, including the error paths through `BasicPublishCoreAsync`.

Activity **status** is not. No publisher or subscriber span ever records an error, because the `catch` blocks sit outside the activity's `using` scope:

- `AsyncConsumerDispatcher.cs` - `using (Activity? activity = ...Deliver(...))` closes at line 39; the `catch (Exception e)` that reports to `CallbackExceptionAsync` is at line 59.
- `Channel.BasicPublish.cs` - `using Activity? sendActivity` at line 107 is scoped to the inner `try`; the `catch (Exception ex)` at line 126 cannot see it.

**Verified.** A mandatory publish to an exchange with no matching queue raises `PublishReturnException` to the caller, and its span ends `status=Unset`, `StatusDescription=null`, zero events. A consumer `ReceivedAsync` handler that throws `InvalidOperationException` is reported through `CallbackExceptionAsync`, and its `deliver` span ends `status=Unset` with zero events.

The connection spans get this right - `IEndpointResolverExtensions.cs:40-95` calls both `AddException` and `SetStatus(ActivityStatusCode.Error)` - so the inconsistency is internal to one implementation.

Related: `error.type` is the only **Stable** attribute in the RabbitMQ semantic convention and is Conditionally Required "if and only if the messaging operation has failed". The client sets it nowhere.

**Fixed** via a single `SetActivityError(this Activity?, Exception)` helper that sets all three of the exception event, `SetStatus(Error)`, and `error.type`. All three are needed: a tracing backend reads an unset status as success, so a span carrying only an exception event still counts as a successful operation in error-rate queries. The helper also replaced the five hand-rolled `AddException` + `SetStatus` pairs on the connection spans, so publisher, subscriber and connection spans now report failures uniformly.

Two subtleties found while fixing this:

- **The publish failure surfaces from the `finally`, not the `try`.** `PublishReturnException` for an unroutable mandatory publish comes out of `MaybeEndPublisherConfirmationTrackingAsync` via `MaybeWaitForConfirmationAsync`, which runs in the `finally` because the confirmation is only awaited once the send has been issued. Moving the `using` out of the inner try and adding a `catch` is therefore *not* sufficient on its own - the confirmation await needs its own try/catch. This was the primary verified case, so a fix without it would have looked complete and covered nothing.
- **A handled exception still marks the span `Error`.** If `MaybeHandleExceptionWithEnabledPublisherConfirmations` swallows, `SetActivityError` has already run. This is deliberate: the publish did fail, it was just reported through the confirmation mechanism instead of by throwing. Revisit if that proves noisy in practice.

If both the `try` and the `finally` throw, the span gets two exception events. That is accurate, but note `ActivityAssert.HasRecordedException` only inspects `Events.First()`.

Regression coverage: `TestPublishFailureIsRecordedOnTheSendActivity_GH1967` (mandatory publish to an exchange with no matching binding, which specifically exercises the `finally` path) and `TestConsumerFailureIsRecordedOnTheDeliverActivity_GH1967`. Both go through `ActivityAssert.RecordsFailure`, which asserts all three signals at once.

## Defect: tracing configuration is process-global, last writer wins

`RabbitMQActivitySource.TracingOptions` is a public settable static holding a mutable object, and `AddRabbitMQInstrumentation` replaces it wholesale while also overwriting both propagation delegates:

```csharp
// projects/RabbitMQ.Client.OpenTelemetry/TraceProviderBuilderExtensions.cs
RabbitMQActivitySource.TracingOptions = options;
RabbitMQActivitySource.ContextExtractor = OpenTelemetryContextExtractor;
RabbitMQActivitySource.ContextInjector = OpenTelemetryContextInjector;
```

**Verified.** Two independent `TracerProvider`s, each calling `AddRabbitMQInstrumentation` with different options: after the second call the first provider's configuration is silently gone, and *both* exporters receive spans shaped by the second (named `publish` / `fetch`, with no routing key). Disposing the second provider restores nothing - `ContextInjector` stays pointed at the OpenTelemetry implementation and `TracingOptions` keeps its values for the life of the process.

This is not a memory-safety problem. 181,425 publishes with a concurrent writer swapping the options object produced zero exceptions, because reference assignment is atomic. The defect is the ownership model: per-provider configuration expressed as process-global mutable state.

The statics are also unvalidated, so `ContextInjector = null` makes every subsequent publish throw `NullReferenceException` from inside the client.

Because these members shipped in 7.2.1, removing them is a breaking change. Adding a per-provider path alongside them is not.

## Semantic-convention gaps

Checked against the specification at `main`: `model/messaging/registry.yaml`, `docs/messaging/messaging-spans.md`, `docs/messaging/rabbitmq.md`.

Stability context: every `messaging.*` attribute is **Development**, none is Stable. `error.type` is Stable. So attribute-level changes are low-risk from the specification's own standpoint, and the client's attribute-name constants are `internal`.

### Span kind for `receive`

`messaging-spans.md` maps operation types to span kinds:

| Operation type | Span kind |
|---|---|
| `create` | `PRODUCER` |
| `send` | `PRODUCER` if the send span's context is the creation context, otherwise `CLIENT` |
| `receive` | `CLIENT` |
| `process` | `CONSUMER` |
| `settle` | `CLIENT` |

`BasicGet` and `BasicGetEmpty` both set `messaging.operation.type = receive` with `ActivityKind.Consumer`. They should be `ActivityKind.Client`. `Deliver` (`process` -> `Consumer`) and `BasicPublish` (`send` -> `Producer`, and its context is what gets injected) are both correct.

### `messaging.rabbitmq.delivery_tag` is not a registry attribute

The client emits `messaging.rabbitmq.delivery_tag`. The registry defines `messaging.rabbitmq.message.delivery_tag`. The emitted name matches nothing in the convention, so any consumer keying off it drops the value.

### `messaging.destination.name` does not follow the RabbitMQ convention

`rabbitmq.md` note [1] specifies `{exchange}:{routing key}` on the producer side when both are present and non-empty, only the available one when just one is, and `amq.default` only when the default exchange is used *and* no routing key is provided. The consumer side is `{exchange}:{routing key}:{queue}`.

The client sets the bare exchange name, or the literal `amq.default` whenever the exchange is empty regardless of routing key.

`BasicGetEmpty` is worse than non-conforming - it is wrong. It hardcodes `amq.default` even though the queue is known. **Verified** with a named exchange `probe-ex`, routing key `warning`, queue `probe-q`:

```
span "publish warning"        kind=Producer  messaging.destination.name = probe-ex
                                             (convention: probe-ex:warning)
span "fetch warning"          kind=Consumer  messaging.destination.name = probe-ex
                                             (convention: probe-ex:warning:probe-q)
span "fetch (empty) probe-q"  kind=Consumer  messaging.destination.name = amq.default
                                             (the fetch never touched the default exchange)
error.type absent on all three.
```

### Span names use the routing key, not `{destination}`

The convention is `{messaging.operation.name} {destination}`, where `{destination}` prefers `messaging.destination.template`, then `messaging.destination.name`, then `server.address:server.port`. The client appends the routing key. For server-named queues that also makes the span name high-cardinality, which the guidance on temporary and anonymous destinations warns against specifically.

### `fetch (empty)` is not a valid operation name

`messaging.operation.name = "fetch (empty)"` encodes an outcome into the operation name. `rabbitmq.md` gives `receive` and `poll` as receive-span examples. An empty result is representable without a distinct operation name.

### `messaging.message.envelope.size` and `body.size` are Opt-In

Opt-In means "SHOULD NOT be collected by default". The client always emits both when sampling. Defensible for a client library, but worth knowing.

## Context propagation: no defects found

**Verified** against a live broker with the `Publisher` source deliberately left unlistened so hand-planted headers survive the injector. Every one of these produced an unparented `fetch` span with zero links and no exception: no headers at all, an unrelated header only, a malformed `traceparent` as bytes, `traceparent = null`, `traceparent` as an `int`, `tracestate` with no `traceparent`, an empty-string `traceparent`, and a legacy `Request-Id` only. A well-formed `traceparent` parsed correctly as `byte[]` and as `string`, producing both a parent and a link.

`DefaultContextGetter` handles only `byte[]`, which is *not* a defect: the broker returns header values as `byte[]` on the wire. It would only matter for an in-process carrier, which does not arise.

Two cosmetic notes, both **fixed** in Group A:

- `OpenTelemetryContextExtractor` passed `props.Headers` to `Propagators.DefaultTextMapPropagator.Extract` with no null check, unlike `DefaultContextExtractor` which returns early. With null headers the getter dereferenced null once per propagator field; the outcome was correct only because `catch (Exception)` swallowed it. That blanket catch was load-bearing rather than defensive, and its logger line was commented out, so a genuine extraction failure was silent. Now: an early return for null `Headers`, a `carrier != null` guard in the getter, and a comment explaining that the catch is now defensive-only (a custom `IDictionary` reaching the header table could still throw from `TryGetValue`, and a failed extraction must not fail the delivery). No test - the pre-existing catch already produced the right outcome, so there is no observable behaviour to assert.
- `DefaultContextSetter`'s comment said "Only propagate headers if they haven't already been set"; the code assigns unconditionally. The overwrite is the right behaviour - the comment was wrong, and now says so.

## Consumer concurrency: no defects found

**Verified** with 40 messages, `ConsumerDispatchConcurrency = 8` on both the factory and the channel, and a random 1-15 ms delay inside each callback to force interleaving:

```
publish spans 40, deliver spans 40
deliver spans parented to a span in the publish set   40/40
distinct deliver parent ids                          40
distinct deliver trace ids                            40
parent ids shared by more than one deliver span        0
Activity.Current inside the callback was that message's own deliver span  40/40
```

No mis-parenting, no context bleed across dispatch slots.

## Test coverage gaps

`TestActivitySource.cs` and `TestOpenTelemetry.cs` (4 tests) both live in `SequentialIntegration`, because `ActivityRecorder` and the activity sources are process-global. `TestOpenTelemetry.cs` drives the real SDK (`Sdk.CreateTracerProviderBuilder`, `AddRabbitMQInstrumentation`, `AddInMemoryExporter`); `TestActivitySource.cs` uses a bare `ActivityListener`.

Group A closed the first two gaps, adding five `_GH1967` tests to `TestActivitySource.cs` and an `ActivityAssert.RecordsFailure` helper:

- ~~**Error status** is asserted only on connection spans.~~ Now asserted on `publish` and `deliver`.
- ~~**Ambient-span pollution** has no coverage at all. `ActivityAssert.HasNoTag` exists and is used nowhere.~~ `HasNoTag` is now used for 11 tags in the ambient test and for `messaging.message.envelope.size` on the connection span.

Still open, and both belong to Group B:

- **Two assertions lock in the span-kind gap.** `TestOpenTelemetry.cs` and `TestActivitySource.cs` both assert `ActivityKind.Consumer` for the `fetch` span. Fixing the span kind requires updating them.
- **One assertion locks in the destination gap.** `TestActivitySource.cs` asserts `messaging.destination.name == "amq.default"` for the default-exchange case.

`ActivityRecorder.ShouldListenTo` is an exact source-name match, so a recorder constructed with `ConnectionSourceName` cannot see publisher or subscriber spans. Keep that in mind when reasoning about which tests would catch which regression.

### `ActivityRecorder` matches on span name, and the routing key is in it by default

`UseRoutingKeyAsOperationName` defaults to **`true`**, so a publish span is named `publish <routing-key>`, not `publish`. `ActivityRecorder` matches `activity.OperationName` exactly, so a recorder built for `"publish"` records **zero** activities under the default configuration and fails with `Expected: 1 / Actual: 0` - no hint that the name is the problem.

Three of the five new tests hit this. Any new test that constructs a recorder with a bare operation name needs `TestActivitySource.PlainOperationNames`, a `using` scope that sets the flag false and **restores the previous value on dispose**.

The restore matters beyond politeness. None of the pre-existing tests restore this flag or `TracingOptions` after mutating them, so test outcomes depend on execution order, and `TestOpenTelemetry.TestDefaultTracingOptions` asserts the default is `true` - it would fail if an earlier test left it `false`. That is Group C's defect (process-global configuration, last writer wins) reproducing inside our own test suite, which is a reasonable argument for fixing it.

## Documentation gap

`messaging-spans.md` states that an instrumentation using the message creation context as the parent of `process` spans SHOULD document that it does so, and MAY offer a configuration option. This client does exactly that by default (`UsePublisherAsParent = true`) and the option exists, but the `RabbitMQ.Client.OpenTelemetry` README documents only SDK wiring - not the trace structure, the span names, the attributes emitted, or either option.

## What was checked and found clean

- Activity disposal on every path, including the error branches through `BasicPublishCoreAsync`.
- `HasListeners()` / `IsAllDataRequested` guard pairing at all activity-creation sites.
- Exception recording on the connection spans (`IEndpointResolverExtensions.cs` sets both the event and the status, and deliberately leaves the parent connection activity alone when a later endpoint succeeds - Group A preserved that, routing it through `SetActivityError` and picking up `error.type` in the process).
- Public API surface parity between `net8.0` and `netstandard2.0`.
- `RabbitMQ.Client.OpenTelemetry` packaging: TFMs, signing, SourceLink, and the `otel-` MinVer prefix for independent versioning.
- The `OpenTelemetry.Api` 1.15.3 pin, which is the oldest version without GHSA-g94r-2vxg-569j.
