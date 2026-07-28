# GH-1960 abort-cancellation flake repro

A standalone console app that reproduces the net472-only flaky test
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
the `integration-win32` CI job, and cannot be reproduced on Linux/WSL.

## The load-bearing question (why this app gathers data instead of applying a fix)

Reading the abort path in `Connection.CloseAsync` and `Connection.Receive.cs`
surfaces a race between two shutdown paths, either of which can win `SetCloseReason`:

- **Path A (abort caller):** `AbortAsync(cts.Token)` -> `Connection.CloseAsync`
  sets the close reason and awaits `OnShutdownAsync(reason)`. The channel's
  shutdown handler then receives the abort's **1s token**, and its parked
  `Task.Delay` cancels at ~1s. The TCS completes. Healthy.
- **Path B (main loop):** the out-of-band socket close makes `MainLoop` hit
  end-of-stream first and build its own `ShutdownEventArgs(Library, ...,
  _mainLoopCts.Token)` (`Connection.Receive.cs`), winning `SetCloseReason`. The
  abort then finds the reason already set and skips `OnShutdownAsync`. The
  channel handler instead sees a **`Library`-initiated** reason carrying
  `_mainLoopCts.Token`. Its parked `Task.Delay` only cancels if/when that token
  fires. And because shutdown handlers run **sequentially**
  (`AsyncEventingWrapper.InvokeAsync`), the test's `ConnectionShutdownAsync`
  fallback cannot complete the TCS while the channel handler is parked.

So the discriminating question is: **on a timeout, which shutdown reason did the
channel handler receive, and was its token cancellable and cancelled in time?**
That is fully observable from the event args, so this app measures it rather than
guessing a fix. Per the project's debugging discipline, a fix to this
timing-sensitive shutdown code should only be proposed after native-Windows
net472 data confirms the mechanism.

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

Build and run under **both** target frameworks on **native Windows** (not WSL,
since the flake only manifests on the net472 client on native Windows). A broker
must be reachable; the RabbitMQ Docker container running under WSL publishes 5672
to the Windows host, so `localhost` works.

```
dotnet run -c Release -f net8.0  -- localhost 50
dotnet run -c Release -f net472  -- localhost 50
```

The optional first argument is the broker hostname (default `localhost`); the
optional second is the iteration count (default 50). Raise the iteration count if
the flake is infrequent.

### Interpreting the output

- **Linux control (either TFM):** every attempt should complete via
  `channel-delay-cancelled` at ~1s, seeing `Application/200/Connection close
  forced` with a cancellable token (Path A). 0 timeouts. Confirmed on Linux.
- **net472 on Windows:** watch the `TIMEOUTS` count. If a timeout attempt shows a
  `Library/...` channel reason (Path B won) and/or a token that is not cancellable
  or not cancelled in time, that confirms the mechanism above and points the fix
  at making the abort's cancellation reach the channel handler even when MainLoop
  wins the reason race.
