# GH-1960 abort-cancellation flake repro

A standalone console app that reproduces the flaky test
`Test.Integration.TestConnectionShutdown.TestAbortWithSocketClosedOutOfBandAndCancellation`
(issue [#1960](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1960)).

This app is **not** part of `RabbitMQDotNetClient.sln` or `Build.csproj`, so it
does not affect the main build or CI. It references the local `RabbitMQ.Client`
project directly, and reaches the internal `AutorecoveringConnection.CloseFrameHandlerAsync()`
via reflection (it is not on the client's `InternalsVisibleTo` list).

## Background

The test closes the frame handler out of band, then calls `AbortAsync(cts.Token)`
with a 1s token, and waits up to 6s for a `ChannelShutdownAsync` handler whose
parked `Task.Delay(1min, args.CancellationToken)` must be cancelled. It
intermittently times out at the 6s wait on the net472 (netstandard2.0) client on
the `integration-win32` CI job.

## Root cause (confirmed)

Two shutdown paths race to win `SetCloseReason` (first writer wins, via
`Interlocked.CompareExchange`):

- **Path A (abort caller):** `AbortAsync(cts.Token)` -> `Connection.CloseAsync`
  sets the close reason and awaits `OnShutdownAsync(reason)`. The channel's
  shutdown handler receives the abort's **1s token**, its parked `Task.Delay`
  cancels at ~1s, the TCS completes. Healthy.
- **Path B (main loop):** the out-of-band socket close makes `MainLoop` hit
  end-of-stream first and mint its own `ShutdownEventArgs(Library, ...,
  _mainLoopCts.Token)` (`Connection.Receive.cs`), winning `SetCloseReason`. The
  abort then finds the reason already set and skips `OnShutdownAsync`.

Path B was a **self-deadlock**. `HandleMainLoopExceptionAsync` -> `OnShutdownAsync`
invokes shutdown handlers **sequentially** (`AsyncEventingWrapper.InvokeAsync`). A
handler awaiting `args.CancellationToken` -- the documented way to observe "this
connection is going away" -- parked on a token that only `FinishCloseAsync`
cancels, and MainLoop reaches `FinishCloseAsync` only *after* every handler
returns. So the handler waited out its own 1-minute delay while MainLoop waited on
the handler, and the test's `ConnectionShutdownAsync` fallback could not run either
(sequential invocation). Automatic recovery is not involved.

The fix cancels the main loop token *before* invoking the shutdown handlers, so
they observe an already-cancelled token and unwind immediately. See
`Connection.Receive.cs` and the regression test
`TestAbortCancellationWhenMainLoopWinsCloseReasonRace_GH1960`.

**Not net472-specific.** net472 merely loses the race reliably on a cold process
(the abort path takes ~10ms un-JITted vs ~0.1ms warm). The same deadlock
reproduces on Linux/net8.0 once MainLoop is made to win the race -- see the
`force-race` mode below.

## What it does

For each iteration it mirrors the test exactly:

1. Opens a connection (automatic recovery on) and a channel.
2. Registers a `ChannelShutdownAsync` handler that awaits
   `Task.Delay(1min, args.CancellationToken)` and completes a TCS when cancelled.
3. Registers a `ConnectionShutdownAsync` fallback that `TrySetResult`s the same TCS.
4. Closes the frame handler out of band (`CloseFrameHandlerAsync`, via reflection).
5. `await conn.AbortAsync(cts.Token)` with a 1s token.
6. Waits up to 6s for the TCS (the test's `_waitSpan`).

Unlike the test, it records per attempt and as histograms:

- the shutdown reason the **channel handler** saw: `Initiator` (Application vs
  Library), `ReplyCode`, `ReplyText`;
- whether that handler's token was cancellable, and whether it was already
  cancelled on entry;
- how the TCS completed: `channel-delay-cancelled`, `connection-fallback`, or
  `TIMEOUT (6s)`;
- elapsed time to TCS completion, and the count of 6s timeouts.

## Running it

A broker must be reachable; the RabbitMQ Docker container running under WSL
publishes 5672 to the Windows host, so `localhost` works from native Windows too.

```
dotnet run -c Release -f net8.0  -- localhost 50
dotnet run -c Release -f net472  -- localhost 50
```

The optional first argument is the broker hostname (default `localhost`); the
optional second is the iteration count (default 50).

Note that a plain in-process loop is a **weak** gate: only iteration 0 is cold, so
it shows at most `1/N` even pre-fix. Use one of the two deterministic modes below.

### force-race mode (deterministic, any platform / TFM)

Passing `force-race` as the third argument inserts a delay between the
out-of-band frame-handler close and `AbortAsync`, standing in for the cold-start
JIT latency. MainLoop then wins `SetCloseReason` every time, so the deadlock
reproduces on any platform and TFM -- including Linux/net8.0 -- without needing a
cold process. This is the fast local gate:

```
dotnet run -c Release -f net8.0 -- localhost 5 force-race
```

Pre-fix: 5/5 timeouts, ~6.25s each, channel token `cancellable,not-yet`.
Post-fix: 0/5, ~0.25s each, channel token `cancellable,already-cancelled`.

### Cold-start gate (deterministic, net472 on native Windows)

This is the faithful reproduction of the CI failure. On net472 the failure is a
**cold-start race**, not a random flake: only the *first* connection after process
start loses it, because the abort code path
(`AutorecoveringConnection.CloseAsync -> StopRecoveryLoopAsync -> Connection.CloseAsync
-> SetCloseReason`) has not yet been JIT-compiled and takes ~10ms, which is the
entire race window. The already-warm MainLoop wins `SetCloseReason` with a
`Library` reason inside that window. Once the abort path is JIT-warm it reaches
`SetCloseReason` in ~0.1ms and wins every time, so iterations 2..N in a single
process all pass. This is why the in-process loop shows only "1/200": iteration 0
is the only cold one.

To exercise the race deterministically, run **one iteration per fresh process** in
a loop. The app prints a final `RESULT: PASS|FAIL` line and returns a non-zero
exit code on any timeout, so the loop can tally results. Use `repro.ps1`
(native Windows PowerShell); it builds net472 once, then runs one cold iteration
per fresh process and tallies the failures:

```powershell
.\projects\Applications\GH-1960\repro.ps1
.\projects\Applications\GH-1960\repro.ps1 -Host_ localhost -Count 50
```

Measured: **20/20 failures pre-fix, 0/20 post-fix.** The script builds first and
then uses `--no-build` inside the loop, which keeps each process cold at the CLR
level without rebuilding — do not skip the build step, or the loop silently
measures stale binaries.

### Interpreting the output

- **Path A (healthy):** the attempt completes via `channel-delay-cancelled` at
  ~1s, seeing `Application/200/Connection close forced` with a
  `cancellable,not-yet` token — the abort won the reason race and its own 1s token
  released the handler.
- **Path B, fixed:** `channel-delay-cancelled` promptly, seeing `Library/541/...`
  with a `cancellable,already-cancelled` token — MainLoop won the race but
  cancelled the token before invoking handlers.
- **Path B, broken:** `TIMEOUT (6s)` with a `Library/541/...` reason and a
  `cancellable,not-yet` token. This is the #1960 deadlock; the handler is parked on
  a token nobody will cancel until it returns.
- A `connection-fallback` completion also indicates a problem: it means the channel
  handler was never released by its own token. The regression test asserts against
  this.
