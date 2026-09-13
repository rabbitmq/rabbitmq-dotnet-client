# Consumer dispatch concurrency

Notes on how the value reaches the consumer dispatcher, and the one input that used to break it.

## Where the value comes from

Three suppliers, resolved in `CreateChannelOptions.InternalConsumerDispatchConcurrency`:

1. `CreateChannelOptions.ConsumerDispatchConcurrency`, if the caller set it.
2. The owning connection's `ConnectionConfig.ConsumerDispatchConcurrency`, copied in by `CreateOrUpdate` at channel creation.
3. `Constants.DefaultConsumerDispatchConcurrency` otherwise.

Note that the public `CreateChannelOptions` constructor defaults its parameter to 1 rather than `null`, so level 2 is unreachable for anyone constructing options explicitly. That is deliberate; see #2027 for why changing it was rejected. Note #2027 also rewrites the member's own documentation, so the two need reconciling whichever merges second.

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

## Still open in this area

- Channel 0 inherits the factory value through the internal `CreateChannelOptions(ConnectionConfig)` constructor, so a factory set high gives channel 0 that many parked reader loops for a channel that can never carry a consumer. Measured as negligible per connection, but it is waste.
- `ContinuationTimeout` on the same type has the mirror-image hole: no initializer and no fallback, so an options object that skipped `CreateOrUpdate` yields `TimeSpan.Zero`, which means *immediate* timeout rather than infinite. Latent today because all three in-library `Channel` construction sites populate it: `Impl/Channel.cs`, `Impl/Connection.cs` (channel 0) and `Impl/RecoveryAwareChannel.cs`. `AutorecoveringChannel` is not a `Channel` and forwards to its inner channel instead.
- `AsyncDefaultBasicConsumer` is not thread-safe against its own callbacks at concurrency greater than one (#2033), and ordering is lost between work types, not only between deliveries.
