// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       https://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v2.0:
//
//---------------------------------------------------------------------------
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//---------------------------------------------------------------------------

// Benchmark harness for PR #1913. Publishes a fixed-size body via
// BasicPublishAsync with publisher confirms (tracking enabled) for a
// configured duration, then reports throughput and allocation stats in
// a YAML-ish format suitable for automated parsing and human reading.
//
// Usage:
//   dotnet run -c Release --project projects/Applications/GH-1913-Benchmark -- \
//     --uri amqp://guest:guest@localhost:5672/ \
//     --duration 30 --warmup 5 --iterations 3 --body-size 256 \
//     --label with-catch
//
// Runs a durable queue declaration at startup, publishes to the default
// exchange with routing-key = queue name, mandatory = true. Any basic.return
// or channel/connection shutdown during the run aborts the process.

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

int exitCode = 0;
try
{
    var options = BenchOptions.Parse(args);
    await RunAsync(options);
}
catch (UsageException ue)
{
    Console.Error.WriteLine(ue.Message);
    Console.Error.WriteLine();
    Console.Error.WriteLine(BenchOptions.Usage);
    exitCode = 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
    Console.Error.WriteLine(ex);
    exitCode = 1;
}
return exitCode;

static async Task RunAsync(BenchOptions opt)
{
    EmitHeader(opt);

    var factory = new ConnectionFactory
    {
        Uri = new Uri(opt.Uri),
        AutomaticRecoveryEnabled = false,
        TopologyRecoveryEnabled = false,
        ClientProvidedName = "GH-1913-Benchmark",
    };

    await using IConnection conn = await factory.CreateConnectionAsync();

    // Flips to false just before we start closing the channel/connection, so
    // the shutdown handlers below can tell "unexpected shutdown during run"
    // from "expected shutdown during teardown."
    bool running = true;

    // Abort on any connection-level trouble during a measured run.
    conn.ConnectionShutdownAsync += (_, ea) =>
    {
        if (running)
        {
            Console.Error.WriteLine($"FATAL: connection shutdown during run: {ea.ReplyCode} {ea.ReplyText}");
            Environment.Exit(1);
        }
        return Task.CompletedTask;
    };

    var channelOptions = new CreateChannelOptions(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: opt.Tracking,
        outstandingPublisherConfirmationsRateLimiter: null);

    await using IChannel channel = await conn.CreateChannelAsync(channelOptions);

    // Abort on any channel-level trouble during a measured run.
    channel.ChannelShutdownAsync += (_, ea) =>
    {
        if (running)
        {
            Console.Error.WriteLine($"FATAL: channel shutdown during run: {ea.ReplyCode} {ea.ReplyText}");
            Environment.Exit(1);
        }
        return Task.CompletedTask;
    };

    // Abort on any basic.return (mandatory=true but unroutable).
    channel.BasicReturnAsync += (_, ea) =>
    {
        Console.Error.WriteLine($"FATAL: basic.return {ea.ReplyCode} {ea.ReplyText} exchange={ea.Exchange} routingKey={ea.RoutingKey}");
        Environment.Exit(1);
        return Task.CompletedTask;
    };

    string queueName = $"gh-1913-bench-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(0, 1_000_000):D6}";
    Dictionary<string, object?>? queueArgs = opt.QueueType switch
    {
        "quorum" => new Dictionary<string, object?> { ["x-queue-type"] = "quorum" },
        "classic" => null,
        _ => throw new UsageException($"Unsupported --queue-type: '{opt.QueueType}' (expected 'classic' or 'quorum')."),
    };
    await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: queueArgs);

    try
    {
        // Warm-up: publish for `warmup` seconds, discard counters.
        Console.WriteLine($"# warm-up ({opt.WarmupSeconds}s)...");
        await PublishForDurationAsync(channel, queueName, opt.BodySize, TimeSpan.FromSeconds(opt.WarmupSeconds), measure: false);

        for (int i = 1; i <= opt.Iterations; i++)
        {
            Console.WriteLine($"# iteration {i}/{opt.Iterations} ({opt.DurationSeconds}s)...");

            // Force a full GC between iterations so each measurement starts clean.
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

            IterationResult r = await PublishForDurationAsync(channel, queueName, opt.BodySize, TimeSpan.FromSeconds(opt.DurationSeconds), measure: true);
            EmitIteration(i, r, opt);
        }
    }
    finally
    {
        running = false;

        try
        {
            await channel.QueueDeleteAsync(queue: queueName, ifUnused: false, ifEmpty: false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"WARN: queue delete failed: {ex.Message}");
        }

        await channel.CloseAsync();
        await conn.CloseAsync();
    }
}

