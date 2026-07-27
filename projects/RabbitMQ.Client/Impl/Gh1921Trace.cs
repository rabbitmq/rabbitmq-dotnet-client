// TEMPORARY diagnostic for issue #1921 (fix/gh-1921-cancellation branch).
//
// This file exists only to split the ~10s net472 abort-path stall into its
// component awaits. It is inert unless the environment variable GH1921_TRACE=1
// is set, and MUST be reverted before the branch is merged.

using System;
using System.Diagnostics;

namespace RabbitMQ.Client.Impl
{
    internal static class Gh1921Trace
    {
        private static readonly bool s_enabled =
            Environment.GetEnvironmentVariable("GH1921_TRACE") == "1";

        private static readonly Stopwatch s_sw = Stopwatch.StartNew();

        public static bool Enabled => s_enabled;

        public static void Mark(string label)
        {
            if (s_enabled)
            {
                Console.Error.WriteLine($"[GH1921 {s_sw.Elapsed.TotalMilliseconds,10:F1}ms] {label}");
            }
        }
    }
}
