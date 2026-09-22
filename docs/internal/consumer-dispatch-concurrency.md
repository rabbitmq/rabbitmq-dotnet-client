# Consumer dispatch concurrency

Notes on how the value reaches the consumer dispatcher, and the one input that used to break it.

## Where the value comes from

Three suppliers, resolved in `CreateChannelOptions.InternalConsumerDispatchConcurrency`:

1. `CreateChannelOptions.ConsumerDispatchConcurrency`, if the caller set it.
2. The owning connection's `ConnectionConfig.ConsumerDispatchConcurrency`, copied in by `CreateOrUpdate` at channel creation.
3. `Constants.DefaultConsumerDispatchConcurrency` otherwise.

Note that the public `CreateChannelOptions` constructor defaults its parameter to 1 rather than `null` and assigns it unconditionally, so options constructed explicitly never reach level 2 *by default* - passing `consumerDispatchConcurrency: null` does reach it, and is the only way to. That is deliberate rather than an oversight, and #2027 settled it.

**Why the constructor default cannot simply be changed to `null`.** Measured rather than argued, because the intuitive answer is wrong twice over. It is not a compile-time break: C# bakes an optional parameter's default into the *caller's* assembly, so the change compiles everywhere and nothing fails to build. What it does is split behaviour by rebuild - an application that upgrades the package without recompiling keeps 1, one that recompiles silently switches to inheriting - and turn a previously safe `options.ConsumerDispatchConcurrency.Value` into a runtime `InvalidOperationException`. The mechanical signal is that the default is recorded in `PublicAPI.Shipped.*.txt` *by value*, so changing it trips the public-API analyzers rather than passing unnoticed. Overloads are worse, not better: a shorter constructor or a second one both make `new CreateChannelOptions(true, true)` ambiguous (CS0121), and appending a parameter to the existing constructor is a binary break (`MissingMethodException`) for every caller that does not recompile. `[CallerArgumentExpression]` *can* distinguish an omitted argument from an explicit `1`, but only via one of those two undeployable shapes. A purely additive static factory or fluent `With…` method is the one option that breaks nothing, and is where to start if this is revisited.

**The same divergence exists on `OutstandingPublisherConfirmationsRateLimiter`, and it matters more there.** Its field initializer is `new ThrottlingRateLimiter(128)` while the constructor parameter defaults to `null`, also assigned unconditionally - and `null` there disables rate limiting rather than choosing another value. So `new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true)` yields **no** limit on outstanding publisher confirmations while the member's own summary promises 128. #2027 documents both; whether the behaviour should change is open.

## Why zero was a bug (#2035)

`ConsumerDispatcherChannelBase`'s constructor builds one reader loop per unit of concurrency. At zero it allocated a zero-length task array, the loop body never ran, and `Task.WhenAll` over that empty array was already complete. The result:

- `BasicConsumeAsync` still returned a consumer tag, because the write to the unbounded work channel completes synchronously, and the broker began delivering.
- Nothing dispatched, so consumers appeared registered and never fired.
- Queued deliveries held their pooled buffers. A delivery takes ownership of a rented array via `TakeoverBody()`, and the only places that return it to `ArrayPool<byte>.Shared` are inside the reader loop and its drain-on-shutdown `finally`. **This is retention, not a leak in the unmanaged sense**: the arrays are ordinary GC-tracked `byte[]` and are reclaimed once the dispatcher and its work channel become unreachable. What is lost is pooling, plus unbounded growth for as long as the channel lives.
- Close looked clean, because `WaitForShutdownAsync` awaited an already-completed worker.

Zero was a legal `ushort` and unvalidated at every supplier: `ConnectionFactory.ConsumerDispatchConcurrency`, the `CreateChannelOptions` constructor, and `ConnectionConfig`.

## Where the guard lives, and why

In `ConsumerDispatcherChannelBase`'s constructor, not at the suppliers.

The invariant belongs to the dispatcher: it is the type whose loop count breaks. Guarding at the options layer would leave every other construction path unprotected, and one already exists — `projects/Benchmarks/ConsumerDispatching/ConsumerDispatcher.cs` constructs `new AsyncConsumerDispatcher(null, Concurrency)` directly, bypassing `CreateChannelOptions` entirely.

