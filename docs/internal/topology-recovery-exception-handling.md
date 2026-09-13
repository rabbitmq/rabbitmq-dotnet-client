# Topology Recovery: Exceptions, Handlers, and Retry

This document covers what happens when recovering a single topology entity fails, why the two classification paths give different answers, and why the obvious fix for the difference does not work. Issues #1993 and #1995 are the history.

**Nothing here describes a change.** This is the behaviour as it stands, plus an account of one attempt to change it that was measured to make things worse and abandoned. Read the "Why the obvious fix does not work" section before proposing a fix for #1995.

## The two paths

`RecoverExchangesAsync`, `RecoverQueuesAsync`, `RecoverBindingsAsync` and `RecoverConsumersAsync` each wrap their per-entity work in a `try`/`catch`, and each catch has two branches:

- **No handler configured** (or its condition rejects this exception): `HandleTopologyRecoveryException` consults `ShouldRetryRecoveryAfter`, which treats every `OperationInterruptedException`, `TimeoutException`, and a non-cancelled `OperationCanceledException` as a connectivity problem and rethrows, failing the whole attempt so it is retried. Anything else is logged and the entity is skipped.
- **Handler configured**: the handler is awaited and the exception is then swallowed, so the classification added for #1993 is never consulted for anyone who installed a handler, and recovery reports success even when the entity was never recovered. **This is #1995, and it is still open.**

## Why the handler path is not just the no-handler path

`ShouldRetryRecoveryAfter` is deliberately coarse: every `OperationInterruptedException` means retry. That is right when nothing has been done about the failure, but it would force a full extra recovery cycle on the very case handlers exist for, a `precondition-failed` from redeclaring an entity with different arguments, which the handler has just repaired. Measured across the existing handler integration tests, reusing the coarse classification on the handler path cost roughly 62 seconds against 43 without it.

So a fix for #1995 cannot simply reuse the no-handler classification. It has to narrow the "do not retry" verdict, and the narrowing is where the subtlety is.

## Which refusals are actually final

Most channel-level refusals look final during recovery and are not:

- 405 `resource-locked` and 403 `access-refused` are what the broker answers while an **exclusive queue is still owned by the connection that just died**. `NetworkRecoveryInterval` defaults to 5 seconds, while the broker needs roughly two missed heartbeat intervals to reap that owner, so a client that reconnects promptly hits these routinely and a retry succeeds once the owner is gone.
- 404 `not-found` is transient whenever the missing entity is one the same pass has yet to declare, or has skipped, which a `TopologyRecoveryFilter` can also cause.

Treating any of them as final permanently drops the entity, and everything bound to it, while `RecoverySucceededAsync` still fires. 311 `content-too-large`, 312 `no-route` and 313 `no-consumers` are soft errors too, but they are `basic.publish` and `basic.return` codes that never close a channel during topology recovery.

Two further conditions have to hold, and neither is implied by the reply code:

- **The request must have been sent.** `AlreadyClosedException` derives from `OperationInterruptedException` and carries a close reason, so it can present a final-looking reply code while the operation was never transmitted. That entity is definitely un-recovered, so it must be retried. Note it is thrown both from `SessionBase` with the *channel's* reason and from `Connection` with the *connection's*; the retry verdict is the same for both.
- **The connection must still be usable.** The same codes appear on connection-level closes. This is also why `ShouldTriggerConnectionRecovery` has to special-case a peer-initiated `access-refused`.

## Why the obvious fix does not work

The attempt in #2015 was to run the classification after the handler, with "final" narrowed to 406 `precondition-failed` alone. It was measured to be a regression against `main` in two independent ways, and both are properties of the design rather than of that implementation.

**The shared consumer channel makes "final" the wrong axis.** Consumers recover on one shared channel (`channelToUse`), while exchange, queue and binding recovery each open a throwaway channel per entity. A refusal classified final on the consumer path closes the shared channel, and every consumer after it then fails with `AlreadyClosedException`, which forces a retry that reproduces the original refusal, forever, with `RecoverySucceededAsync` never firing.

The narrowing to 406 was justified by the claim that `basic.consume` is refused with 404, 403 or 405 and never with 406, so no consumer refusal could ever be classified final. **That claim is false.** Measured against RabbitMQ 4.3.4, `basic.consume` returns 406 `precondition-failed` in at least these ways:

- consuming a stream queue with `autoAck: true`, or without a prefetch set: `consumer prefetch count is not set for stream queue`
- an invalid `x-stream-offset` argument, on a stream queue or on a quorum queue
- an `x-priority` argument of the wrong type: `expected integer, got longstr`

