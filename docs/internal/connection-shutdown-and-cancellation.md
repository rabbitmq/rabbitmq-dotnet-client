# Connection Shutdown, Channel 0, and Cancellation

This document captures the connection/channel shutdown model of the 7.x client, with emphasis on the areas that are subtle enough to have produced real hangs (notably issues #1921 and #1960). It is written for maintainers who need to reason about what happens when a connection open is cancelled or aborted, how channel 0 gets torn down, and which cancellation token a shutdown handler actually receives.

## The central fact: channel 0 is special

Every connection has a hidden channel/session pair on channel number 0 (`_channel0` / `_session0` in `Connection.cs`). It carries connection-level methods (`connection.start`, `connection.tune`, `connection.open`, `connection.close`), not application traffic.

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

Application channels (channel number != 0) are shut down automatically when the connection shuts down, because their session is wired to `ConnectionShutdownAsync`. That event handler chain ends in `Channel.OnSessionShutdownAsync -> OnChannelShutdownAsync -> _continuationQueue.HandleChannelShutdown(reason)`, which faults any pending RPC continuation with an `OperationInterruptedException`.

Channel 0 has **no such wiring**. Its only shutdown path is `Connection.FinishCloseAsync`:

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

`Connection.FinishCloseAsync` runs in exactly one place: at the end of `MainLoop` (`Connection.Receive.cs`), after `ReceiveLoopAsync` returns or throws. In other words:

> **If `MainLoop` never runs to completion, channel 0 is never shut down.**

## Why an un-shut-down channel 0 causes a hang

When a `Channel` (including channel 0) is disposed while still `IsOpen`, its dispose path issues an abort:

```csharp
// projects/RabbitMQ.Client/Impl/Channel.cs  (DisposeAsyncCoreAsync)
if (IsOpen)
{
    await this.AbortAsync().ConfigureAwait(false);
}
```

`AbortAsync` funnels into `Channel.CloseAsync(ShutdownEventArgs, bool abort)`, which enqueues a `ChannelCloseAsyncRpcContinuation` and then awaits it:

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

If the connection is already dead and channel 0 was never shut down, neither (1) nor (2) happens, so `await k` blocks for the **entire `ContinuationTimeout`** before failing. That is the observed "hang".

The continuation timeout is a self-contained `CancellationTokenSource`; note that `k`'s cancellation token deliberately does **not** include the user's token when the channel `IsOpen` ("we should really try to close the channel"), which is why a cancelled user token does not shorten this wait:

```csharp
// projects/RabbitMQ.Client/Impl/AsyncRpcContinuations.cs (AsyncRpcContinuation ctor)
_continuationTimeoutCancellationTokenSource = new CancellationTokenSource(continuationTimeout);
```

## Issue #1921: hang when a CancellationToken fires during connection open

Symptom: `ConnectionFactory.CreateConnectionAsync(token)` could hang for the full `ContinuationTimeout` (not the 5s abort timeout) when `token` fired while the connection was being opened.

### The open path

```
ConnectionFactory.CreateConnectionAsync
  -> AutorecoveringConnection.CreateAsync         (AutomaticRecoveryEnabled, the default)
       -> endpoints.SelectOneAsync(...)           frame handler (TCP) connects here
       -> Connection.OpenAsync(cancellationToken) throws on cancellation
       -> catch: connection.CloseAsync(abort:true); connection.DisposeAsync()
```

On the failure path, `Connection.DisposeAsync` disposes `_channel0`. If channel 0 is still `IsOpen` at that point, the dispose-time abort hangs as described above.

### Four distinct root causes (all verified via a memory dump or a native-Windows repro)

The hang had more than one trigger. Causes 1-3 were confirmed by collecting a dump of a hung process and inspecting the async chains and object fields (see "Diagnostic method" below); cause 4 is .NET Framework specific and was confirmed with the native-Windows repro app at `projects/Applications/GH-1921/`. None was guessed.

1. **`MainLoop` blocked on a socket read during abort.** On abort, the main loop was not being cancelled, so it sat in `ReadFrameAsync` until a timeout, delaying `FinishCloseAsync`. Fix: cancel the main loop on abort.

   ```csharp
   // Connection.CloseAsync, in the finally that runs after transmitting connection.close
   MaybeTerminateMainloopAndStopHeartbeatTimers(cancelMainLoop: abort);
   ```

2. **The abort path did not wait for `MainLoop` cleanup.** The final `await _mainLoopTask.WaitAsync(cts.Token)` used a token linked to the user's (already-cancelled) token, so it returned immediately without letting `FinishCloseAsync` shut down channel 0. Fix: on abort, wait bounded only by the abort timeout, not the user token.

   ```csharp
   CancellationToken mainLoopWaitToken = abort ? timeoutCts.Token : cts.Token;
   await _mainLoopTask.WaitAsync(mainLoopWaitToken).ConfigureAwait(false);
   ```

3. **`MainLoop` never started at all.** `OpenAsync` used to check `cancellationToken.ThrowIfCancellationRequested()` *before* starting the main loop, and passed the token to `Task.Run`. If the token was already cancelled, either the pre-check threw, or `Task.Run(MainLoop, cancelledToken)` produced a task that went straight to `Canceled` without ever running `MainLoop`. Either way `FinishCloseAsync` never ran, so channel 0 was left open and its dispose hung. This is the sub-millisecond window that made #1921 look intermittent.

   Fix: start `MainLoop` as the first action in `OpenAsync`, before any cancellation is observed, and do not pass the token to `Task.Run` (the loop observes cancellation via `_mainLoopCts`).

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

   Precondition check: the frame handler (TCP socket) is already connected by the endpoint resolver before `OpenAsync` is called (see `AutorecoveringConnection.CreateAsync` and the non-recovering branch of `ConnectionFactory.CreateConnectionAsync`), so starting the main loop first is safe.

4. **netstandard2.0 / .NET Framework only: a parked `PipeReader.ReadAsync` never observes cancellation.** On .NET Framework, `PipeReader.ReadAsync` over a `NetworkStream` does **not** honor its `CancellationToken` once it is parked (this happens when open is cancelled before the protocol header is sent, so no server bytes ever arrive). Cancelling `_mainLoopCts` therefore cannot unblock the read: `MainLoop` stays parked in `ReadFrameAsync`, `_mainLoopTask` never completes, and fixes 1-3 have nothing to unwind. The abort-timeout wait from fix 2 burns its full budget, and because `MainLoop` never reaches `FinishCloseAsync`, channel 0 is left open and its later dispose stalls for a second timeout (the ~10s stacked-timeout stall seen on the `integration-win32` job).

   On net8.0 this does not happen: `Frame.ReadFromPipeAsync` awaits `reader.ReadAsync(mainLoopToken)` and that read observes cancellation, so `_mainLoopCts.Cancel()` alone unwinds the loop. The fix is therefore netstandard-only.

   Fix: on abort, force-close the socket up front so the OS aborts the parked read, letting `MainLoop` unwind promptly and run `FinishCloseAsync` (the single authoritative frame-handler close and channel 0 shutdown). The close touches **only** the socket, never the pipe reader/writer, so it cannot corrupt the single-consumer `StreamPipeReader` or race the `_frameHandler.CloseAsync` inside `FinishCloseAsync` (a second socket close is idempotent).

   ```csharp
   // Connection.CloseAsync, immediately before awaiting _mainLoopTask
   CancellationToken mainLoopWaitToken = abort ? timeoutCts.Token : cts.Token;
#if NETSTANDARD
   if (abort)
   {
       _frameHandler.CloseSocket();   // OS-level abort of the parked read; see IFrameHandler.CloseSocket
   }
#endif
   await _mainLoopTask.WaitAsync(mainLoopWaitToken).ConfigureAwait(false);
   ```

   `IFrameHandler.CloseSocket` is declared and implemented under `#if NETSTANDARD` only; `SocketFrameHandler` is the sole implementer, so the interface addition affects nothing else. The pre-existing `#if NETSTANDARD2_0` guard in `AutorecoveringConnection.DisposeAsync` was widened to `#if NETSTANDARD` at the same time for consistency (behavior-preserving today, since both TFMs are `net8.0;netstandard2.0`).

### How the four fixes compose

- Fix 3 guarantees `MainLoop` **starts**, so `FinishCloseAsync` will run.
- Fix 2 guarantees `Connection.CloseAsync` **waits** (bounded by the 5s abort timeout) for that cleanup before returning, so the subsequent dispose does not race an open channel 0.
- Fix 1 guarantees `MainLoop` **exits promptly** on abort instead of blocking on a socket read (net8.0, where the read observes cancellation).
- Fix 4 provides the same prompt exit on netstandard2.0 / .NET Framework, where the parked read cannot observe cancellation and only an OS-level socket close will unblock it.

Measured effect on a delay-sweep repro (cancellation timer swept across 0-50ms, `ContinuationTimeout` set high to make hangs obvious):

| Build / platform                        | Worst case | Hang rate       |
| --------------------------------------- | ---------- | --------------- |
| Pristine main (Linux, net8.0)           | 60s        | ~50% (30/60)    |
| + fix 1 only (Linux)                    | 60s        | ~0.5%           |
| + fix 2 (Linux)                         | 60s        | ~1.5%           |
| + fix 3 (all three; Linux, net8.0)      | 40ms       | 0 / 2100        |
| Fixes 1-3 only, Windows net472          | 10.2s      | present         |
| + fix 4 (Windows net472)                | ~0.2s      | 0 / 200         |

## Issue #1960: shutdown handlers deadlock on the main loop token

A second, independent cancellation defect in the same subsystem. It surfaced as an intermittent `integration-win32` failure in `TestConnectionShutdown.TestAbortWithSocketClosedOutOfBandAndCancellation`, timing out at that test's 6s wait.

### The race

`SetCloseReason` uses `Interlocked.CompareExchange`, so the **first writer wins** and every later path is a no-op. When a socket dies out of band and the application aborts at roughly the same moment, two paths compete:

| | Winner | Reason minted | Token carried by `ShutdownEventArgs` |
| --- | --- | --- | --- |
| Path A | abort caller | `Application` / 200 | the **caller's** token |
| Path B | `MainLoop` (dead socket) | `Library` / 541 | `_mainLoopCts.Token` |

Path A is healthy: the caller's token fires on its own schedule, independent of the shutdown machinery.

### Why Path B self-deadlocked

`OnShutdownAsync` invokes shutdown handlers **sequentially** (`AsyncEventingWrapper.InvokeAsync`) and awaits each one. Meanwhile `_mainLoopCts` is cancelled only by `FinishCloseAsync` - which `MainLoop` reaches only *after* `HandleMainLoopExceptionAsync` returns, i.e. after every handler has completed.

So a handler that awaits `args.CancellationToken` - the documented way to observe "this connection is going away" - parked on a token that nobody could cancel until the handler itself returned. The handler waited out its own timeout while `MainLoop` waited on the handler. Worse, because invocation is sequential, no *later* handler could run either, which is why the test's `ConnectionShutdownAsync` fallback could not rescue the TCS.

### The fix

Cancel the main loop token *before* invoking the handlers, in `HandleMainLoopExceptionAsync` (`Connection.Receive.cs`):

```csharp
MaybeTerminateMainloopAndStopHeartbeatTimers(cancelMainLoop: true);
await OnShutdownAsync(reason).ConfigureAwait(false);
```

By that point the close reason is set and the connection is unrecoverably down, so an already-cancelled token is the *correct* signal: handlers unwind immediately instead of waiting for a liveness that will never return.

Note this touches **only** the Library / dead-connection path. It does not conflict with the #1888 invariant (`ConsumerDispatcherChannelBase`) that a shutdown token must not *always* arrive already-cancelled - that constraint concerns the Application close path, where the caller's token is still passed through untouched.

### Two traps worth remembering

1. **`IsOpen` is defined as `CloseReason is null`.** The first fix attempt added the cancellation to `Connection.CloseAsync`'s lost-the-race branch and was **dead code**: `AutorecoveringConnection.CloseAsync` only calls into the inner connection via `CloseInnerConnectionAsync()`, which is guarded on `_innerConnection.IsOpen` - already `false` once `MainLoop` set the reason. The repro, not the reasoning, caught this. Re-run the reproduction after every "obvious" fix.

2. **A pre-cancelled shutdown token breaks code that awaits on it.** Making Path B's token arrive already-cancelled exposed a latent bug in `Channel.PublisherConfirms.cs`: `MaybeHandlePublisherConfirmationTcsOnChannelShutdownAsync` did `_confirmSemaphore.WaitAsync(reason.CancellationToken)`, which *throws* on a cancelled token - so `MaybeSetExceptionOnConfirmsTcs` never ran and publisher-confirm waiters hung forever. Shutdown **cleanup** must not be gated on the shutdown's own token; it now uses a bounded wait with `CancellationToken.None` and faults the TCS even if the semaphore cannot be acquired. When changing token semantics, audit every consumer of `ShutdownEventArgs.CancellationToken`.

### Reproducing it

Not net472-specific, despite appearances. net472 merely *loses the race reliably* on a cold process: the abort path (`AutorecoveringConnection.CloseAsync -> StopRecoveryLoopAsync -> Connection.CloseAsync -> SetCloseReason`) takes ~10ms un-JITted versus ~0.1ms warm, and that latency is the entire race window. Iterations 2..N in one process all win the race, so an in-process loop shows at most `1/N` - which is why this looked like a rare flake for so long.

Two gates, in `projects/Applications/GH-1960/`:

- **`force-race` mode** - delays between the out-of-band socket close and `AbortAsync`, standing in for the JIT latency. Deterministic on any platform and TFM, including Linux/net8.0. The fast local gate: 5/5 fail pre-fix (~6.25s each), 0/5 post-fix (~0.25s each).
- **`repro.ps1`** - the faithful reproduction: builds net472 once, then runs one *cold* iteration per fresh process. Measured 20/20 failures pre-fix, 0/20 post-fix on native Windows.

The token state in the app's output is the discriminating signal: `cancellable,not-yet` on a `Library/541` reason is the bug; `cancellable,already-cancelled` is the fix.

## Issue #1968: a disposed `SemaphoreSlim` strands the second frame-handler closer

This one presented as a flaky net472 test - `TestCleanClosureWithSocketClosedOutOfBand` failing after exactly 30s with a bare `OperationCanceledException` - and was filed as `S-needs-investigation`. It is a library bug, and the platform and the test were both red herrings.

### The mechanism

`SocketFrameHandler.CloseAsync` acquired `_closingSemaphore` and, in its `finally`, **disposed** it without ever releasing it. `SemaphoreSlim` grants disposal no special meaning for pending async waiters: a second `CloseAsync` already parked in `WaitAsync` when the first disposes the semaphore stays pending **forever**.

Verified directly, and worth internalising because it is counter-intuitive - all three wait forms hang, so a timeout is not an escape hatch:

| Wait form | Outcome after the semaphore is disposed |
|---|---|
| `WaitAsync(token)` with a 3s CTS | still pending at 8s |
| `WaitAsync(TimeSpan.FromSeconds(3))` | still pending at 8s |
| `WaitAsync(3s, token)` | still pending at 8s |

Not even the waiter's own cancellation token releases it. `Dispose()` itself does not throw, so the first closer sees nothing wrong.

### Why that reaches a user-visible 30s stall

`MainLoop`'s `FinishCloseAsync` calls `_frameHandler.CloseAsync`, so it is itself one of the concurrent closers. When it lost this race it never returned, which means:

1. `MainLoop` never completes, so `_mainLoopTask` never completes.
2. `Connection.CloseAsync` waits on `_mainLoopTask` (`Connection.cs:464`) and burns its full timeout.
3. That timeout is `InternalConstants.DefaultConnectionCloseTimeout` (30s), **not** the caller's 6s - a non-abort close floors the caller's value at 30s.
4. On netstandard2.0 the throwing frame is `TaskExtensions.DoWaitAsync`, which is inside `#if !NET`, so it surfaces as a bare `OperationCanceledException`.

Item 4 is the only platform-specific part, and it affects the *exception type*, not the hang. The 30-second duration was the tell that this tracked the close timeout rather than a merely-slow close.

### The fix

Release the semaphore instead of disposing it, and set `_closed` before releasing so the next waiter returns rather than repeating the close. A `WaitAsync` that faults now returns instead of proceeding - it must not run the close body without holding the semaphore. `SemaphoreSlim` only requires disposal once `AvailableWaitHandle` has been read, and this one never exposes it, so nothing leaks.

`_closed` is also now `volatile`. Both `CloseAsync` and `WriteAsync` read it outside any lock, on a field written by whichever thread happens to own the close, so those reads need acquire semantics rather than a value the JIT may keep in a register. The store paired with `Release()` is already ordered by the semaphore; the fast-path reads are not. `Connection._closed` (`Connection.cs:51`) is `volatile` for the same reason.

## Issue #1976: the same anti-pattern everywhere else

#1968 fixed one instance. An audit for the rest found **six** `SemaphoreSlim` fields in the client and **seven** disposal calls across three types - two more sites than the issue's own audit claimed, because it recorded `AutorecoveringConnection`'s two semaphores as never disposed when in fact both were.

| Field | Disposed in | Concurrent waiters |
|---|---|---|
| `MainSession._closingSemaphore` | `MainSession.Dispose` | `SetSessionClosingAsync` |
| `Channel._rpcSemaphore` | `Dispose(bool)` and `DisposeAsyncCoreAsync` | ~20 RPC sites |
| `Channel._confirmSemaphore` | `Dispose(bool)` and `DisposeAsyncCoreAsync` | publish path, shutdown cleanup |
| `AutorecoveringConnection._recordedEntitiesSemaphore` | `DisposeAsync` | ~30 recording sites |
| `AutorecoveringConnection._channelsSemaphore` | `DisposeAsync` | 4 sites |
| `SocketFrameHandler._closingSemaphore` | (fixed in #1968) | `CloseAsync` |

All seven disposal calls were removed. Proving no waiter can be parked at each of these sites is more expensive than simply not disposing, and there is nothing to reclaim: `SemaphoreSlim` only requires disposal once `AvailableWaitHandle` has been read, and no site in this client ever exposes it.

`MainSession._closingSemaphore` is the closest analogue of #1968 and reachable the same way. `Connection.DisposeAsync` disposes the session after `AbortAsync` (`Connection.cs:594`), while `MainLoop`'s `FinishCloseAsync` independently calls `SetSessionClosingAsync`; if the latter is parked on the semaphore when disposal runs, `MainLoop` never returns. The `DefaultConnectionAbortTimeout` on that wait does not mitigate it - see the table above.

`Channel`'s two are worth noting for the same reason. `_rpcSemaphore` is awaited under the continuation's linked token, so `ContinuationTimeout` will not shake a stranded RPC loose. `_confirmSemaphore` is awaited during shutdown cleanup with a 5s timeout added *specifically* so a stuck semaphore cannot block shutdown - reasoning that does not survive the semaphore being disposed rather than merely held.

`_recoveryCancellationTokenSource.Dispose()` in `AutorecoveringConnection.DisposeAsync` was deliberately **kept**. A `CancellationTokenSource` is genuinely different: it owns a timer and registrations that need reclaiming, and cancelling it already released anything waiting on its token.

`MainSession._disposed` is now `volatile`, matching `_closing` and `_closeIsServerInitiated` beside it - `SetSessionClosingAsync` reads it from whichever thread reaches it, and disposal writes it from another.

### Testing this

`projects/Test/Integration/TestSemaphoreDisposal.cs`. The race is not deterministically forceable from the public API at every site, so the tests assert the invariant that makes it impossible: after a full dispose, every semaphore is still usable. `Wait(0)` throws `ObjectDisposedException` on a disposed instance and returns immediately otherwise, so it detects disposal without blocking - note that `CurrentCount` does *not* throw and cannot be used for this. A third test reflects over the assembly and fails if the set of `SemaphoreSlim` fields changes, so adding one forces a decision about its disposal.

Two things to know if you extend these tests. `AutorecoveringChannel.DisposeAsync` does not dispose its inner channel, so disposing the `IChannel` that `CreateChannelAsync` returns never reaches `Channel`'s dispose path at all - the test disposes `InnerChannel` directly. And use `BindingFlags.DeclaredOnly` when enumerating: `_rpcSemaphore` is `protected`, so `RecoveryAwareChannel` otherwise reports it a second time.

## Relevant timeouts

These are easy to confuse; distinguishing which one a hang tracks is the key diagnostic signal.

| Constant / setting                         | Default | Meaning                                             |
| ------------------------------------------ | ------- | --------------------------------------------------- |
| `ContinuationTimeout`                      | 20s     | Max wait for an RPC reply (e.g. `channel.close-ok`) |
| `HandshakeContinuationTimeout`             | 10s     | Continuation timeout during the AMQP handshake      |
| `InternalConstants.DefaultConnectionAbortTimeout` | 5s | Time budget for an abort                       |
| `InternalConstants.DefaultConnectionCloseTimeout` | 30s | Time budget for a graceful close               |
| `InternalConstants.DefaultChannelDisposeTimeout`  | 5s  | Wait for server-originated channel close on dispose |

A hang whose duration matches `ContinuationTimeout` (not the 5s abort timeout) points at an un-completed RPC continuation - the channel-0 abort described here. A stacked pair of 5s stalls (~10s) on .NET Framework points at cause 4: the abort-timeout wait plus a subsequent channel-0 dispose timeout.

## Diagnostic method

This bug was intermittent (sub-millisecond timing window) and involved suspended async state machines, so thread stacks alone were useless. The workflow that worked:

1. **Reproduce deterministically.** A small console app swept a `CancellationTokenSource.CancelAfter` delay across microsecond values around the TCP-connect / handshake boundary, running many iterations per delay and counting how many exceeded a threshold. Setting `ContinuationTimeout` high made any hang unmistakable. This app lives at `projects/Applications/GH-1921/` and multi-targets net8.0 and net472 so both platform behaviors can be exercised natively on Windows.

2. **Set the continuation timeout high, not low.** A large timeout turns a "flaky slow test" into a clear multi-second stall you can catch and dump.

3. **Capture a dump of the hung process.** On WSL/Linux with `kernel.yama.ptrace_scope=1`, `createdump` cannot attach to a sibling/parent process. Instead, have the repro hold the hung task alive (`await Task.Delay(...)`) and attach from another shell:

   ```bash
   dotnet-dump collect -p <pid> -o hang.dmp --type Full
   ```

4. **Read async chains, not thread stacks.**

   ```bash
   printf 'dumpasync\nexit\n' | dotnet-dump analyze hang.dmp
   ```

   This walks the continuation graph on the heap. The #1921 hang showed a chain ending at `Channel.CloseAsync` awaiting a `Task<bool>` (the RPC continuation), reached from `Channel.DisposeAsyncCoreAsync <- Connection.DisposeAsync <- AutorecoveringConnection.DisposeAsync <- ...CreateAsync (catch)`.

5. **Confirm object state with `dumpobj`.** The decisive evidence for fix 3 was dumping the `Connection` instance and observing `_closed == false` together with `_mainLoopTask` having a `null` task scheduler and `RanToCompletion` flags - proving `_mainLoopTask` was still the constructor's `Task.CompletedTask` and `MainLoop` had never run.

   ```bash
   printf 'dumpheap -mt <MethodTable>\ndumpobj <addr>\nexit\n' | dotnet-dump analyze hang.dmp
   ```

6. **Prove the fix with the regression test both ways.** Revert only the suspected fix, confirm the strengthened test fails for the full `ContinuationTimeout`, then restore and confirm it passes. This guards against a test that passes for the wrong reason.

## Testing

### #1960

`TestConnectionShutdown.TestAbortCancellationWhenMainLoopWinsCloseReasonRace_GH1960` is the deterministic counterpart to `TestAbortWithSocketClosedOutOfBandAndCancellation`. It delays before `AbortAsync` so `MainLoop` wins the reason race on every platform and TFM, rather than relying on cold-start JIT latency that only net472 provides.

It asserts more than "the TCS completed": it asserts the channel shutdown handler was released **by its own cancellation token** and not by the `ConnectionShutdownAsync` fallback. Without that assertion the test would pass on a build where the handler is still parked, since the fallback also completes the TCS. Verified to fail at the 6s bar with the fix reverted.

### #1968

`TestConnectionShutdown.TestConcurrentFrameHandlerCloseDoesNotHang_GH1968` calls `CloseFrameHandlerAsync()` twice concurrently, which is the minimal deterministic form of the `MainLoop` race. Two direct closes are used rather than trying to time the real race, and the result is not platform-specific: verified failing at the 6s bar on net8.0/Linux with the fix reverted (`TimeoutException`), passing in ~140ms with it. The pre-fix signature is a task left in `WaitingForActivation` indefinitely.

### #1976

`projects/Test/Integration/TestSemaphoreDisposal.cs` - see "Testing this" under #1976 above. Verified to fail with the fix reverted, naming all five reachable disposal sites across the two runtime tests, while `SocketFrameHandler._closingSemaphore` still passes because #1968 already fixed it.

### #1921

The regression test (`projects/Test/Integration/TestConnectionFactory.cs::TestCreateConnectionAsync_CancellationDuringHandshake_CompletesQuickly`) sweeps short cancellation delays (0us .. 50ms) with a 30s `ContinuationTimeout` and asserts each attempt completes in under 3s. The wide `ContinuationTimeout` is deliberate: it makes a regression manifest as a ~30s stall rather than a subtle few-second delay that a slow CI runner could mask.

The test is a timing assertion and does not assert a specific exception type. It catches the only two exceptions `CreateConnectionAsync` can surface: `OperationCanceledException` (rethrown when the token was the cause) and `BrokerUnreachableException` (the wrapper for any other handshake failure, including the Windows DNS-cancel `SocketException`). Anything else escapes and fails the test.

### Running against a local broker

Integration and sequential-integration tests need a broker. The CI setup script is the source of truth:

```bash
.ci/ubuntu/gha-setup.sh
```

It starts a `rabbitmq:management` container named `<prefix>-rabbitmq` (i.e. `rabbitmq-dotnet-client-rabbitmq`) publishing 5671 (TLS), 5672 (AMQP), and 15672 (management), mounting the TLS certs from `.ci/certs`. Once it is up:

```bash
dotnet test projects/Test/Integration/Integration.csproj -c Release \
  --filter "FullyQualifiedName~TestCreateConnectionAsync"
```