The floor is `InternalConstants.MinConsumerDispatchConcurrency`, deliberately separate from `Constants.DefaultConsumerDispatchConcurrency` even though both are 1. They answer different questions — "what do you get if you ask for nothing" versus "below what can the dispatcher not function" — and sharing one constant would mean that raising the default silently raised every zero-configured deployment to the new value, costing it the in-order delivery guarantee.

Coerced rather than rejected: throwing would add a new exception to public setters and constructors that accept zero today.

`InternalConsumerDispatchConcurrency` deliberately does **not** coerce. It reports what was asked for, so the public field and the internal resolution agree; only the dispatcher applies the floor.

## Decided: no upper bound

`ushort` is the ceiling. A caller asking for 60000 gets 60000 reader loops per channel, and that is
their problem. The floor exists because zero is silently *broken* - it produces a dispatcher that
cannot work at all, from an input a config binder can hand you by accident. A large value is merely
expensive, does exactly what it says, and is not something an unset environment variable produces.
Do not add a ceiling without a new reason.

## Dropping work items at shutdown, and who owns the pooled body (#1988, #2039)

Each `Handle*Async` on `ConsumerDispatcherChannelBase` checks `_disposed`/`IsQuiescing` and then writes to the work channel, and the two are not atomic. `Dispose()` completes the channel, so a caller can pass the check, be preempted across that completion, and have `WriteAsync` raise `ChannelClosedException`. For a delivery that unwinds through `Channel.HandleCommandAsync`, which has no catch, into the connection's frame-receive loop - tearing down the whole connection rather than the one channel. That is the same failure the captured `_shutdownToken` was introduced to remove, reached by another route, so each site now drops the work item instead.

**Which two threads race, because the obvious answer is wrong.** All four writes are reached only from the serialized main loop - `Channel.HandleCommandAsync` via `session.CommandReceived`, or an RPC continuation's `HandleCommandAsync` - so they are never concurrent with each other. The token is not uniform across them either: the delivery and cancel sites get the main loop's token, while the two `*OkAsync` sites get an RPC continuation's linked token. Nor is the completer always an application thread: `Dispose()` runs on its caller's, `AutorecoveringChannel` disposes the replaced channel from the recovery task, and `Channel.OnSessionShutdownAsync` reaches `TryComplete()` on whichever thread drove the shutdown - the main loop for a broker or heartbeat close, the application thread for `CloseAsync`, since `Connection.OnShutdownAsync` has callers in both. The window exists wherever writer and completer differ.

**The pooled body is owned by the callee.** `Channel.HandleBasicDeliverAsync` passes it as `cmd.TakeoverBody()`, which clears `cmd.Body`, so `HandleCommandAsync`'s `finally { cmd.ReturnBuffers(); }` is a no-op for the body. Only `RentedMemory.Dispose()` returns the array to `ArrayPool<byte>.Shared`.

**The catch clauses cover the rare exits, not the common one.** Measured by counting all four exits of `HandleBasicDeliverAsync`: 3000 messages per channel, no prefetch limit, a 5 ms consumer, six ordinary `CloseAsync`/`DisposeAsync` rounds gave 4479 delivered, **2971 dropped by the `_disposed`/`IsQuiescing` guard**, and **zero** for the entry `ThrowIfCancellationRequested` and for *both* catch clauses. The guard case is not a race at all: `Channel.CloseAsync` calls `Quiesce()` before transmitting `channel.close`, so every delivery arriving between that point and `close-ok` takes it. Treat the exact split as one configuration rather than a rate; what did not vary is that the guard fires in the hundreds per close and the catches do not fire. Those two uncovered paths are **#2039**, deliberately not fixed with #1988.

**Why a cancelled token needs its own catch.** Measured against `System.Threading.Channels` on an unbounded channel: an already-cancelled token yields `TaskCanceledException`, not `ChannelClosedException`; and when the channel is completed *and* the token is cancelled, cancellation wins, so the `ChannelClosedException` handler does not run. That is a narrow corner rather than the ordinary teardown case - the entry throw has already returned, so it needs cancellation inside the few await-free instructions before the write, and once `_mainLoopCts` is cancelled the receive loop stops dispatching frames. The delivery path catches `OperationCanceledException` as defence in depth, returns the body, and rethrows. `TryWrite`, used by `ShutdownConsumer`, does not throw; it returns `false`.

