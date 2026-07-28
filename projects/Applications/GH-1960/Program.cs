using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

// One-off repro for issue #1960: the net472-only flaky test
// Test.Integration.TestConnectionShutdown.TestAbortWithSocketClosedOutOfBandAndCancellation.
//
// The test:
//   1. cts = CancellationTokenSource(1s)
//   2. registers _channel.ChannelShutdownAsync that awaits
//      Task.Delay(1min, args.CancellationToken) and completes a TCS only when
//      that delay is cancelled (OperationCanceledException).
//   3. registers _conn.ConnectionShutdownAsync that TrySetResult(true) on the
//      SAME TCS (a fallback path; the test logs "[ERROR]" if this one wins).
//   4. closes the frame handler out of band (AutorecoveringConnection.CloseFrameHandlerAsync).
//   5. await _conn.AbortAsync(cts.Token)
//   6. waits up to 6s for the TCS, then up to 6s for the frame-handler close.
//
// It intermittently times out at the 6s TCS wait on the net472 (netstandard2.0)
// client on the integration-win32 CI job, and cannot be reproduced on Linux/WSL.
//
// GATHER DATA, DO NOT ASSUME A FIX. The load-bearing question raised by reading
// the abort path in Connection.CloseAsync / Connection.Receive.cs is: when the
// out-of-band socket close makes MainLoop win SetCloseReason first, the channel
// shutdown handler receives a Library-initiated reason carrying _mainLoopCts.Token
// instead of the abort's 1s token, so its parked Task.Delay may never cancel in
// the 6s window; and because shutdown handlers run SEQUENTIALLY
// (AsyncEventingWrapper), the ConnectionShutdownAsync fallback cannot complete
// the TCS while the channel handler is parked. This app records, per attempt:
//
//   * which shutdown reason the CHANNEL handler saw: Initiator (Application vs
//     Library), ReplyCode, ReplyText
//   * whether that handler's token CanBeCanceled, and IsCancellationRequested on entry
//   * how the TCS was completed: "channel-delay-cancelled", "connection-fallback",
//     or "TIMEOUT (6s)"
//   * elapsed time to TCS completion
//
// Run under BOTH frameworks on native Windows against a reachable broker (the WSL
// docker container publishes 5672 to the Windows host, so `localhost` works):
//
//   dotnet run -c Release -f net8.0   -- localhost
//   dotnet run -c Release -f net472   -- localhost
//
// The optional first argument is the broker hostname (default localhost). The
// optional second argument is the iteration count (default 50).

string host = args.Length > 0 ? args[0] : "localhost";
int iterations = args.Length > 1 && int.TryParse(args[1], out int n) ? n : 50;

string tfm =
#if NET48_OR_GREATER || NET472
    "net472 (netstandard2.0 client)";
#elif NET
    "net8.0 (net8.0 client)";
#else
    "unknown";
#endif

Console.WriteLine($"GH-1960 repro  |  TFM: {tfm}  |  host: {host}  |  iterations: {iterations}");
Console.WriteLine("Mirrors TestAbortWithSocketClosedOutOfBandAndCancellation: out-of-band");
Console.WriteLine("frame-handler close, then AbortAsync(1s token), wait 6s for a ChannelShutdownAsync");
Console.WriteLine("handler whose parked Task.Delay must be cancelled.");
Console.WriteLine();

// Reflect the internal AutorecoveringConnection.CloseFrameHandlerAsync() once.
MethodInfo? closeFrameHandler = null;

var outcomeHistogram = new Dictionary<string, int>();
var channelReasonHistogram = new Dictionary<string, int>();
int timeouts = 0;
double worstTcsSeconds = 0;