static async Task<IterationResult> PublishForDurationAsync(
    IChannel channel,
    string queueName,
    int bodySize,
    TimeSpan duration,
    bool measure)
{
    byte[] body = new byte[bodySize];
    Random.Shared.NextBytes(body);
    ReadOnlyMemory<byte> bodyMem = body;
    var props = new BasicProperties { DeliveryMode = DeliveryModes.Persistent };

    long gen0Before = 0, gen1Before = 0, gen2Before = 0;
    long allocatedBefore = 0;
    if (measure)
    {
        gen0Before = GC.CollectionCount(0);
        gen1Before = GC.CollectionCount(1);
        gen2Before = GC.CollectionCount(2);
        allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
    }

    long messages = 0;
    var sw = Stopwatch.StartNew();
    TimeSpan target = duration;

    while (sw.Elapsed < target)
    {
        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: queueName,
            mandatory: true,
            basicProperties: props,
            body: bodyMem);
        messages++;
    }

    sw.Stop();

    IterationResult r = default;
    r.Messages = messages;
    r.Elapsed = sw.Elapsed;

    if (measure)
    {
        r.Gen0 = GC.CollectionCount(0) - gen0Before;
        r.Gen1 = GC.CollectionCount(1) - gen1Before;
        r.Gen2 = GC.CollectionCount(2) - gen2Before;
        r.AllocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
    }

    return r;
}

static void EmitHeader(BenchOptions opt)
{
    string clientVersion = typeof(IChannel).Assembly.GetName().Version?.ToString() ?? "unknown";
    string infoVersion = typeof(IChannel).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "unknown";

    Console.WriteLine("bench:");
    Console.WriteLine($"  label: \"{opt.Label}\"");
    Console.WriteLine($"  uri: \"{opt.Uri}\"");
    Console.WriteLine($"  body_size: {opt.BodySize}");
    Console.WriteLine($"  queue_type: \"{opt.QueueType}\"");
    Console.WriteLine($"  tracking: {opt.Tracking.ToString().ToLowerInvariant()}");
    Console.WriteLine($"  warmup_seconds: {opt.WarmupSeconds}");
    Console.WriteLine($"  duration_seconds: {opt.DurationSeconds}");
    Console.WriteLine($"  iterations: {opt.Iterations}");
    Console.WriteLine($"  client_version: \"{clientVersion}\"");
    Console.WriteLine($"  client_informational_version: \"{infoVersion}\"");
    Console.WriteLine($"  dotnet_version: \"{Environment.Version}\"");
    Console.WriteLine($"  runtime_identifier: \"{System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}\"");
    Console.WriteLine($"  os_description: \"{System.Runtime.InteropServices.RuntimeInformation.OSDescription}\"");
    Console.WriteLine($"  processor_count: {Environment.ProcessorCount}");
    Console.WriteLine($"  is_server_gc: {System.Runtime.GCSettings.IsServerGC}");
    Console.WriteLine();
}

static void EmitIteration(int index, IterationResult r, BenchOptions opt)
{
    double seconds = r.Elapsed.TotalSeconds;
    double mps = seconds > 0 ? r.Messages / seconds : 0;
    double bytesPerMsg = r.Messages > 0 ? (double)r.AllocatedBytes / r.Messages : 0;

    Console.WriteLine($"iteration:");
    Console.WriteLine($"  index: {index}");
    Console.WriteLine($"  label: \"{opt.Label}\"");
    Console.WriteLine($"  duration_seconds: {seconds.ToString("F4", CultureInfo.InvariantCulture)}");
    Console.WriteLine($"  messages_published: {r.Messages}");
    Console.WriteLine($"  messages_per_second: {mps.ToString("F2", CultureInfo.InvariantCulture)}");
    Console.WriteLine($"  gc_gen0: {r.Gen0}");
    Console.WriteLine($"  gc_gen1: {r.Gen1}");
    Console.WriteLine($"  gc_gen2: {r.Gen2}");
    Console.WriteLine($"  allocated_bytes_total: {r.AllocatedBytes}");
    Console.WriteLine($"  allocated_bytes_per_message: {bytesPerMsg.ToString("F2", CultureInfo.InvariantCulture)}");
    Console.WriteLine();
}

