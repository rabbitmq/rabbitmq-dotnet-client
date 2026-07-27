# GH-1921 cancellation repro

A standalone console app that reproduces the Windows-only test failures seen on the `fix/gh-1921-cancellation` branch, where a `CancellationToken` fires while a connection is being opened.

This app is **not** part of `RabbitMQDotNetClient.sln` or `Build.csproj`, so it does not affect the main build or CI. It references the local `RabbitMQ.Client` project directly.

## Background

Issue [#1921](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1921): `CreateConnectionAsync(token)` could hang for the full `ContinuationTimeout` when `token` fired during connection open. The fix on this branch resolves the hang on Linux (the integration test sweeps short cancellation delays and sees 0 slow attempts out of 2100), but the strengthened test failed two different ways on the `integration-win32` CI job, one per target framework:

- **net8.0**: the attempt threw `AggregateException` wrapping a `SocketException` (`A call to WSALookupServiceEnd was made while this call was still processing. The call has been canceled.`) at ~3ms. On Windows, cancelling `Dns.GetHostAddressesAsync` (which receives the token on .NET) surfaces a `SocketException`; `EndpointResolverExtensions.SelectOneAsync` collects it and rethrows it wrapped in `AggregateException`. The test only caught `OperationCanceledException`, so the exception escaped and failed the test.

- **net472**: one attempt at a 0us cancellation delay took 10.2s (over the test's 3s bar, but under the 30s `ContinuationTimeout`). The netstandard2.0 client does not pass the token to DNS, and `TcpClientAdapter.ConnectAsync` uses `socket.ConnectAsync(...).WaitAsync(token)` (abandon-not-cancel).

## Resolution

Both failures are now fixed on this branch; this app was used to confirm each one natively on Windows.

- **net8.0 (Failure A)** was a test-only gap: the fix was correct, but the regression test's `catch` only handled `OperationCanceledException`. The Windows DNS-cancel `SocketException` reaches the caller wrapped as `BrokerUnreachableException`, so the test now also catches that.

- **net472 (Failure B)** was a genuine residual hang, not a test artifact. On .NET Framework a `PipeReader.ReadAsync` already parked on the `NetworkStream` does not observe cancellation of the main loop's token, so `MainLoop` never unwinds and channel 0 is never shut down. The fix force-closes the socket on abort (netstandard only) to unblock the parked read; see `CloseSocket` in `SocketFrameHandler` and the `#if NETSTANDARD` abort block in `Connection.CloseAsync`. Post-fix, the repro reports 0 slow attempts out of 200 with a worst case of ~0.2s.

## What it does

The app mirrors the regression test (`Test.Integration.TestConnectionFactory.TestCreateConnectionAsync_CancellationDuringHandshake_CompletesQuickly`):

- Sweeps the same cancellation delays: 0, 100, 250, 500, 750, 1000, 1500, 2000, 5000, and 50000 microseconds.
- Runs 20 iterations per delay.
- Uses a large `ContinuationTimeout` / `HandshakeContinuationTimeout` (30s) so any residual hang blocks long enough to be unmistakable.

Unlike the test, it catches **all** exceptions and prints:

- Per-delay maximum elapsed time and a count of attempts slower than 3s.
- The overall worst attempt and where it occurred.
- A histogram of exception types (including inner-exception chains), which is what distinguishes the net8.0 `AggregateException -> SocketException` case from a plain `OperationCanceledException`.

## Running it

Build and run under **both** target frameworks on **native Windows** (not WSL, since the `WSA*` socket behavior only manifests on Windows). A broker must be reachable; the RabbitMQ Docker container running under WSL is fine, since it publishes 5672 to the Windows host.

```
dotnet run -c Release -f net8.0 -- localhost
dotnet run -c Release -f net472 -- localhost
```

The optional first argument is the broker hostname (default `localhost`).

### Interpreting the output

- **net8.0**: expect an `AggregateException[...] -> SocketException` entry in the histogram. This is Failure A: the fix is fine, and (before the test was broadened) the test's `catch` was too narrow to accept it.
- **net472**: watch the `slow(>3s)` counts and the `WORST` line. Before the fix this path showed a genuine residual hang (Failure B); after the socket-close fix it reports 0 slow attempts.

On Linux this app runs clean (0 slow, worst well under a second) and cannot reproduce either Windows failure; that is expected.
