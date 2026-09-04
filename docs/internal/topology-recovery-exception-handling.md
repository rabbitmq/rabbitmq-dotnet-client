# Topology Recovery: Exceptions, Handlers, and Retry

This document covers what happens when recovering a single topology entity fails, and why the two classification paths deliberately give different answers. Issues #1993 and #1995 are the history.

## The two paths

`RecoverExchangesAsync`, `RecoverQueuesAsync`, `RecoverBindingsAsync` and `RecoverConsumersAsync` each wrap their per-entity work in a `try`/`catch`, and each catch has two branches:

- **No handler configured** (or its condition rejects this exception): `HandleTopologyRecoveryException` consults `ShouldRetryRecoveryAfter`, which treats every `OperationInterruptedException`, `TimeoutException`, and a non-cancelled `OperationCanceledException` as a connectivity problem and rethrows, failing the whole attempt so it is retried. Anything else is logged and the entity is skipped.
- **Handler configured**: the handler is awaited, and then `ThrowIfHandledExceptionStillRequiresRetry` consults `ShouldRetryAfterHandledRecoveryException`. Before #1995 this branch simply swallowed the exception, so the classification added for #1993 was never consulted for anyone who installed a handler, and recovery reported success even when the entity was never recovered.

## Why the handler path is not just the no-handler path

`ShouldRetryRecoveryAfter` is deliberately coarse: every `OperationInterruptedException` means retry. That is right when nothing has been done about the failure, but it forces a full extra recovery cycle on the very case handlers exist for, a `precondition-failed` from redeclaring an entity with different arguments, which the handler has just repaired. Measured across the existing handler integration tests, reusing the coarse classification cost roughly 62 seconds against 43 on `main`.

So the handler path narrows the "do not retry" verdict, and the narrowing is where the subtlety is.

## Only precondition-failed is final

`BrokerRefusalIsFinal` returns true for exactly one reply code, 406 `precondition-failed`. The other channel-level refusals look final and are not:

- 405 `resource-locked` and 403 `access-refused` are what the broker answers while an **exclusive queue is still owned by the connection that just died**. `NetworkRecoveryInterval` defaults to 5 seconds, while the broker needs roughly two missed heartbeat intervals to reap that owner, so a client that reconnects promptly hits these routinely and a retry succeeds once the owner is gone.
- 404 `not-found` is transient whenever the missing entity is one the same pass has yet to declare, or has skipped, which a `TopologyRecoveryFilter` can also cause.

Treating any of them as final permanently dropped the entity, and everything bound to it, while `RecoverySucceededAsync` still fired, which made a connection with a handler installed **strictly worse** than one without. 311 `content-too-large`, 312 `no-route` and 313 `no-consumers` are soft errors too, but they are `basic.publish` and `basic.return` codes that never close a channel during topology recovery.

Two further conditions have to hold, and neither is implied by the reply code:

- **The request must have been sent.** `AlreadyClosedException` derives from `OperationInterruptedException` and carries the *channel's* close reason, so it can present a final-looking reply code while the operation was never transmitted. That entity is definitely un-recovered, so it must be retried.
- **The connection must still be usable.** The same codes appear on connection-level closes. This is also why `ShouldTriggerConnectionRecovery` has to special-case a peer-initiated `access-refused`.

## The shared consumer channel is why the narrowing matters

Consumers recover on one shared channel (`channelToUse`), while exchange, queue and binding recovery each open a throwaway channel per entity. So a refusal classified final on the consumer path would close the shared channel, and every consumer after it would then fail with `AlreadyClosedException`, which forces a retry that reproduces the original refusal, forever, with `RecoverySucceededAsync` never firing. Restricting "final" to `precondition-failed` removes that class of livelock structurally, because `basic.consume` is refused with 404, 403 or 405 and never with 406, so no consumer refusal is ever classified final.

## Consequences for handler authors

Recorded in the public XML docs on `TopologyRecoveryExceptionHandler`, and worth knowing here:

- A handler must be **idempotent**. The attempt can be retried, so the handler can be invoked again for the same entity.
- There is no way for a handler to report "handled, do not retry": the delegates return bare `Task`. Throwing from a handler still forces a retry directly, bypassing this classification entirely, which is the long-standing explicit way to ask for one.

## Known gaps

- A retried attempt re-declares server-named queues under fresh names, so each retry can leave the previous attempt's durable queue on the broker, bound and unconsumed.
- The retry loop in `RecoverConnectionAsync` has no attempt cap and no escalating backoff, so a deterministic failure that no handler can repair holds the connection in a rebuild loop every `NetworkRecoveryInterval`. This predates #1995 on the no-handler path.
- There is no integration coverage of the retry branch. An attempt to add one was removed as vacuous, because `ConnectionRecoveryErrorAsync` is raised only from the reconnection path and so cannot observe a failed topology attempt at all; that is tracked as #2014.
