# Connection and channel architecture

How a connection, its sessions and its channels are put together, where their state lives, and the gaps that structure produces. The last section is the plan for 7.4.0; prune it as each step ships.

## Objects and their jobs

Paths are under `projects/RabbitMQ.Client/Impl/`.

- `Connection` owns the socket through `SocketFrameHandler`, runs the receive loop (`Connection.Receive.cs`), the heartbeat timers (`Connection.Heartbeat.cs`) and channel 0.
- `SessionManager` allocates channel numbers and maps a number to its `ISession`.
- `ISession`, implemented by `SessionBase`, `Session` and `MainSession`, is the per-channel framing layer: `Session` assembles frames into commands with a `CommandAssembler` and hands each one to its owner through the `CommandReceived` delegate, and `SessionBase.TransmitAsync` serializes outgoing commands. `MainSession` is channel 0, with its own quiescing flags for a connection close.
- `Channel` implements the protocol: RPCs, publish, publisher confirmations, and the consumer dispatcher.
- `AutorecoveringConnection` and `AutorecoveringChannel` wrap the above, record topology (`AutorecoveringConnection.Recording.cs`) and replay it on a new inner connection and channel.

`ISession` has no counterpart in AMQP 0-9-1. It arrived in the 2008 initial import, whose design notes (`docs/wikipages/data.SessionApi.txt` in e79cde291) expected AMQP 0-10's transport-independent sessions. That line of the protocol became AMQP 1.0, which RabbitMQ serves through a separate client (`rabbitmq-amqp-dotnet-client`), so this client will never need it. Its other early job, swapping a closing channel's session for a `QuiescingSession`, was removed in a5d43bd7f. The Java client keeps the same responsibilities in one class per channel (`AMQChannel`, extended by `ChannelN`), and the Go client in `Channel` itself.

## Where close state lives

Each layer records its own:

- `Connection._closeReason` (first cause wins, `SetCloseReason`) and `Connection._closed`.
- `SocketFrameHandler._closed`.
- `MainSession._closing` and `_closeIsServerInitiated`, for channel 0.
- Per channel, both `Channel._closeReason` and `SessionBase._closeReason`.