**`WorkStruct.Dispose()` is not idempotent.** It is a readonly struct whose `RentedMemory` field is readonly, while `RentedMemory.Dispose()` is not declared readonly and mutates, so C# invokes it on a defensive copy and the `RentedArray = Array.Empty<byte>()` write-back is discarded. Confirmed on a minimal struct of the same shape: after one `Dispose` the field still referenced the original array, and after a second, two consecutive `ArrayPool<byte>.Shared.Rent` calls returned the same instance. One array, two owners - worse than the leak. Every caller must dispose a given work item exactly once; today the reader loop disposes what it drains and each drop site is reached by at most one of the mutually exclusive catch clauses. A bounded channel, a retry around the write, or a drain running alongside a drop path would each have to re-establish that.

## Disposing a dispatcher has to run the whole shutdown (#1988)

Disposal must release the worker, and completing the writer is the only thing that can: `ProcessChannelAsync` awaits `_reader.WaitToReadAsync()` with **no token**, so cancelling `_shutdownCts` cannot wake it. Without the completion the worker stays parked for the process lifetime, rooting the dispatcher, its channel and its session - reachable whenever a channel is disposed without its session having been shut down, for instance when an abort swallows a close that never got a `close-ok`.

**Completing alone is worse than not completing, though.** `ShutdownConsumer` enqueues each consumer's `Shutdown` work item with `_writer.TryWrite` and discards the result, so once the writer is completed a later `ConsumerDispatcher.ShutdownAsync` - which is exactly what `OnSessionShutdownAsync` runs when the socket finally drops - silently enqueues nothing. Every consumer is then left reporting no shutdown reason with `IsRunning` true, on a channel that is gone, and nothing surfaces it because the channel's own `ChannelShutdownAsync` event does not go through the dispatcher. This was a regression found by review, not a hypothetical.

So `Dispose` runs the whole of `ShutdownAsync(CloseReason)`. That works because `ShutdownAsync` is **not** an `async` method: `DoShutdownConsumers` and the `TryComplete` inside `InternalShutdownAsync` both run before it returns, so the notifications are queued ahead of the completion and the worker drains them on its way out - the same enqueue-then-complete order the session-driven path uses. The returned task is `_worker`, which `Dispose` deliberately does not await: it runs on the caller's thread, a consumer callback may be arbitrarily slow, and the worker is reachable through the field for anyone who needs to wait. `DoShutdownConsumers` clears the consumer collection, so a shutdown that already happened makes this a no-op rather than a duplicate notification.

The reason passed is the channel's own. `Channel.CloseAsync` sets it before transmitting `channel.close`, so it is published on every path reaching disposal, including an abort whose handshake never completed; the fallback covers a dispatcher built without a channel, which the unit tests do.

**`_shutdownCts` is deliberately not disposed**, for the same reason the channel does not dispose its semaphores (#1976). `Quiesce()` takes an early return when another caller has already set the quiescing flag, so a `Quiesce` racing a `Dispose` could dispose the source before `Cancel()` ran - leaving it disposed and never cancelled, so work items carry a token that can never fire and a consumer awaiting it hangs silently, while reading its `WaitHandle` throws. The source arms no timer and holds no library registrations, so there is nothing to reclaim.

`DisposeAsync` additionally waits, briefly, for those queued notifications to reach their consumers, which the synchronous path cannot do. It reuses `WaitForShutdownAsync` rather than writing a second wait, because that one already carries the `AggregateException` filtering #1751 needed; note it returns early once `_disposed` is set, which is why the wait happens before the `finally`. Bounded by `ConsumerDispatcherDrainTimeout` and best effort: expiry means a consumer callback is slow or stuck, which must not stop the channel being disposed.

## Still open in this area

- Channel 0 inherits the factory value through the internal `CreateChannelOptions(ConnectionConfig)` constructor, so a factory set high gives channel 0 that many parked reader loops for a channel that can never carry a consumer. Measured as negligible per connection, but it is waste.
- `ContinuationTimeout` on the same type has the mirror-image hole: no initializer and no fallback, so an options object that skipped `CreateOrUpdate` yields `TimeSpan.Zero`, which means *immediate* timeout rather than infinite. Latent today because all three in-library `Channel` construction sites populate it: `Impl/Channel.cs`, `Impl/Connection.cs` (channel 0) and `Impl/RecoveryAwareChannel.cs`. `AutorecoveringChannel` is not a `Channel` and forwards to its inner channel instead.
- `AsyncDefaultBasicConsumer` is not thread-safe against its own callbacks at concurrency greater than one (#2033), and ordering is lost between work types, not only between deliveries.
