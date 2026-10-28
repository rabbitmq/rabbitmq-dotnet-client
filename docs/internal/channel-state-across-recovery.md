# Channel state across automatic recovery

Automatic recovery does not recover a channel. It builds a *new* inner channel and transplants selected state onto it: `AutorecoveringConnection.RecoverChannelsAsync` calls `AutorecoveringChannel.AutomaticallyRecoverAsync`, which calls `AutorecoveringConnection.CreateNonRecoveringChannelAsync`, then `RecoveryAwareChannel.TakeOver`, then replays a handful of settings by re-issuing AMQP methods.

Anything the application set on the channel and that is *not* in that transplant list is silently discarded. That is issue #2031, and it is a class of bug rather than a single one, so this records the whole inventory rather than only the two members that were fixed.

## What survives, and how

| State | Survives? | Mechanism |
|---|---|---|
| The seven event wrappers | yes | `Channel.TakeOver` |
| `ActiveDeliveryTagOffset` / `MaxSeenDeliveryTag` | yes | `RecoveryAwareChannel.TakeOver` |
| prefetch count, consumer and global | yes | re-issued `basic.qos` |
| `_usesTransactions` | yes | re-issued `tx.select` |
| `ContinuationTimeout` | yes | replayed from a wrapper field (#2031) |
| `DefaultConsumer` | yes | replayed from a wrapper field (#2031) |
| the dispatcher's consumer map | yes | repopulated by consumer recovery |
| confirms enabled / tracking / rate limiter, `TracingOptions` | yes | re-derived from the `CreateChannelOptions` the wrapper stored |
| the consumer dispatcher's concurrency | yes | same stored options, through `Channel`'s constructor |
| `_nextPublishSeqNo`, outstanding confirm TCSs, `_flowControlBlock`, `_closeReason`, `_rpcSemaphore`, `_continuationQueue` | reset, correctly | the broker resets them too |
| **`CurrentQueue`** | **no** | nothing carries it - see below |
| `basic.qos` `prefetchSize` | no | `AutorecoveringChannel` records only `prefetchCount` and replays `0` |
| `basic.consume` `noLocal` | no | not stored on `RecordedConsumer`; re-issued as `false` |

The last two are unreachable rather than fixed, and both were checked against a RabbitMQ 4.3 broker: a non-zero `prefetchSize` is rejected outright (`540 NOT_IMPLEMENTED - prefetch_size!=0`), so no value can exist to lose, and the broker ignores `noLocal`, so re-issuing `false` changes nothing. `AutorecoveringChannel._recordedConsumerTags` can look stale after a consumer-tag change; it is not, because `RecordedConsumer.RecoverAsync` re-sends the explicit tag and the broker returns the same one.

## `CurrentQueue` is lost, and is not fixed here

`IChannel.CurrentQueue` is a public getter, set by `queue.declare`, that enables the empty-queue-name convenience on `basic.consume` and friends. `TakeOver` does not carry it, so it is null after a recovery. Measured:

```
my queue            = amq.gen-7n8G91nWBbcWg5DyQ4d2Vg
CurrentQueue before = amq.gen-7n8G91nWBbcWg5DyQ4d2Vg
CurrentQueue after  = <null>
basic.consume(queue:"") after recovery
   -> 404 NOT_FOUND - no previously declared queue   (channel closed)
```

`RecordedConsumer` also reads `_channel.CurrentQueue ?? string.Empty`, so the null would be recorded as `""` if the broker ever accepted the consume.

Carrying it in `TakeOver` is a one-line change and was *not* made, because it is only right for named queues. For a server-named queue, topology recovery may obtain a **different** name, and that new name is known to the connection's recorded entities rather than to the channel - so carrying the old string would replace a null that fails loudly with a stale name that fails just as hard while looking valid. Fixing this properly means sourcing the name from the recorded queue, which is a different change from #2031. It fails loudly today, which caps the cost of leaving it.

## Two non-obvious choices in the #2031 fix

**`ContinuationTimeout` is replayed unconditionally.** The wrapper's `_continuationTimeoutTicks` starts at the inner channel's creation value and the public setter overwrites it, so recovery replays whatever was last assigned, or the creation value if nothing was.

The alternative, replaying only an explicit assignment so that an untouched channel keeps following the connection config, was rejected because `CreateChannelOptions` wrote each connection's config into the caller's own options instance, which let a channel on one connection adopt another connection's timeout at recovery time. Measured then: the first connection's value before recovery and the second's after, having set nothing. #2034 removed that write, and the two designs now produce the same value in every case, because a connection's `ContinuationTimeout` cannot change and a channel always recovers onto the connection it was created on. The unconditional replay is kept for the simpler invariant, not because the alternative still breaks.

The field is `long` ticks behind `Volatile`, not a `TimeSpan?`. `Nullable<TimeSpan>` is 16 bytes and reads as two loads of two sub-fields, the property setter holds no lock, and recovery reads it from another thread - so a race with the first assignment could observe `HasValue == true` with `Value == default`. `AsyncRpcContinuation.StartTimeout` does `CancelAfter(timeout)` with no validation, so `TimeSpan.Zero` would complete every RPC on the recovered channel as cancelled until the next recovery. The other replayed fields are a `bool` and two `ushort`s, all atomic, which is why this one is the only field in that set needing care.

**The replay sits *after* recovery's own `basic.qos` and `tx.select`, not before.** Each RPC builds its continuation from `ContinuationTimeout` at issue time, so moving the replay earlier lets a short application value break recovery itself - measured: with a one-tick timeout and a prefetch to replay, recovery times out and the channel never reopens. The position is therefore what makes it work, and it leaves a real split: the application's value governs from that point on, including consumer recovery, while recovery's own prefetch and transaction replay ran under the connection's value, and `channel.open` / `confirm.select` ran earlier still inside `CreateNonRecoveringChannelAsync`. A user who *raised* the timeout for a slow broker still gets the default for those. Narrowing that needs the value plumbed into channel creation.

**`DefaultConsumer` is remembered on the wrapper, not carried in `TakeOver`.** `TakeOver` reads the old channel's dispatcher before the new channel is installed, and the window between those two spans recovery's `basic.qos` x2 and `tx.select` - up to a `ContinuationTimeout` of wall clock. An assignment landing in that window would write to the channel about to be discarded, with no copy anywhere, and be lost permanently rather than for one cycle. Keeping both members on the wrapper also keeps `TakeOver` and `DropTakenOverHandlers` symmetrical: the latter exists to undo a takeover on a channel abandoned before installation (#1988), and state carried by `TakeOver` but not dropped there is a trap for whoever adds the next one.

## Consumer tags across recovery (#2080)

Consumer recovery re-sends each recorded tag on the new inner channel, and `Channel.BasicConsumeAsync` throws `InvalidOperationException` without sending when the tag is already in that channel's dispatcher map. The broker treats a reused tag as a connection error, 530 `NOT_ALLOWED`, and `ShouldTriggerConnectionRecovery` recovers from it. Without the local check, an application that re-subscribes with its stored tag after a connection loss drives a loop that never ends: recovery restores the consumer, the application's consume closes the connection, and the record survives the failed call because `AutorecoveringChannel.BasicConsumeAsync` records only after consume-ok. This loop was reproduced on 5.2.0 and 6.6.0, which have the same recording design.

The check is only as accurate as the dispatcher map, so `BasicCancelAsync` removes the tag once its `basic.cancel` is on the wire, whether the cancel-ok arrives or the wait times out. A timed-out cancel-ok is discarded as a late reply, so without that removal the tag would stay in the map and the check would reject every later consume with it on a healthy channel. Removing it is safe because the broker handles a channel's frames in order, so a later `basic.consume` finds the tag released. A cancel whose send fails keeps the tag, because the broker still has the consumer. Deliveries that arrive between the timeout and the broker handling the cancel go to the fallback consumer. The check is skipped on a closed channel, because channel shutdown runs the `ChannelShutdownAsync` handlers before the dispatcher clears its map: a handler that re-subscribes after the broker closed the channel or the connection dropped gets `AlreadyClosedException`, as it did before the check.

Known gaps: the map can lag the broker, so a consume right after a broker-initiated cancel can be rejected locally although the broker would accept it (#2120); a consume whose consume-ok arrives after the timeout leaves a consumer the map never learns (#2117); and deliveries in flight after a `noWait` or timed-out cancel reach the fallback consumer (#2115).

The application's consume can also reach the new channel before recovery does. `_innerChannel` is installed before `RecoverConsumersAsync` runs, and `AutorecoveringChannel.BasicConsumeAsync` takes `_recordedEntitiesSemaphore` only after the broker answers, so it can register the tag while recovery holds the semaphore or has released it around a user callback. `RecoverConsumersAsync` therefore skips a tag the new channel already has. That consumer is live, and the application's `RecordConsumerAsync` replaces the record once recovery releases the semaphore.

The skip is checked before `RecoveringConsumerAsync` is raised and again after it. A handler may change `ConsumerArguments`, which is the dictionary the recovery `basic.consume` sends, so raising the event for a consumer recovery then skips would silently drop that change. The second check covers a handler that subscribes with the tag itself. For that consumer `RecoveringConsumerAsync` has fired and `ConsumerTagChangeAfterRecoveryAsync` does not, and the handler's argument changes are replaced by the arguments of the application's own consume.

Both checks read the dispatcher without `_rpcSemaphore`, and a tag is registered only on consume-ok, so they miss an application consume still waiting for its reply. Recovery's consume then queues on `_rpcSemaphore` behind it and fails the local check. Recovery treats that `InvalidOperationException` as the same skip when the tag is registered by then. The filter also requires that recovery's consume did not return, so an `InvalidOperationException` thrown after recovery's own consume-ok registered the tag is still reported. A `ConsumerTagChangeAfterRecoveryAsync` handler's exception never reaches this catch: it goes to `CallbackExceptionAsync`.

Recovery gives way to an application consume that reaches the new channel first, whatever its queue, which is why the skip matches on the tag alone. If the application consumes `(T, Q2)` while `(T, Q1)` is still recorded, recovery skips `(T, Q1)`, `DoRecordConsumer` replaces the record, and `T` consumes from Q2 from then on. Recovery does not report this. On the new channel the application's consume is the only live consumer with `T`, so the outcome is the application's choice, not a recovery failure. The consume can come from a consumer-recovery callback, or from another thread when the application re-subscribes on its own with topology recovery enabled. Outside that window the same consume throws, and the supported way to move a tag to another queue is to cancel it first.

## What the tests do and do not pin

`projects/Test/Integration/ConnectionRecovery/TestChannelStateRecovery.cs`, five tests. Each fails individually under the obvious wrong implementation of what it covers, with two exceptions, both on `ContinuationTimeout`. Nothing discriminates the conditional-replay design, and nothing can, since the two produce the same value. And `TestRecoveryDoesNotAdoptAnotherConnectionsTimeout_GH2031` fails only if both the copy and the replay are removed: either one alone still yields the creating connection's value. What discriminates #2034 here is `TestRecoveryDoesNotAdoptAnotherConnectionsConcurrency_GH2034`, which asserts a value nothing replays, confirmed by reverting the copy and watching that test alone go red.

`TestDefaultConsumerSurvivesRecovery_GH2031` is an **identity** assertion: it pins that the reference survives, not that the dispatcher consults it. Producing an unmatched consumer tag against a live broker is awkward, so the delivery path itself is uncovered. The failure mode the issue describes - an unmatched delivery reaching `FallbackConsumer` - is therefore reasoned, not tested. Note that mode is also less dramatic than it first looks: with `autoAck: true` the message is gone, but under manual ack it stays unacked and is requeued when the channel closes, so delivery stalls rather than vanishing.
