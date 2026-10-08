# Channel state across automatic recovery

Automatic recovery does not recover a channel. It builds a *new* inner channel and transplants
selected state onto it: `AutorecoveringConnection.RecoverChannelsAsync` calls
`AutorecoveringChannel.AutomaticallyRecoverAsync`, which calls
`AutorecoveringConnection.CreateNonRecoveringChannelAsync`, then `RecoveryAwareChannel.TakeOver`,
then replays a handful of settings by re-issuing AMQP methods.

Anything the application set on the channel and that is *not* in that transplant list is silently
discarded. That is issue #2031, and it is a class of bug rather than a single one, so this records
the whole inventory rather than only the two members that were fixed.

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
| confirms enabled / tracking / rate limiter, `TracingOptions` | yes | re-derived from the same `CreateChannelOptions` |
| `_nextPublishSeqNo`, outstanding confirm TCSs, `_flowControlBlock`, `_closeReason`, `_rpcSemaphore`, `_continuationQueue` | reset, correctly | the broker resets them too |
| **`CurrentQueue`** | **no** | nothing carries it - see below |
| `basic.qos` `prefetchSize` | no | `AutorecoveringChannel` records only `prefetchCount` and replays `0` |
| `basic.consume` `noLocal` | no | not stored on `RecordedConsumer`; re-issued as `false` |

The last two are unreachable rather than fixed, and both were checked against a RabbitMQ 4.3 broker:
a non-zero `prefetchSize` is rejected outright (`540 NOT_IMPLEMENTED - prefetch_size!=0`), so no value
can exist to lose, and the broker ignores `noLocal`, so re-issuing `false` changes nothing.
`AutorecoveringChannel._recordedConsumerTags` can look stale after a consumer-tag change; it is not,
because `RecordedConsumer.RecoverAsync` re-sends the explicit tag and the broker returns the same one.

## `CurrentQueue` is lost, and is deliberately not fixed

`IChannel.CurrentQueue` is a public getter, set by `queue.declare`, that enables the
empty-queue-name convenience on `basic.consume` and friends. `TakeOver` does not carry it, so it is
null after a recovery. Measured:

```
my queue            = amq.gen-7n8G91nWBbcWg5DyQ4d2Vg
CurrentQueue before = amq.gen-7n8G91nWBbcWg5DyQ4d2Vg
CurrentQueue after  = <null>
basic.consume(queue:"") after recovery
   -> 404 NOT_FOUND - no previously declared queue   (channel closed)
```

`RecordedConsumer` also reads `_channel.CurrentQueue ?? string.Empty`, so the null would be recorded
as `""` if the broker ever accepted the consume.

Carrying it in `TakeOver` is a one-line change and was *not* made, because it is only right for
named queues. For a server-named queue, topology recovery may obtain a **different** name, and that
new name is known to the connection's recorded entities rather than to the channel - so carrying the
old string would replace a null that fails loudly with a stale name that fails just as hard while
looking valid. Fixing this properly means sourcing the name from the recorded queue, which is a
different change from #2031. It fails loudly today, which caps the cost of leaving it.

## Two non-obvious choices in the #2031 fix

**`ContinuationTimeout` is captured in the constructor and replayed unconditionally.** Replaying only
an explicit assignment - so an untouched channel "keeps following the connection config" - is the
obvious conservative design and is wrong. `CreateChannelOptions.CreateOrUpdate` mutates the caller's
own options instance **in place** via `WithConnectionConfig`, on every `CreateChannelAsync`. One
options object shared across two connections therefore ends up holding the second connection's
config, and a channel on the first connection that never touched the property adopts it at recovery
time. Measured: a channel reading 30 s before recovery and 7 s after, having set nothing.
`TestRecoveryDoesNotAdoptAnotherConnectionsTimeout_GH2031` pins it. The in-place mutation in
`CreateChannelOptions` is pre-existing and worth its own issue.

The field is `long` ticks behind `Volatile`, not a `TimeSpan?`. `Nullable<TimeSpan>` is 16 bytes and
reads as two loads of two sub-fields, the property setter holds no lock, and recovery reads it from
another thread - so a race with the first assignment could observe `HasValue == true` with
`Value == default`. `AsyncRpcContinuation.StartTimeout` does `CancelAfter(timeout)` with no
validation, so `TimeSpan.Zero` would complete every RPC on the recovered channel as cancelled until
the next recovery. The other replayed fields are a `bool` and two `ushort`s, all atomic, which is why
this one is the only field in that set needing care.

**The replay sits *after* recovery's own `basic.qos` and `tx.select`, not before.** Each RPC builds
its continuation from `ContinuationTimeout` at issue time, so moving the replay earlier lets a
deliberately short application value break recovery itself - measured: with a one-tick timeout and a
prefetch to replay, recovery times out and the channel never reopens. The position is therefore
load-bearing, and it leaves a real split: the application's value governs from that point on,
including consumer recovery, while recovery's own prefetch and transaction replay ran under the
connection's value, and `channel.open` / `confirm.select` ran earlier still inside
`CreateNonRecoveringChannelAsync`. A user who *raised* the timeout for a slow broker still gets the
default for those. Narrowing that needs the value plumbed into channel creation.

**`DefaultConsumer` is remembered on the wrapper, not carried in `TakeOver`.** `TakeOver` reads the
old channel's dispatcher before the new channel is installed, and the window between those two spans
recovery's `basic.qos` x2 and `tx.select` - up to a `ContinuationTimeout` of wall clock. An
assignment landing in that window would write to the channel about to be discarded, with no copy
anywhere, and be lost permanently rather than for one cycle. Keeping both members on the wrapper also
keeps `TakeOver` and `DropTakenOverHandlers` symmetrical: the latter exists to undo a takeover on a
channel abandoned before installation (#1988), and state carried by `TakeOver` but not dropped there
is a trap for whoever adds the next one.

## What the tests do and do not pin

`projects/Test/Integration/ConnectionRecovery/TestChannelStateRecovery.cs`. All four tests fail
individually under the obvious wrong implementations, including the conditional-replay design above.

`TestDefaultConsumerSurvivesRecovery_GH2031` is an **identity** assertion: it pins that the reference
survives, not that the dispatcher consults it. Producing a genuinely unmatched consumer tag against a
live broker is awkward, so the delivery path itself is uncovered. The failure mode the issue
describes - an unmatched delivery reaching `FallbackConsumer` - is therefore reasoned, not tested.
Note that mode is also less dramatic than it first looks: with `autoAck: true` the message is gone,
but under manual ack it stays unacked and is requeued when the channel closes, so delivery stalls
rather than vanishing.