for (int i = 0; i < iterations; i++)
{
    var factory = new ConnectionFactory
    {
        HostName = host,
        AutomaticRecoveryEnabled = true,
        // Mirrors the fixture's ContinuationTimeout = WaitSpan; large enough that
        // a residual hang would be obvious, but the test's own bar is the 6s TCS wait.
        ContinuationTimeout = TimeSpan.FromSeconds(30),
        HandshakeContinuationTimeout = TimeSpan.FromSeconds(30)
    };

    IConnection conn = await factory.CreateConnectionAsync();
    IChannel channel = await conn.CreateChannelAsync();

    if (closeFrameHandler is null)
    {
        closeFrameHandler = conn.GetType().GetMethod("CloseFrameHandlerAsync",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (closeFrameHandler is null)
        {
            Console.WriteLine($"[FATAL] could not find CloseFrameHandlerAsync on {conn.GetType().FullName}");
            return;
        }
    }

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
    var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var sw = new Stopwatch();

    // Data captured by the channel shutdown handler.
    string channelReason = "(handler never invoked)";
    bool channelTokenCanBeCanceled = false;
    bool channelTokenCancelledOnEntry = false;
    double channelHandlerEnteredAt = -1;
    double channelTokenFiredAt = -1;
    // Identity of the reason objects, to see whether the channel and connection
    // handlers receive the SAME ShutdownEventArgs (i.e. the same close reason
    // propagated) or two different reason objects (a race between close paths).
    int channelReasonId = -1;
    int connReasonId = -1;
    string completionCause = "TIMEOUT (6s)";
    double abortReturnedAt = -1;
    int recoveryStarted = 0;
    int recoveryError = 0;

    // Record EVERY channel-handler invocation (recovery may swap the inner channel
    // and invoke the handler more than once). Each entry pairs with a "reason#<id>"
    // in the GH1960_TRACE library output, so the two logs can be cross-referenced.
    var channelInvocations = new List<string>();
    var invocationsLock = new object();

    // Detect whether the out-of-band close kicked off the autorecovery loop, which
    // races the abort. There is no "recovery started" event, so RecoverySucceeded /
    // RecoveryError are the observable proxies.
    conn.RecoverySucceededAsync += (c, ea) => { Interlocked.Increment(ref recoveryStarted); return Task.CompletedTask; };
    conn.ConnectionRecoveryErrorAsync += (c, ea) => { Interlocked.Increment(ref recoveryError); return Task.CompletedTask; };

    channel.ChannelShutdownAsync += async (ch, ea) =>
    {
        double enteredAt = sw.Elapsed.TotalSeconds;
        int reasonId = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(ea);
        bool canCancel = ea.CancellationToken.CanBeCanceled;
        bool cancelledOnEntry = ea.CancellationToken.IsCancellationRequested;
        lock (invocationsLock)
        {
            channelInvocations.Add(
                $"enter={enteredAt:F2}s reason#{reasonId} {ea.Initiator}/{ea.ReplyCode} token(canCancel={canCancel},cancelled={cancelledOnEntry})");
        }
        // First invocation still populates the single-value fields the summary uses.
        if (channelHandlerEnteredAt < 0)
        {
            channelHandlerEnteredAt = enteredAt;
            channelReason = $"{ea.Initiator}/{ea.ReplyCode}/{ea.ReplyText}";
            channelTokenCanBeCanceled = canCancel;
            channelTokenCancelledOnEntry = cancelledOnEntry;
            channelReasonId = reasonId;
        }
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), ea.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            channelTokenFiredAt = sw.Elapsed.TotalSeconds;
            if (tcs.TrySetResult(true))
            {
                completionCause = "channel-delay-cancelled";
            }
        }
    };

    conn.ConnectionShutdownAsync += (c, ea) =>
    {
        connReasonId = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(ea);
        if (tcs.TrySetResult(true))
        {
            completionCause = "connection-fallback";
        }
        return Task.CompletedTask;
    };

    // Out-of-band frame-handler close (same as the test).
    sw.Start();
    var closeTask = (ValueTask)closeFrameHandler.Invoke(conn, null)!;

    try
    {
        await conn.AbortAsync(cts.Token);
    }
    catch
    {
        // AbortAsync is best-effort; the test does not expect it to throw.
    }
    abortReturnedAt = sw.Elapsed.TotalSeconds;

    // Wait up to 6s for the TCS, exactly like the test's _waitSpan.
    Task completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(6)));
    sw.Stop();
    if (completed != tcs.Task)
    {
        timeouts++;
    }

    double tcsSeconds = sw.Elapsed.TotalSeconds;
    if (tcsSeconds > worstTcsSeconds) worstTcsSeconds = tcsSeconds;

    try { await closeTask; } catch { }
    try { await conn.DisposeAsync(); } catch { }

    Record(outcomeHistogram, completionCause);
    string canceledInfo = channelTokenCanBeCanceled
        ? (channelTokenCancelledOnEntry ? "cancellable,already-cancelled" : "cancellable,not-yet")
        : "NOT-cancellable";
    Record(channelReasonHistogram, $"{channelReason}  [{canceledInfo}]");

    // Only spell out the extra timing/identity detail for the interesting (slow or
    // fallback) attempts so the common 1s case stays a one-liner.
    bool interesting = tcsSeconds > 1.5 || completionCause != "channel-delay-cancelled";
    string sameReason = (channelReasonId != -1 && channelReasonId == connReasonId) ? "same-reason-obj"
        : (connReasonId == -1 ? "conn-handler-not-run" : "DIFFERENT-reason-objs");
    string detail = interesting
        ? $"  | chEnter={channelHandlerEnteredAt:F2}s tokFired={channelTokenFiredAt:F2}s abortRet={abortReturnedAt:F2}s"
          + $" recSucc={recoveryStarted} recErr={recoveryError} {sameReason}"
        : "";

    Console.WriteLine(
        $"#{i,3}  {completionCause,-24}  tcs={tcsSeconds,6:F2}s  channelReason={channelReason}  token={canceledInfo}{detail}");

    // For interesting attempts, dump every channel-handler invocation so a
    // recovery-driven double-invoke (fresh inner channel) is visible and each
    // line's reason#<id> cross-references the GH1960_TRACE library output.
    if (interesting)
    {
        List<string> snapshot;
        lock (invocationsLock) { snapshot = new List<string>(channelInvocations); }
        Console.WriteLine($"       channel handler invoked {snapshot.Count}x:");
        foreach (string inv in snapshot)
        {
            Console.WriteLine($"         - {inv}");
        }
    }
}

Console.WriteLine();
Console.WriteLine($"TIMEOUTS (>6s TCS wait): {timeouts}/{iterations}   worst tcs wait: {worstTcsSeconds:F2}s");
Console.WriteLine();
Console.WriteLine("TCS completion cause histogram:");
foreach (KeyValuePair<string, int> kv in outcomeHistogram)
{
    Console.WriteLine($"  {kv.Value,5}x  {kv.Key}");
}
Console.WriteLine();
Console.WriteLine("Channel shutdown reason (Initiator/Code/Text) + token state histogram:");
foreach (KeyValuePair<string, int> kv in channelReasonHistogram)
{
    Console.WriteLine($"  {kv.Value,5}x  {kv.Key}");
}

static void Record(Dictionary<string, int> histogram, string key)
{
    histogram.TryGetValue(key, out int count);
    histogram[key] = count + 1;
}