So narrowing to 406 does not remove the livelock class, it selects the reply code that triggers it. Reproduced end to end: two autoAck consumers on one channel, the queue recreated out of band as a stream, then the connection killed. The branch never recovers.

**Read the `main` side of that comparison carefully, because an earlier revision of this document stated it as "`main` recovers in one attempt", and that is not what happens.** With a handler configured, `main` swallows the refusal without consulting the classification at all (`AutorecoveringConnection.Recovery.cs`, the handler branch in each entity loop), so it completes one attempt, skips the consumers it could not recover, and fires `RecoverySucceededAsync` anyway. The consumers are **gone**. That is #1995 itself. So the honest comparison is "silently loses the consumers once" against "never recovers and flaps forever" - the branch is still worse, because a livelock also denies service to everything else on the connection, but `main` is not a working baseline and must not be quoted as one.

**A handler cannot say "handled, do not retry".** The delegates return bare `Task`, so a handler that repairs the entity and returns looks identical to one that only logged. With the classification applied after the handler, a log-only handler on a non-406 refusal therefore fails the attempt every time, and since the retry loop has no cap the connection flaps every `NetworkRecoveryInterval` indefinitely. Measured: the branch was still rebuilding after 20 seconds and three attempts, where `main` completed one attempt (having skipped the entity, per the caveat above). No configuration restores skip-and-continue, because narrowing the handler's condition just routes the same exception to `HandleTopologyRecoveryException`, which rethrows for every `OperationInterruptedException` anyway.

Two things follow for whoever picks #1995 up:

- Fixing it well needs at least one of a **retry cap with backoff** and a **way for a handler to signal that it handled the failure**. Neither exists today.
- The verdict cannot be a pure function of the reply code, because the same code means different things depending on whether the channel is shared.

**The first of those is contested, and a fair reading of the evidence is that it is argued rather than measured.** The counter-argument is that skip-and-continue is already `main`'s behaviour on the handler path, so a fix that adds retries *only* for the narrow unknown-fate class - an `AlreadyClosedException` whose operation was never transmitted, where the entity is definitely un-recovered - would never retry a refusal a handler had seen, and so could not livelock on one. That is not obviously wrong. What makes it doubtful is the shared consumer channel again: a channel-level refusal closes that channel whatever the client classifies it as, so the sibling consumers behind it raise exactly that unknown-fate `AlreadyClosedException`, get retried, and meet the original refusal again. The livelock would then be reached through the narrow class rather than avoided by narrowing to it.

That reasoning has **not** been measured, unlike the two regressions above, so treat it as the reason to test a narrow fix rather than as a reason to reject one. Whoever picks #1995 up should build the shared-channel case first and find out.

## Consequences for handler authors

**This document is the only place these are written down.** An earlier revision said they were "recorded in the public XML docs on `TopologyRecoveryExceptionHandler`". They were, on the #2015 branch, and the revert in `258d28223` deleted them along with the code - so that sentence became false the moment it landed. There is no `<remarks>` on that type mentioning either point today.

- A handler must be **idempotent**. The attempt can be retried by the no-handler path on a later entity, so the handler can be invoked again for the same one.
- There is no way for a handler to report "handled, do not retry": the delegates return bare `Task`. Throwing from a handler forces a retry directly, which is the long-standing explicit way to ask for one.

Whether to restore them as XML documentation is worth deciding separately: both are contracts on a public type, and a handler author has no reason to read `docs/internal/`.

## Known gaps

- **#1995 itself is open**: a configured handler still bypasses the retry classification, so recovery can report success for an entity that was never recovered. Deferred past 7.3.0 because the two blockers above have to be settled first.
- A retried attempt re-declares server-named queues under fresh names, so each retry can leave the previous attempt's durable queue on the broker, bound and unconsumed.
- The retry loop in `RecoverConnectionAsync` has no attempt cap and no escalating backoff, so a deterministic failure that no handler can repair holds the connection in a rebuild loop every `NetworkRecoveryInterval`.
- There is no integration coverage of the retry branch at all. One attempt was abandoned as vacuous on the grounds that `ConnectionRecoveryErrorAsync` is raised only from the reconnection path and cannot observe a failed topology attempt; that is tracked as #2014. Note that the conclusion was too strong: a failed attempt is observable without that event, because `RecoverySucceededAsync` does not fire while the loop spins and a handler's invocation count rises per attempt, which is how the measurements above were taken.
