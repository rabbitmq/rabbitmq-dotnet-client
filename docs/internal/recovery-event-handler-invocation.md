# When channel recovery event handlers run, and why it matters

Read this before moving, wrapping, or adding a user callback inside automatic recovery.

## The rule

`AutorecoveringConnection` holds `_recordedEntitiesSemaphore` across the whole of topology recovery, and **user code that can reach a recording operation must never run while it is held**. Recording an entity re-acquires that same semaphore, so a callback that reaches one waits for a holder that is waiting for the callback.

**The operative property is that no caller can supply a token to that wait, not that the wait is untokened.** Five recording methods in `AutorecoveringConnection.Recording.cs` take no token at all - `RecordExchangeAsync`, `RecordBindingAsync`, `RecordConsumerAsync`, `DeleteRecordedConsumerAsync`, `DeleteRecordedChannelAsync`. The other five accept one, but it arrives from the public channel method the callback called, where it defaults to `default`. Both halves hang identically and permanently, because a token that defaults to `default` is no token: a handler calling only `QueueDeclareAsync` reaches the token-taking `RecordQueueAsync` and waits just as uncancellably as one calling `ExchangeDeclareAsync`. So fixing only the five untokened waits would not have addressed #2038.

`Recovery.cs` follows the rule for **every asynchronous callback it invokes itself**: the four topology recovery exception handlers, `QueueNameChangedAfterRecoveryAsync`, `RecoveringConsumerAsync` and `ConsumerTagChangeAfterRecoveryAsync` each release the semaphore, invoke, and re-acquire in a `finally`; `ConnectionRecoveryErrorAsync` is outside it entirely; `RecoverySucceededAsync` fires after the `finally` that releases it. The one that missed the rule was the channel `RecoveryAsync` event, because its invocation lived in another class, two frames down: `RecoverChannelsAndItsConsumersAsync` -> `AutorecoveringChannel.AutomaticallyRecoverAsync` -> `Channel.RunRecoveryEventHandlers`.

**Eight synchronous user delegates still run with the semaphore held**, and that is the remaining exposure: the four `TopologyRecoveryFilter` predicates and the four `TopologyRecoveryExceptionHandler` conditions, all public settable `Func<>` properties. Note the exception *conditions* are evaluated under the semaphore while the exception *handlers* immediately after release it. Being synchronous they cannot `await` a recording call, so reaching the deadlock needs sync-over-async such as `.GetAwaiter().GetResult()` - narrower than the `RecoveryAsync` hole, but the same hang.

## What that cost (issue #2038)

A handler that declared topology, registered or cancelled a consumer, or closed its channel deadlocked recovery permanently. The blast radius was worse than a stalled recovery: the recovery task never returned, so the semaphore was never released, and it guards *all* recorded-entity access rather than just recovery. Measured after a handler wedged: `IsOpen` still `true`, `RecoverySucceededAsync` never fired, and `ExchangeDeclareAsync` and `QueueDeclareAsync` on brand-new channels still blocked when the probe gave up. That the block is permanent rather than merely long follows from the wait accepting no token the caller can set. Disposal is not an escape either, and not for the reason it looks: `AutorecoveringConnection` deliberately never disposes these semaphores, because disposing a `SemaphoreSlim` leaves an already-parked waiter pending forever rather than faulting it. Shipped in every 7.x release from `v7.0.0` to `v7.2.2`.

The trigger set was every one of the 14 sites in `AutorecoveringChannel` that pass `recordedEntitiesSemaphoreHeld: false` - all exchange, queue and binding declares, deletes, binds and unbinds, plus `BasicConsumeAsync`, `BasicCancelAsync` and the two channel `CloseAsync` call sites. Re-declare my topology and re-attach my consumer are the two most plausible things to do from a recovery handler, and both were in it.

## Current design

`RecoverChannelsAndItsConsumersAsync` returns the channels that recovered. `TryPerformAutomaticRecoveryAsync` invokes their handlers after the `finally` releases the semaphore and before `RecoverySucceededAsync`, via `AutorecoveringChannel.RunRecoveryEventHandlersAsync`, which skips a disposed channel and resolves `_innerChannel` at call time so it always finds the post-swap channel with the handlers `TakeOver` carried across.

Consequences, all deliberate:

- **Handlers fire once every channel has recovered, not interleaved per channel.** Interleaving offered little: consumers on already-recovered channels are delivering by then, so a handler could not get ahead of traffic, and the things most worth doing with an earlier notification were the ones that deadlocked. Publishing, acking and `BasicQosAsync` were always safe from a handler and are merely slightly later now.
- **Every channel's consumers are recovered before any handler runs**, which is stronger than the per-channel ordering the old arrangement gave. Two caveats: consumer recovery is skipped entirely when `TopologyRecoveryEnabled` is false, and a `ConsumerFilter` can exclude individual consumers.
- **If a later channel fails with an exception, the earlier channels' handlers are not invoked for that attempt.** The exception propagates out before the loop, recovery retries, and every handler fires on the attempt that succeeds - rather than announcing recovery for an attempt that did not complete. A channel that merely returns `false`, which happens only when it is disposed, does not suppress anything.
- **A channel closed but not disposed during the loop still gets its handler invoked.** `CloseAsync` does not set `_disposed`, so the guard does not catch it; the handler runs against a closed inner channel and any topology call it makes throws `AlreadyClosedException` into `CallbackExceptionAsync`. This became reachable only with this change, since closing another channel from a handler previously deadlocked.

Unchanged: handler exceptions go to `CallbackExceptionAsync` rather than aborting recovery, because `AsyncEventingWrapper.InternalInvoke` routes them to a non-null `_onException`, and `OperationCanceledException` is swallowed outright.

One asymmetry worth knowing: calling the *channel* `CloseAsync` from a handler is now fine, but the *connection* `CloseAsync` stalls for `RequestedConnectionTimeout` and then completes, because the handler runs on the recovery task and `StopRecoveryLoopAsync` waits on that task. Pre-existing and unrelated to the semaphore.

## If you add a callback here

Invoke it outside `_recordedEntitiesSemaphore`, and prefer after recovery completes over release-and-re-acquire - PR #2012 exists partly because that pattern has its own sharp edge, where a cancelled re-acquire escapes without the semaphore. If the callback is a synchronous predicate, it joins the eight above; say so rather than assuming synchronous means safe. Note also that `IRecoverable` is public shipped API with real dependents: `EasyNetQ` pattern-tests every channel against it and throws `NotSupportedException` when the test fails, so it cannot be removed, and it publishes its own event from inside the handler.