struct IterationResult
{
    public long Messages;
    public TimeSpan Elapsed;
    public long Gen0;
    public long Gen1;
    public long Gen2;
    public long AllocatedBytes;
}

sealed class BenchOptions
{
    public string Uri { get; init; } = "amqp://guest:guest@localhost:5672/";
    public int BodySize { get; init; } = 256;
    public int WarmupSeconds { get; init; } = 5;
    public int DurationSeconds { get; init; } = 30;
    public int Iterations { get; init; } = 5;
    public string QueueType { get; init; } = "quorum";
    public bool Tracking { get; init; } = false;
    public string Label { get; init; } = "unlabeled";

    public const string Usage =
        "Options:\n" +
        "  --uri <amqp-uri>         AMQP URI (default amqp://guest:guest@localhost:5672/)\n" +
        "  --body-size <bytes>      Message body size (default 256)\n" +
        "  --warmup <seconds>       Warm-up duration (default 5)\n" +
        "  --duration <seconds>     Measured iteration duration (default 30)\n" +
        "  --iterations <count>     Number of measured iterations (default 5)\n" +
        "  --queue-type <classic|quorum>\n" +
        "                           Queue type to declare (default quorum)\n" +
        "  --tracking               Enable publisher-confirm tracking\n" +
        "                           (default disabled; each publish returns as soon as\n" +
        "                           the frame is enqueued rather than awaiting the confirm)\n" +
        "  --label <text>           Free-form label emitted in output (default \"unlabeled\")";

    public static BenchOptions Parse(string[] args)
    {
        string uri = "amqp://guest:guest@localhost:5672/";
        int bodySize = 256;
        int warmup = 5;
        int duration = 30;
        int iterations = 5;
        string queueType = "quorum";
        bool tracking = false;
        string label = "unlabeled";

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--uri":
                    uri = RequireValue(args, ref i);
                    break;
                case "--body-size":
                    bodySize = ParsePositive(RequireValue(args, ref i), nameof(BodySize));
                    break;
                case "--warmup":
                    warmup = ParseNonNegative(RequireValue(args, ref i), nameof(WarmupSeconds));
                    break;
                case "--duration":
                    duration = ParsePositive(RequireValue(args, ref i), nameof(DurationSeconds));
                    break;
                case "--iterations":
                    iterations = ParsePositive(RequireValue(args, ref i), nameof(Iterations));
                    break;
                case "--queue-type":
                    queueType = RequireValue(args, ref i);
                    break;
                case "--tracking":
                    tracking = true;
                    break;
                case "--label":
                    label = RequireValue(args, ref i);
                    break;
                case "-h":
                case "--help":
                    throw new UsageException("(help requested)");
                default:
                    throw new UsageException($"Unknown argument: {args[i]}");
            }
        }

        return new BenchOptions
        {
            Uri = uri,
            BodySize = bodySize,
            WarmupSeconds = warmup,
            DurationSeconds = duration,
            Iterations = iterations,
            QueueType = queueType,
            Tracking = tracking,
            Label = label,
        };
    }

    static string RequireValue(string[] args, ref int i)
    {
        if (i + 1 >= args.Length)
        {
            throw new UsageException($"Argument {args[i]} requires a value.");
        }
        return args[++i];
    }

    static int ParsePositive(string s, string name)
    {
        if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) || v <= 0)
        {
            throw new UsageException($"Invalid value for {name}: '{s}' (expected a positive integer).");
        }
        return v;
    }

    static int ParseNonNegative(string s, string name)
    {
        if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) || v < 0)
        {
            throw new UsageException($"Invalid value for {name}: '{s}' (expected a non-negative integer).");
        }
        return v;
    }
}

sealed class UsageException : Exception
{
    public UsageException(string message) : base(message) { }
}
