# GH-1968 clean-closure flake rate

`repro.ps1` measures how often
`Test.Integration.TestConnectionShutdown.TestCleanClosureWithSocketClosedOutOfBand`
fails on net472 (issue
[#1968](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1968)).

Unlike `GH-1960/`, there is no application project here: the script drives the
existing test rather than reimplementing it, so this directory holds only the
script. Nothing here is part of `RabbitMQDotNetClient.sln` or `Build.csproj`.

The issue records the frequency as unquantified, seen once while watching PR
[#1734](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1734). This
script exists to get a real rate before spending time on a fix.

## Why net472 only

The test closes the frame handler out of band and then calls
`CloseAsync(_waitSpan)`. It catches `AlreadyClosedException` and
`ChannelClosedException`, but not `OperationCanceledException`, which is what it
intermittently gets.

The throwing frame, `TaskExtensions.DoWaitAsync`, is inside `#if !NET`. It throws
a bare `new OperationCanceledException(cancellationToken)` when the token wins its
`Task.WhenAny` race. On net8.0 the built-in `Task.WaitAsync` is used instead, so
**the bug cannot reproduce on net8.0 at all** - a net8.0 loop is a control, not a
repro. net472 is a target framework only on Windows, and the net472 test binaries
run against the netstandard2.0 client build.

## Two signatures

- **FAIL** - the test failed. This is **not** automatically #1968. The script groups
  failures by reason and says so explicitly when none of them is an
  `OperationCanceledException`. Two things to watch for: an unrelated failure masks
  #1968 completely, because the run never reaches the close timeout; and a
  deterministic 100% rate is by definition not #1968, which is intermittent.
- **slow** - the test passed, but its duration exceeded `-SlowSeconds`. Worth
  counting separately because `Connection.CloseAsync` raises any non-abort timeout
  below `InternalConstants.DefaultConnectionCloseTimeout` (30s) up to 30s, so the
  test's own 6s `_waitSpan` is ignored. A run that waits out the full timeout is
  approaching the failure however it ends. Healthy runs finish in well under a
  second, so the 5s default is generous.

Outcome, duration and failure message all come from the trx (`--logger trx`), so
nothing depends on console formatting. That matters because the console output is
genuinely unusable for this: at default verbosity a net472 **pass** prints no
per-test line and leaves the summary `Duration:` field empty, while a **failure**
reports its time on a per-test line and also leaves the summary empty. Both cases
were misparsed before switching to the trx. The trx carries a `duration` attribute
either way, and it is the test's own time rather than process wall clock, which is
dominated by ~15s of per-iteration startup.

A trx with no `UnitTestResult` for the test is treated as a hard error rather than a
clean run, which covers both a renamed test (a filter matching nothing still exits 0)
and a test host that died before writing results. A skipped test is likewise an error,
not a pass.

## Running it

Native Windows PowerShell, with a broker reachable at `localhost:5672`. The test
fixture hardcodes that and has no host override, so under WSL2 this relies on
localhost forwarding from the Docker container started by
`.ci/ubuntu/gha-setup.sh`.

```powershell
.\projects\Applications\GH-1968\repro.ps1
.\projects\Applications\GH-1968\repro.ps1 -Count 100 -SlowSeconds 2
.\projects\Applications\GH-1968\repro.ps1 -Configuration Release -Count 10
```

`-Configuration` defaults to `Debug`, matching CI. This is not cosmetic: CI's
`integration-win32` job builds Debug and **passes** this test on net472, so Debug is
the only configuration with a known-green baseline to compare against. An earlier
version of this script defaulted to Release, copied from `GH-1960/repro.ps1`, which
made a Release-only failure look like a general one.

It builds net472 once, then uses `--no-build` in the loop. One `dotnet test` per
iteration means every iteration is a fresh process, which is deliberate: #1960
turned out to be a cold-start race on net472 that an in-process loop under-reported
as 1/N. It also means each iteration costs roughly 15s of startup, so a run of 25
takes several minutes.

Logs and trx files are kept only for failing and slow runs; passing runs are cleaned
up. The script classifies failures itself, so reading the logs is only needed when it
reports a reason as unrecognized.

## Status: blocked by a different net472 failure

Windows Release runs of this script came back 9/10 and 10/10 failures, none of them
#1968:

```
Assert.IsAssignableFrom() Failure: Value is an incompatible type
Expected: typeof(System.IO.IOException)
Actual:   typeof(System.ObjectDisposedException)
```

That is `TestConnectionShutdown.cs:73`, inside the `AlreadyClosedException` catch, so
the close threw the expected exception type but with the wrong `InnerException`. It is
deterministic and fast (~70-100ms, not the ~30s a close timeout would take), which
makes it a separate bug from the intermittent #1968.

Until that is understood, #1968 cannot be measured here at all: the assertion fails
long before the run reaches the close timeout.

Both of those runs were Release, whereas CI builds Debug and passes this test on
net472 (verified in run 30542231661, on a commit that contains the #1734 tracing
merge, which logs `Passed ... TestCleanClosureWithSocketClosedOutOfBand [2 ms]` from
`bin\Debug\net472\`). Configuration is the one systematic difference between CI and
those local runs, which is why the default is now Debug. The open question is whether
the `ObjectDisposedException` is Release-only, so run both:

```powershell
.\projects\Applications\GH-1968\repro.ps1 -Count 5
.\projects\Applications\GH-1968\repro.ps1 -Configuration Release -Count 5
```

If Debug passes and Release fails, it is a real configuration-dependent bug and wants
its own issue. If Debug fails too, suspect a stale `bin\` after branch switching
before suspecting the client.

## Open questions on the issue

A rate does not settle these, but it informs them:

1. Whether a bare `OperationCanceledException` is the right caller-visible
   exception for a clean-close timeout.
2. Whether the non-abort path should get the same up-front `CloseSocket()` that
   the `#if NETSTANDARD` block in `Connection.cs` applies only when `abort` is
   true. Cancelling `_mainLoopCts` does not interrupt a `PipeReader.ReadAsync`
   parked on a `NetworkStream` on .NET Framework, which is issue
   [#1921](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1921).
Whether the 30s floor overriding a caller-supplied 6s timeout is intended was the
third question. It was answerable from history without a repro, and is now filed
separately as
[#1973](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1973).
