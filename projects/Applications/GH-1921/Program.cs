using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

// One-off repro for the Windows CI failures seen on fix/gh-1921-cancellation.
//
// The strengthened regression test
// (TestConnectionFactory.TestCreateConnectionAsync_CancellationDuringHandshake_CompletesQuickly)
// passes on Linux (0 slow / 2100) but fails two different ways on the win32 job:
//
//   net8.0 : throws AggregateException(SocketException "WSALookupServiceEnd ...
//            canceled") at ~3ms -- the test only catches OperationCanceledException,
//            so the exception escapes and fails the test.
//   net472 : one attempt at a 0us cancellation delay took 10.2s (> 3s bar).
//
// This app reproduces both: it sweeps the same cancellation delays, catches ALL
// exceptions (recording their types), and reports the slowest attempt plus an
// exception-type histogram. Run it under BOTH target frameworks on native
// Windows against a reachable broker (the WSL docker container is fine):
//
//   dotnet run -c Release -f net8.0   -- localhost
//   dotnet run -c Release -f net472   -- localhost
//
// The second (framework) argument is optional and only affects the printed label.

string host = args.Length > 0 ? args[0] : "localhost";

string tfm =
#if NET48_OR_GREATER || NET472
    "net472 (netstandard2.0 client)";
#elif NET
    "net8.0 (net8.0 client)";
#else
    "unknown";
#endif

Console.WriteLine($"GH-1921 repro  |  TFM: {tfm}  |  host: {host}");
Console.WriteLine("Sweeping cancellation delays; a large ContinuationTimeout makes any hang obvious.");
Console.WriteLine();

// Same sweep as the regression test, plus a few extra iterations to widen the window.
int[] delaysMicroseconds = { 0, 100, 250, 500, 750, 1000, 1500, 2000, 5000, 50000 };
const int iterationsPerDelay = 20;
const double slowThresholdSeconds = 3.0;

var exceptionHistogram = new Dictionary<string, int>();
double worstSeconds = 0;
int worstDelayMicroseconds = -1;
int totalSlow = 0;
int totalRuns = 0;

foreach (int delayMicroseconds in delaysMicroseconds)
{
    double maxSeconds = 0;
    int slow = 0;

    for (int i = 0; i < iterationsPerDelay; i++)
    {
        var factory = new ConnectionFactory
        {
            HostName = host,
            // Large on purpose: a residual hang would block for this long, so any
            // stall well under it but over the 3s bar is a distinct, real signal.
            ContinuationTimeout = TimeSpan.FromSeconds(30),
            HandshakeContinuationTimeout = TimeSpan.FromSeconds(30)
        };

        var sw = Stopwatch.StartNew();
        try
        {
            using var cts = new CancellationTokenSource(
                TimeSpan.FromTicks(delayMicroseconds * (TimeSpan.TicksPerMillisecond / 1000)));
            await using IConnection conn = await factory.CreateConnectionAsync(cts.Token);
            RecordException("(none - connection actually opened before cancel)");
        }
        catch (Exception ex)
        {
            RecordException(DescribeException(ex));
        }
        sw.Stop();

        totalRuns++;
        double seconds = sw.Elapsed.TotalSeconds;
        if (seconds > maxSeconds) maxSeconds = seconds;
        if (seconds > worstSeconds) { worstSeconds = seconds; worstDelayMicroseconds = delayMicroseconds; }
        if (seconds > slowThresholdSeconds) { slow++; totalSlow++; }
    }

    Console.WriteLine($"delay={delayMicroseconds,6}us   max={maxSeconds,7:F2}s   slow(>{slowThresholdSeconds:F0}s)={slow}/{iterationsPerDelay}");
}

Console.WriteLine();
Console.WriteLine($"WORST: {worstSeconds:F2}s at delay={worstDelayMicroseconds}us   totalSlow={totalSlow}/{totalRuns}");
Console.WriteLine();
Console.WriteLine("Exception types observed:");
foreach (KeyValuePair<string, int> kv in exceptionHistogram)
{
    Console.WriteLine($"  {kv.Value,5}x  {kv.Key}");
}

void RecordException(string key)
{
    exceptionHistogram.TryGetValue(key, out int count);
    exceptionHistogram[key] = count + 1;
}

// Produce a compact, stable signature: outer type plus the chain of inner types.
// This is what tells net8.0's AggregateException(SocketException) apart from a
// plain OperationCanceledException.
static string DescribeException(Exception ex)
{
    var parts = new List<string>();
    Exception? cur = ex;
    int depth = 0;
    while (cur != null && depth < 6)
    {
        if (cur is AggregateException agg && agg.InnerExceptions.Count > 0)
        {
            parts.Add($"AggregateException[{agg.InnerExceptions.Count}]");
            cur = agg.InnerExceptions[0];
        }
        else
        {
            parts.Add(cur.GetType().Name);
            cur = cur.InnerException;
        }
        depth++;
    }
    return string.Join(" -> ", parts);
}
