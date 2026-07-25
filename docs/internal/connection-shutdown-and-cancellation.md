# Connection Shutdown, Channel 0, and Cancellation

This document captures the connection/channel shutdown model of the 7.x client,
with emphasis on the areas that are subtle enough to have produced real hangs
(notably issue #1921). It is written for maintainers who need to reason about
what happens when a connection open is cancelled or aborted, and how channel 0
gets torn down.

## The central fact: channel 0 is special

Every connection has a hidden channel/session pair on channel number 0
(`_channel0` / `_session0` in `Connection.cs`). It carries connection-level
methods (`connection.start`, `connection.tune`, `connection.open`,
`connection.close`), not application traffic.

The single most important and least obvious fact about shutdown:

> **Session 0 does not subscribe to `Connection.ConnectionShutdownAsync`.**

See `SessionBase` construction:

```csharp
// projects/RabbitMQ.Client/Impl/SessionBase.cs
protected SessionBase(Connection connection, ushort channelNumber)
{
    Connection = connection;
    ChannelNumber = channelNumber;
    if (channelNumber != 0)
    {
        connection.ConnectionShutdownAsync += OnConnectionShutdownAsync;
    }
    ...
}
```

Application channels (channel number != 0) are shut down automatically when the
connection shuts down, because their session is wired to
`ConnectionShutdownAsync`. That event handler chain ends in
`Channel.OnSessionShutdownAsync -> OnChannelShutdownAsync ->
_continuationQueue.HandleChannelShutdown(reason)`, which faults any pending RPC
continuation with an `OperationInterruptedException`.

Channel 0 has **no such wiring**. Its only shutdown path is
`Connection.FinishCloseAsync`:

```csharp
// projects/RabbitMQ.Client/Impl/Connection.cs
// Only call at the end of the Mainloop or HeartbeatLoop
private async Task FinishCloseAsync(CancellationToken cancellationToken)
{
    _mainLoopCts.Cancel();
    _closed = true;
    MaybeStopHeartbeatTimers();

    await _frameHandler.CloseAsync(cancellationToken).ConfigureAwait(false);
    _channel0.SetCloseReason(CloseReason!);
    await _channel0.FinishCloseAsync(cancellationToken).ConfigureAwait(false);
    RabbitMqClientEventSource.Log.ConnectionClosed();
}
```

`Connection.FinishCloseAsync` runs in exactly one place: at the end of
`MainLoop` (`Connection.Receive.cs`), after `ReceiveLoopAsync` returns or
throws. In other words:

> **If `MainLoop` never runs to completion, channel 0 is never shut down.**

## Why an un-shut-down channel 0 causes a hang

When a `Channel` (including channel 0) is disposed while still `IsOpen`, its
dispose path issues an abort:

```csharp
// projects/RabbitMQ.Client/Impl/Channel.cs  (DisposeAsyncCoreAsync)
if (IsOpen)
{
    await this.AbortAsync().ConfigureAwait(false);
}
```

`AbortAsync` funnels into `Channel.CloseAsync(ShutdownEventArgs, bool abort)`,
which enqueues a `ChannelCloseAsyncRpcContinuation` and then awaits it:

```csharp
var k = new ChannelCloseAsyncRpcContinuation(ContinuationTimeout,
            IsOpen ? CancellationToken.None : cancellationToken);
...
AssertResultIsTrue(await k);   // <-- waits here
```

`await k` completes when one of:

1. a `channel.close-ok` frame arrives (normal case), or
2. the continuation is faulted via `HandleChannelShutdown` (channel shutdown), or
3. the `ContinuationTimeout` elapses (default 20s).

If the connection is already dead and channel 0 was never shut down, neither
(1) nor (2) happens, so `await k` blocks for the **entire `ContinuationTimeout`**
before failing. That is the observed "hang".

The continuation timeout is a self-contained `CancellationTokenSource`; note
that `k`'s cancellation token deliberately does **not** include the user's
token when the channel `IsOpen` ("we should really try to close the channel"),
which is why a cancelled user token does not shorten this wait:

```csharp
// projects/RabbitMQ.Client/Impl/AsyncRpcContinuations.cs (AsyncRpcContinuation ctor)
_continuationTimeoutCancellationTokenSource = new CancellationTokenSource(continuationTimeout);
```

## Issue #1921: hang when a CancellationToken fires during connection open

Symptom: `ConnectionFactory.CreateConnectionAsync(token)` could hang for the
full `ContinuationTimeout` (not the 5s abort timeout) when `token` fired while
the connection was being opened.

### The open path

```
ConnectionFactory.CreateConnectionAsync
  -> AutorecoveringConnection.CreateAsync         (AutomaticRecoveryEnabled, the default)
       -> endpoints.SelectOneAsync(...)           frame handler (TCP) connects here
       -> Connection.OpenAsync(cancellationToken) throws on cancellation
       -> catch: connection.CloseAsync(abort:true); connection.DisposeAsync()
```

On the failure path, `Connection.DisposeAsync` disposes `_channel0`. If channel
0 is still `IsOpen` at that point, the dispose-time abort hangs as described
above.

### Three distinct root causes (all verified via a memory dump)

The hang had more than one trigger. All were confirmed by collecting a dump of a
hung process and inspecting the async chains and object fields (see
"Diagnostic method" below), not by guessing.

1. **`MainLoop` blocked on a socket read during abort.** On abort, the main loop
   was not being cancelled, so it sat in `ReadFrameAsync` until a timeout,
   delaying `FinishCloseAsync`. Fix: cancel the main loop on abort.

   ```csharp
   // Connection.CloseAsync, in the finally that runs after transmitting connection.close
   MaybeTerminateMainloopAndStopHeartbeatTimers(cancelMainLoop: abort);
   ```

2. **The abort path did not wait for `MainLoop` cleanup.** The final
   `await _mainLoopTask.WaitAsync(cts.Token)` used a token linked to the user's
   (already-cancelled) token, so it returned immediately without letting
   `FinishCloseAsync` shut down channel 0. Fix: on abort, wait bounded only by
   the abort timeout, not the user token.

   ```csharp
   CancellationToken mainLoopWaitToken = abort ? timeoutCts.Token : cts.Token;
   await _mainLoopTask.WaitAsync(mainLoopWaitToken).ConfigureAwait(false);
   ```

3. **`MainLoop` never started at all.** `OpenAsync` used to check
   `cancellationToken.ThrowIfCancellationRequested()` *before* starting the main
   loop, and passed the token to `Task.Run`. If the token was already cancelled,
   either the pre-check threw, or `Task.Run(MainLoop, cancelledToken)` produced a
   task that went straight to `Canceled` without ever running `MainLoop`. Either
   way `FinishCloseAsync` never ran, so channel 0 was left open and its dispose
   hung. This is the sub-millisecond window that made #1921 look intermittent.

   Fix: start `MainLoop` as the first action in `OpenAsync`, before any
   cancellation is observed, and do not pass the token to `Task.Run` (the loop
   observes cancellation via `_mainLoopCts`).

   ```csharp
   internal async ValueTask<IConnection> OpenAsync(CancellationToken cancellationToken)
   {
       try
       {
           RabbitMqClientEventSource.Log.ConnectionOpened();
           _mainLoopTask = Task.Run(MainLoop);       // no token, runs before any cancellation check
           cancellationToken.ThrowIfCancellationRequested();
           await StartAndTuneAsync(cancellationToken).ConfigureAwait(false);
           cancellationToken.ThrowIfCancellationRequested();
           await _channel0.ConnectionOpenAsync(_config.VirtualHost, cancellationToken).ConfigureAwait(false);
           return this;
       }
       catch { /* abort + rethrow */ }
   }
   ```

   Precondition check: the frame handler (TCP socket) is already connected by the
   endpoint resolver before `OpenAsync` is called (see
   `AutorecoveringConnection.CreateAsync` and the non-recovering branch of
   `ConnectionFactory.CreateConnectionAsync`), so starting the main loop first is
   safe.

### How the three fixes compose

- Fix 3 guarantees `MainLoop` **starts**, so `FinishCloseAsync` will run.
- Fix 2 guarantees `Connection.CloseAsync` **waits** (bounded by the 5s abort
  timeout) for that cleanup before returning, so the subsequent dispose does not
  race an open channel 0.
- Fix 1 guarantees `MainLoop` **exits promptly** on abort instead of blocking on
  a socket read.

Measured effect on a delay-sweep repro (cancellation timer swept across
0-50ms, `ContinuationTimeout` set to 60s to make hangs obvious):

| Build                                   | Worst case | Hang rate       |
| --------------------------------------- | ---------- | --------------- |
| Pristine main                           | 60s        | ~50% (30/60)    |
| + fix 1 only                            | 60s        | ~0.5%           |
| + fix 2                                 | 60s        | ~1.5%           |
| + fix 3 (all three)                     | 40ms       | 0 / 2100        |

## Relevant timeouts

These are easy to confuse; distinguishing which one a hang tracks is the key
diagnostic signal.

| Constant / setting                         | Default | Meaning                                             |
| ------------------------------------------ | ------- | --------------------------------------------------- |
| `ContinuationTimeout`                      | 20s     | Max wait for an RPC reply (e.g. `channel.close-ok`) |
| `HandshakeContinuationTimeout`             | 10s     | Continuation timeout during the AMQP handshake      |
| `InternalConstants.DefaultConnectionAbortTimeout` | 5s | Time budget for an abort                       |
| `InternalConstants.DefaultConnectionCloseTimeout` | 30s | Time budget for a graceful close               |
| `InternalConstants.DefaultChannelDisposeTimeout`  | 5s  | Wait for server-originated channel close on dispose |

A hang whose duration matches `ContinuationTimeout` (not the 5s abort timeout)
points at an un-completed RPC continuation - the channel-0 abort described here.

## Diagnostic method

This bug was intermittent (sub-millisecond timing window) and involved suspended
async state machines, so thread stacks alone were useless. The workflow that
worked:

1. **Reproduce deterministically.** A small console app swept a
   `CancellationTokenSource.CancelAfter` delay across microsecond values around
   the TCP-connect / handshake boundary, running many iterations per delay and
   counting how many exceeded a threshold. Setting `ContinuationTimeout` to 60s
   made any hang unmistakable.

2. **Set the continuation timeout high, not low.** A large timeout turns a
   "flaky slow test" into a clear multi-second stall you can catch and dump.

3. **Capture a dump of the hung process.** On WSL/Linux with
   `kernel.yama.ptrace_scope=1`, `createdump` cannot attach to a sibling/parent
   process. Instead, have the repro hold the hung task alive
   (`await Task.Delay(...)`) and attach from another shell:

   ```bash
   dotnet-dump collect -p <pid> -o hang.dmp --type Full
   ```

4. **Read async chains, not thread stacks.**

   ```bash
   printf 'dumpasync\nexit\n' | dotnet-dump analyze hang.dmp
   ```

   This walks the continuation graph on the heap. The #1921 hang showed a chain
   ending at `Channel.CloseAsync` awaiting a `Task<bool>` (the RPC continuation),
   reached from `Channel.DisposeAsyncCoreAsync <- Connection.DisposeAsync <-
   AutorecoveringConnection.DisposeAsync <- ...CreateAsync (catch)`.

5. **Confirm object state with `dumpobj`.** The decisive evidence for fix 3 was
   dumping the `Connection` instance and observing `_closed == false` together
   with `_mainLoopTask` having a `null` task scheduler and `RanToCompletion`
   flags - proving `_mainLoopTask` was still the constructor's
   `Task.CompletedTask` and `MainLoop` had never run.

   ```bash
   printf 'dumpheap -mt <MethodTable>\ndumpobj <addr>\nexit\n' | dotnet-dump analyze hang.dmp
   ```

6. **Prove the fix with the regression test both ways.** Revert only the
   suspected fix, confirm the strengthened test fails for the full
   `ContinuationTimeout`, then restore and confirm it passes. This guards against
   a test that passes for the wrong reason.

## Testing

The regression test
(`projects/Test/Integration/TestConnectionFactory.cs::
TestCreateConnectionAsync_CancellationDuringHandshake_CompletesQuickly`) sweeps
short cancellation delays (0us .. 50ms) with a 30s `ContinuationTimeout` and
asserts each attempt completes in under 3s. The wide `ContinuationTimeout` is
deliberate: it makes a regression manifest as a ~30s stall rather than a subtle
few-second delay that a slow CI runner could mask.

### Running against a local broker

Integration and sequential-integration tests need a broker. The CI setup script
is the source of truth:

```bash
.ci/ubuntu/gha-setup.sh
```

It starts a `rabbitmq:management` container named
`<prefix>-rabbitmq` (i.e. `rabbitmq-dotnet-client-rabbitmq`) publishing
5671 (TLS), 5672 (AMQP), and 15672 (management), mounting the TLS certs from
`.ci/certs`. Once it is up:

```bash
dotnet test projects/Test/Integration/Integration.csproj -c Release \
  --filter "FullyQualifiedName~TestCreateConnectionAsync"
```
