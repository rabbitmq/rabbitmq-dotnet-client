// TEMPORARY diagnostic for issue #1960 (fix/gh-1921-cancellation branch).
//
// This file exists only to trace the lifecycle of ShutdownEventArgs reason
// objects during the abort/recovery race behind the net472-only flaky test
// TestAbortWithSocketClosedOutOfBandAndCancellation. It is inert unless the
// environment variable GH1960_TRACE=1 is set, and MUST be reverted before the
// branch is merged.
//
// It answers: when recovery races the abort, which reason object does each hop
// (MainLoop mint, Connection.SetCloseReason, SessionBase.CloseAsync store,
// Channel shutdown invoke, recovery TakeOver) see, and does that object carry a
// cancellable token? The repro app correlates the "chXXXXXX" reason id printed
// here with the id it reads off the ShutdownEventArgs in its own handler.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Impl
{
    internal static class Gh1960Trace
    {
        private static readonly bool s_enabled =
            Environment.GetEnvironmentVariable("GH1960_TRACE") == "1";

        private static readonly Stopwatch s_sw = Stopwatch.StartNew();

        public static bool Enabled => s_enabled;

        public static void Mark(string label)
        {
            if (s_enabled)
            {
                Console.Error.WriteLine(
                    $"[GH1960 {s_sw.Elapsed.TotalMilliseconds,10:F1}ms t{Thread.CurrentThread.ManagedThreadId,3}] {label}");
            }
        }

        // Compact description of a reason object: identity hash (matches the repro
        // app's RuntimeHelpers.GetHashCode), initiator, code, and token state.
        public static string Describe(ShutdownEventArgs? reason)
        {
            if (reason is null)
            {
                return "reason=<null>";
            }

            int id = RuntimeHelpers.GetHashCode(reason);
            bool canCancel = reason.CancellationToken.CanBeCanceled;
            bool cancelled = reason.CancellationToken.IsCancellationRequested;
            return $"reason#{id} {reason.Initiator}/{reason.ReplyCode} " +
                   $"token(canCancel={canCancel},cancelled={cancelled})";
        }

        public static void Mark(string label, ShutdownEventArgs? reason)
        {
            if (s_enabled)
            {
                Mark($"{label} {Describe(reason)}");
            }
        }
    }
}