The two per-channel reasons are not duplicates: together they encode closing and closed. `Channel.CloseAsync` sets the channel's reason and then sends `channel.close` through the session (`Channel.cs`, the `SetCloseReason(args)` block), which works because the session still reports open. The session's reason is set when `close-ok` arrives. The hazard is that each check reads whichever layer is nearest: `BasicPublishCoreAsync` checks only the session, so a publish racing a close follows `channel.close` onto the wire (#2102), and `SessionBase.TransmitAsync` does not read the connection's reason, so an RPC can reach a quiescing connection (#2111).

## Shutdown paths

Every path takes the connection's close reason through `SetCloseReason`, so the first cause wins. What each does next is its own:

- The receive loop's catch blocks call `HandleMainLoopExceptionAsync`, which cancels the main-loop token before running handlers (#1960) and records the cause for `CreateConnectionAsync` (`MaybeSetConnectionStartException`).
- A `HardProtocolException` goes to `HardProtocolExceptionHandlerAsync`, which does neither (#2109, #2110), sends `connection.close` and waits in `ClosingLoopAsync` without a time limit (#2113).
- A broker `connection.close` goes to `ClosedViaPeerAsync`.
- An application close or abort goes to `Connection.CloseAsync`, which on a timed-out close falls back to closing the socket (#2022).
- The heartbeat read timer calls `HandleMainLoopExceptionAsync` and `FinishCloseAsync` itself.
- A failed write loop completes the writer (#1930) and, with #2122, closes the socket so the receive loop takes the first path.

Teardown then reaches channels through events rather than calls: `Connection.OnShutdownAsync` awaits `PreConnectionShutdownAsync`, to which every `SessionBase` subscribes, and then the public `ConnectionShutdownAsync`; each session raises `SessionShutdownAsync`, and `Channel.OnSessionShutdownAsync` sets the channel's reason and runs the application's `ChannelShutdownAsync` handlers before the dispatcher forgets its consumers. The order is the order of subscription, which is how #2005 and the closed-channel consume in #2081 arose.

## RPCs, timeouts and cancellation

A channel runs one RPC at a time under `_rpcSemaphore`. Each RPC creates a continuation with its own `ContinuationTimeout` linked to the caller's token, enqueues it in `RpcContinuationQueue`'s single slot (`_outstandingRpc`), sends, and awaits it. When the wait is cancelled or times out, the caller records the expected reply in `_timedOutRpcs` (at most 128 entries) and the late reply is later discarded by `ShouldIgnoreCommand`, which matches the oldest record by command type (#1802, #1964).

The broker still performs a timed-out RPC, so discarding its reply leaves the client's view behind the broker's: a late `basic.consume-ok` leaves a consumer the dispatcher never learns (#2117, and #1993 for recovery), and a late `basic.cancel-ok` is matched by order although RabbitMQ sends it from the queue process and a reply the channel sends itself can overtake it (#2116, `rabbit_channel.erl` `cancel_consumer/4`). A timed-out or `noWait` cancel removes the consumer before the broker has stopped delivering to it (#2115). A reply that fails to parse used to leave its caller waiting forever because the continuation was already dequeued (#2076).

Cooperative cancellation was added in the move to async in 7.0. The caller's token, the continuation timeout, the main-loop token and the shutdown tokens are linked per call site, and there is no stated contract for what cancelling leaves behind.

## The receive loop

The receive loop reads frames, dispatches them, and sends some replies itself. It must not wait on anything the peer or the application controls, and today it can: a reply sent from it can wait for room on a full write queue (#2096), and a `ChannelShutdownAsync` handler that calls an RPC during the application's `CloseAsync` deadlocks both (#2118). A peer that closes in the middle of a frame makes the frame reader spin (#2106).

## Recovery records

`AutorecoveringConnection` keeps one connection-wide dictionary of recorded consumers, keyed by tag alone, although tags are per channel (#2119). `AutorecoveringChannel` updates that record and the inner channel in different orders for consume and cancel, so they can disagree (#2121). Event subscriptions made during recovery or an outage are lost (#2094), and disposal racing recovery can leak a broker channel (#2020).

## Plan for 7.4.0

The plan replaces close reasons used as state with explicit state machines for the connection and each channel, modeled on the AMQP 0-9-1 lifecycle. A close reason becomes data carried by a transition, not the thing every check reads. RabbitMQ is built this way already: `rabbit_reader` routes frames differently once a connection is stopping, handling only methods on channel 0 and ignoring other channels (`rabbit_reader.erl`, the `?IS_STOPPING` clauses of `handle_frame`).

The Java client, the reference AMQP 0-9-1 implementation, does not model states either. It uses the same pattern as this client: `isOpen()` is `shutdownCause == null` (`ShutdownNotifierComponent`), and `AMQChannel.quiescingTransmit` and `quiescingRpc` skip the open check so `close` can be sent after the cause is set. Protocol states for an AMQP 0-9-1 client are therefore new ground, with no reference implementation to compare against.

The same model is being introduced in pika's recovery design (pika/pika#1714, `design/connection-recovery/concepts.md`), which uses the lifecycle states below and delivers every transition to listeners once and in order (its I-9). A flaw found in the tables or transitions while doing this work applies to both libraries and is raised on both.

### Protocol states

Checked against RabbitMQ at 3d7dec5982 (`rabbit_reader.erl`, `rabbit_channel.erl`), which this page treats as the definitive broker. The spec rule for both closes: "After sending this method, any received methods except Close and Close-OK MUST be discarded. The response to receiving a Close after sending Close must be to send Close-Ok" (`projects/specs/amqp0-9-1.xml`, `connection.close` and `channel.close`).

The connection, on channel 0:

| State | Entered on | May send | Expected |
| --- | --- | --- | --- |
| AwaitingStart | protocol header sent | nothing | `connection.start`; a protocol header then socket close if the version is refused |
| AwaitingSecureOrTune | `connection.start-ok` sent | `connection.secure-ok` | `connection.secure`, any number of times; `connection.tune`; `connection.close` 403 when authentication fails, after which the broker drops the socket without waiting for close-ok |
| AwaitingOpenOk | `connection.tune-ok` and `connection.open` sent | heartbeats | `connection.open-ok`; `connection.close` for a refused open or tune, answered with close-ok; heartbeats |
| Open | `connection.open-ok` received | `connection.close`, `connection.update-secret`, heartbeats; nothing else on channel 0 | `connection.close`; `connection.blocked` and `connection.unblocked` if the capability was advertised; `connection.update-secret-ok` in reply; heartbeats |
| ClosingByClient | `connection.close` sent | heartbeats, and `connection.close-ok` in reply to a crossing close | traffic on every channel until the broker has drained them; then `connection.close-ok`, or a `connection.close` in its place after a hard error during the drain |
| ClosingByPeer | `connection.close` received | `connection.close-ok`, heartbeats | heartbeats; a `connection.close-ok` if the client's own close crossed |
| Closed | close-ok exchanged, the socket closed, or the transport lost | nothing | nothing |

Rules the table does not show:

- RabbitMQ answers any other method on channel 0 in Open with `connection.close` 504 ("unexpected method in connection state"). It never sends `connection.update-secret`.
- A client `connection.close` sent during the handshake is never answered with close-ok: the broker treats it as unexpected and drops the socket, or in AwaitingOpenOk sends `connection.close` 504. Abandoning a handshake means closing the socket, so every handshake state has a transition to Closed on a client socket close, and none lists `connection.close` as sendable.
- The broker drops the socket without a close frame on a heartbeat timeout in any state but Closed, and in every handshake state on its `handshake_timeout` (10 seconds by default). Heartbeats go on channel 0 only; one on another channel is a 505 hard error.
- In ClosingByClient a blocked connection stops being read by the broker, so close-ok may never come. The state needs a time limit that falls back to closing the socket.
- In ClosingByPeer the broker closes the socket once it reads close-ok, or after min(heartbeat, 30 seconds). The Java client waits up to 10 seconds for that (`AMQConnection`).
- Closed carries its cause, because recovery depends on it: an application close, a close by the library, a peer close with its reply code, transport loss or a heartbeat timeout, or a handshake failure.

Each channel:

| State | Entered on | May send | Expected |
| --- | --- | --- | --- |
| Opening | `channel.open` sent | nothing | `channel.open-ok`; `channel.close`, which the spec allows but RabbitMQ never sends here, because a refused open is a hard error that arrives as `connection.close` on channel 0 |
| Open | `channel.open-ok` received | any method or content for the channel | any method or content for the channel; `basic.cancel` only with `consumer_cancel_notify` |
| ClosingByClient | `channel.close` sent | `channel.close-ok` in reply to a crossing close | deliveries, which the broker does not stop, and are discarded; `channel.close`, answered with close-ok; `channel.close-ok` |
| ClosingByPeer | `channel.close` received | `channel.close-ok` | deliveries until the client's close-ok, discarded and not acknowledged; a `channel.close-ok` if the client's own close crossed |
| Closed | close-ok exchanged, or the connection closed | nothing | nothing |

Rules the table does not show:

- While closing, the broker suppresses replies, `basic.cancel` and confirms, but not deliveries (`rabbit_channel.erl`, `send/2` and `send_reply` against the ungated `handle_deliver0`). Discarded deliveries are safe because the broker requeues unacknowledged messages when the channel ends.
- Publisher confirms do not survive a close: no ack or nack follows the broker's close, and an ack in flight is discarded by the spec rule. On Closed every outstanding confirm fails as outcome unknown, so a failed publish may have been routed, and republishing it can duplicate.
- A channel number is free again once close-ok is sent or received. A `channel.open` sent before that reaches the old channel and fails with "second 'channel.open' seen" 504, which closes the connection.
- RabbitMQ sends connection-class methods only on channel 0 (`rabbit_reader:send_on_channel0`), so one arriving on another channel is a protocol error rather than something to dispatch (#2114). The broker itself answers one received on a non-zero channel with a 503 or 504 that closes the connection.
- RabbitMQ never sends `channel.flow` and rejects `channel.flow active=false` with 540, so Open needs no flow substate. `connection.blocked` is likewise a flag on Open, not a state.

### Lifecycle states

What an application sees, owned by `AutorecoveringConnection` and `AutorecoveringChannel`: Opening, Open, Recovering, Closing and Closed, as in the pika design. These have a precedent: RabbitMQ's AMQP 1.0 Java client exposes the same five as `Resource.State`, with a `StateListener` that receives the previous state, the new state and the failure cause, and `ResourceBase` performs each transition through an `AtomicReference<State>` and dispatches it on an executor. Today this client has no Recovering state; callers infer it from `IsOpen` and the recovery events, and exposing one is new public API that needs its own decision. pika chose to report Open only after replay (its D-7), which this client does not do today, and changing that here would be visible to users.

The two levels map as follows. Each recovery creates a new inner connection and channels, so a protocol machine never returns from Closed to Open; the handshake and channel Opening states belong to lifecycle Opening, or Recovering on a recovery attempt. ClosingByClient maps to Closing when the application asked for the close, but a close by the library, such as a protocol error, goes from Open straight to Closed (pika's D-3), so the protocol state records who initiated it. ClosingByPeer is a step inside Open → Recovering or Open → Closed. Closing an application connection during a reconnect cannot send `connection.close` mid-handshake, so it uses the socket-close transition above. A refused channel open closes the whole connection, so recovery that replays it would hit the same refusal again; the recovery policy has to treat that cause as final.

### Application callbacks

Every event handler, such as `ConnectionShutdownAsync`, `ChannelShutdownAsync`, `BasicAcksAsync`, `BasicReturnAsync` and `ConnectionBlockedAsync`, runs today on the receive loop, which waits for it before reading the next frame; only consumer deliveries go through the dispatcher. Two rules replace that:

- **A callback reports a state the client has already reached.** The protocol work of a transition is complete before application code hears of it: `close-ok` sent for the peer's close, received or timed out for the client's own, resources released and pending RPCs failed. A handler that calls back into the client then gets `AlreadyClosedException` instead of waiting on a close it is part of (#2118), so the client behaves the same whatever a handler does.
- **The receive loop never runs a callback.** Each channel queues its events on a single-reader `System.Threading.Channels` channel drained by one task, and the connection does the same for its own, so handlers run in frame order per channel and an exception goes to `CallbackExceptionAsync`. A slow handler then delays only its channel's later events, not reads or heartbeats (#2134). Events get their own queue, not the channel's delivery queue, so their order relative to deliveries is not guaranteed, as already with `ConsumerDispatchConcurrency` above 1. Moving handlers off the loop is a behavior change and needs a release note.

The Java client follows the first rule only. `ChannelN.asyncShutdown` sends `channel.close-ok`, releases the channel and fails outstanding RPCs before `notifyListeners()`, while return, confirm and blocked listeners run inline on its main loop. It moved the connection's own shutdown off the main loop separately (`AMQConnection.handleConnectionClose`, rabbitmq-java-client#91), and channel shutdown after a connection failure onto an optional `shutdownExecutor` (`ChannelManager.handleSignal`, rabbitmq-java-client#194).

### Steps

Each step is a series of small pull requests. A step that restructures code lands first as a change that preserves behavior, pinned by tests of what users see today, and its behavior fixes follow separately, each with a test shown to fail without it. Public API stays compatible.

1. **The state machines, in shadow.** Add both machines internally, driven by the frames sent and received, while the existing reasons and flags still decide behavior. Every transition checks that the old encoding agrees and reports a disagreement through `RabbitMqClientEventSource`. Run the full suites and a broker-version matrix against it. Nothing changes for users.
2. **Gates move to the state.** `TransmitAsync` asks the state what may be sent (#2102, #2111), and inbound frames are checked against the expected column (#2114). A frame that was silently mishandled before now closes the connection with a protocol error, which is a release note.
3. **One shutdown routine, driven by transitions.** Every path, the receive loop, a protocol error, a peer close, an application close or abort, a heartbeat timeout and a write-loop failure, requests a transition into Closing or Closed with its cause. One routine then cancels tokens, stops writes, sends `connection.close` within a time limit, closes the socket gracefully, closes channels by direct ordered calls instead of the event chain, and runs handlers with an already-cancelled token. Fixes #2109, #2110, #2113 and the remainder of #2096.
4. **A cancellation contract.** A token cancels only the caller's wait. A request that reached the wire keeps an entry until its reply or the channel's Closed, and the reply's side effects are applied when it arrives: a late consume-ok registers the consumer and cancels it, a late cancel-ok releases its tag. Replies that carry an identity, such as consume-ok and cancel-ok with their tag, are matched by it rather than by order. Fixes #2116, #2117 and the timed-out half of #2115, and replaces the guesswork in `_timedOutRpcs`.
5. **A receive loop that never waits on the peer or the application.** Replies it sends are queued without waiting, and application callbacks run on per-channel event queues after the transition they report, as described under Application callbacks. Fixes #2096, #2118 and #2134.
6. **One owner for recovery state, and the lifecycle states.** Records keyed by channel and tag, updated in the same critical section as the inner channel's operation (#2119, #2121), and recovery reported through Recovering.
7. **Remove the old encodings and `ISession`.** Delete the reason-as-state checks and `MainSession`'s flags. Port the tests that construct `TestSession` (17 call sites in six files in `projects/Test/Unit/`) to a real broker or the hostile peer, then fold `Session`, `SessionBase` and `MainSession` into `Channel` and `Connection`, keeping channel-number allocation as a plain helper.

### Testing

A real broker is the default peer, and Toxiproxy covers network faults. Neither can send what a broken or hostile peer sends, and several of this release's defects needed one: a deeply nested field table in `connection.start`, `connection.tune` on a non-zero channel (#2114), a close in the middle of a frame (#2106), a table whose declared length is wrong (#2112), and a cancel-ok overtaken by another reply (#2116), which needed the queue process paused with `rabbitmqctl eval`.

Two ways to provide a hostile peer, not mutually exclusive:

- **A scripted peer in this repository**, listening on a loopback port and speaking AMQP 0-9-1 over a real socket, so it needs no hook inside the client. Python with only the standard library is quick to write and is what the advisory reporter used; C# keeps the tests in one language, and the unit tests already start loopback `TcpListener` peers. Its cost is a hand-written subset of the protocol that has to stay honest.
- **A hostile mode in RabbitMQ**, as a test-only plugin rather than anything in the broker's core. It frames correctly by construction, and every client, Java and Go included, could test against it. It needs the server team and follows the broker's release cadence.
