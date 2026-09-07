// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//
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
//
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Threading;
using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using Xunit;

namespace Test.Unit
{
    /// <summary>
    /// rabbitmq/rabbitmq-dotnet-client#1973
    ///
    /// <see cref="Connection.ResolveCloseTimeout"/> honours what the caller asked for. The 30 second
    /// floor it used to apply to every graceful close was not policy: it arrived as incidental
    /// scaffolding in PR #1809 while fixing an unrelated <see cref="ObjectDisposedException"/>, and
    /// it made <see cref="Timeout.InfiniteTimeSpan"/> unreachable (-1ms compares below any
    /// floor) while silently defeating the #1759 regression test, which closes with
    /// <see cref="TimeSpan.Zero"/>.
    ///
    /// An abort is the one path that is always bounded, between 5 and 10 seconds, because its wait
    /// uses the timeout alone with the caller's token neutralized.
    ///
    /// A one second minimum survives, and it is not the old floor in miniature. The timeout is linked
    /// into the tokens passed to the close handshake itself, so a value too small to reach them
    /// cancels the close before it transmits anything - and that cancellation escapes before the
    /// teardown block, leaving the socket open and the broker still holding the connection while
    /// <c>IsOpen</c> reports false. Measured. See <see cref="InternalConstants.MinConnectionCloseTimeout"/>.
    ///
    /// These cases assert against the resolution directly because the difference is otherwise
    /// unobservable on a healthy connection, which closes in roughly 20ms so no close timeout is ever
    /// reached. That is exactly why the floor went unnoticed for so long. The end-to-end consequence
    /// is covered by <c>TestGitHubIssues.DisposeWhileCatchingTimeoutDeadlocksRepro_GH1759</c>, which
    /// fails on a build without the minimum.
    /// </summary>
    public class TestConnectionCloseTimeout
    {
        [Fact]
        public void InfiniteTimeSpanIsHonouredByAGracefulClose_GH1973()
        {
            Assert.Equal(Timeout.InfiniteTimeSpan,
                Connection.ResolveCloseTimeout(Timeout.InfiniteTimeSpan, abort: false));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(6)]
        [InlineData(29)]
        [InlineData(30)]
        [InlineData(3600)]
        public void GracefulCloseHonoursTheCallerSTimeout_GH1973(int seconds)
        {
            /*
             * The headline of #1973: every one of these used to resolve to 30 seconds. The 6 second
             * case is the one the issue calls out by name. One second is included because it is the
             * minimum, and must be honoured rather than raised.
             */
            TimeSpan timeout = TimeSpan.FromSeconds(seconds);

            Assert.Equal(timeout, Connection.ResolveCloseTimeout(timeout, abort: false));
        }

        [Fact]
        public void ZeroResolvesToTheMinimumRatherThanCancellingTheClose_GH1973()
        {
            /*
             * TimeSpan.Zero is the input #1759's regression test uses, and the one that made this
             * minimum necessary. Resolved literally it produces a pre-cancelled CancellationTokenSource,
             * so the close cancels itself before sending connection.close, escapes before the teardown
             * block, and leaves the connection open on the broker - measured, with the client reporting
             * IsOpen == false throughout. An abort is unaffected because its own floor is larger.
             */
            Assert.Equal(InternalConstants.MinConnectionCloseTimeout,
                Connection.ResolveCloseTimeout(TimeSpan.Zero, abort: false));
            Assert.Equal(InternalConstants.DefaultConnectionAbortTimeout,
                Connection.ResolveCloseTimeout(TimeSpan.Zero, abort: true));

            // The minimum has to be large enough to matter; zero would reintroduce the defect.
            Assert.True(InternalConstants.MinConnectionCloseTimeout > TimeSpan.Zero);
        }

        [Theory]
        [InlineData(-50000000)] // -5s:       rejected by CancellationTokenSource
        [InlineData(-20000)]    // -2ms:      rejected by CancellationTokenSource
        [InlineData(-10001)]    // -1.0001ms: ACCEPTED, and never cancels
        [InlineData(-9999)]     // -0.9999ms: ACCEPTED, and cancels immediately
        public void NegativeTimeoutOtherThanInfiniteResolvesToTheMinimum_GH1973(long ticks)
        {
            /*
             * A negative duration is not a wait, and it must not reach the CancellationTokenSource
             * constructor, so it resolves to the one second minimum - not to zero, which would cancel
             * the close before it transmits anything and leak the connection. The exemption must match
             * Timeout.InfiniteTimeSpan
             * exactly and no other negative value: loosening it to `timeout < TimeSpan.Zero`, or to
             * `(long)timeout.TotalMilliseconds == -1`, must fail here.
             *
             * The cases are chosen from measured constructor behaviour. The constructor validates
             * (long)delay.TotalMilliseconds >= -1, and that cast truncates toward zero, which splits
             * the sub-2ms negatives into two regimes with opposite hazards:
             *
             *   (-2ms, -1ms]  truncates to -1  -> accepted, timer never armed, never cancels.
             *                                     A silent unbounded close, the worse of the two.
             *   (-1ms, 0)     truncates to 0   -> accepted, cancels immediately.
             *
             * Anything at or below -2ms throws ArgumentOutOfRangeException instead, so the first two
             * cases alone would only exercise values the constructor already rejects loudly. Ticks
             * rather than milliseconds because an int millisecond parameter cannot express -1.0001ms.
             */
            TimeSpan timeout = TimeSpan.FromTicks(ticks);
            Assert.NotEqual(Timeout.InfiniteTimeSpan, timeout);

            Assert.Equal(InternalConstants.MinConnectionCloseTimeout,
                Connection.ResolveCloseTimeout(timeout, abort: false));
            Assert.Equal(InternalConstants.DefaultConnectionAbortTimeout,
                Connection.ResolveCloseTimeout(timeout, abort: true));
        }

        [Fact]
        public void OverLargeGracefulTimeoutIsClampedNotMadeUnbounded_GH1973()
        {
            /*
             * CancellationTokenSource rejects a delay above its limit, so passing such a value
             * through would throw out of its constructor before the close reason is set, leaving the
             * connection fully open with no shutdown attempted. It is clamped rather than promoted to
             * an unbounded wait: only an explicit Timeout.InfiniteTimeSpan should produce a wait that
             * nothing local can end, because by then CloseAsync's finally has stopped the heartbeat
             * timers and NetworkStream ignores its read timeout for asynchronous reads, leaving only
             * the broker's own detection of our silence - which never comes if heartbeats are off.
             */
            TimeSpan ceiling = Connection.s_maxCancellationTokenSourceDelay;

            Assert.Equal(ceiling, Connection.ResolveCloseTimeout(TimeSpan.MaxValue, abort: false));
            Assert.Equal(ceiling, Connection.ResolveCloseTimeout(TimeSpan.FromDays(60), abort: false));

            // Only the explicit spelling is unbounded.
            Assert.Equal(Timeout.InfiniteTimeSpan,
                Connection.ResolveCloseTimeout(Timeout.InfiniteTimeSpan, abort: false));

            // The largest value still expressible as a bound is honoured as given.
            Assert.Equal(ceiling, Connection.ResolveCloseTimeout(ceiling, abort: false));
            Assert.Equal(ceiling,
                Connection.ResolveCloseTimeout(ceiling + TimeSpan.FromMilliseconds(1), abort: false));

            /*
             * The ceiling must be accepted by every runtime this build can load on, which means the
             * .NET Framework limit rather than the modern one. Raising it to uint.MaxValue - 1 ms
             * would still be accepted here on net8.0, so asserting only "the ceiling constructs" is
             * unfalsifiable on the default CI leg - as is comparing the constant to its own
             * initializer. Pin the property that actually matters instead: the ceiling is exactly
             * int.MaxValue milliseconds, the largest value .NET Framework accepts, and the net472 leg
             * of this project proves the constructor agrees.
             */
            Assert.Equal(int.MaxValue, (long)ceiling.TotalMilliseconds);

            using (var cts = new CancellationTokenSource(ceiling))
            {
                Assert.False(cts.IsCancellationRequested);
            }
        }

        [Fact]
        public void AbortIsAlwaysBoundedBetweenItsFloorAndCeiling_GH1973()
        {
            /*
             * An abort is best-effort teardown that never throws, so its value to a caller is that it
             * returns promptly. Two spellings of "wait as long as it takes" must not diverge: before
             * the ceiling, AbortAsync(TimeSpan.MaxValue) resolved to roughly 49.7 days while
             * AbortAsync(Timeout.InfiniteTimeSpan) resolved to 5 seconds.
             */
            TimeSpan floor = InternalConstants.DefaultConnectionAbortTimeout;
            TimeSpan ceiling = InternalConstants.MaxConnectionAbortTimeout;

            Assert.True(ceiling > floor, "the abort ceiling must leave room above the floor");

            foreach (TimeSpan unbounded in new[] { Timeout.InfiniteTimeSpan, TimeSpan.MaxValue, TimeSpan.FromDays(60) })
            {
                Assert.Equal(ceiling, Connection.ResolveCloseTimeout(unbounded, abort: true));
            }

            /*
             * A value inside the band is honored as given; outside it is clamped to the nearer end.
             * Ticks arithmetic rather than `(ceiling - floor) / 2`: the TimeSpan division operators
             * were added in .NET Core 2.0 and do not exist on .NET Framework, so the latter compiles
             * on net8.0 and breaks the net472 target of this project.
             */
            TimeSpan inBand = floor + TimeSpan.FromTicks((ceiling - floor).Ticks / 2);
            Assert.Equal(inBand, Connection.ResolveCloseTimeout(inBand, abort: true));
            Assert.Equal(floor, Connection.ResolveCloseTimeout(TimeSpan.Zero, abort: true));
            Assert.Equal(floor, Connection.ResolveCloseTimeout(floor - TimeSpan.FromSeconds(1), abort: true));
            Assert.Equal(ceiling, Connection.ResolveCloseTimeout(ceiling + TimeSpan.FromSeconds(1), abort: true));

            // No abort resolution may exceed the ceiling, whatever was asked for.
            foreach (TimeSpan input in new[]
                     {
                         Timeout.InfiniteTimeSpan, TimeSpan.MinValue, TimeSpan.Zero,
                         TimeSpan.FromSeconds(7), TimeSpan.FromHours(1), TimeSpan.MaxValue
                     })
            {
                TimeSpan resolved = Connection.ResolveCloseTimeout(input, abort: true);
                Assert.InRange(resolved, floor, ceiling);
            }
        }

        [Fact]
        public void ResolutionIsMonotonic_GH1973()
        {
            /*
             * Asking for more time must never yield less. This is the property that the earlier
             * promote-to-unbounded-then-floor ordering broke: an abort of 49 days resolved to 49 days
             * while 50 days resolved to the 5 second floor, a five-order-of-magnitude reversal from
             * one extra day. Unbounded counts as the largest value, so it is compared separately.
             */
            /*
             * The last three elements are the point: without a value strictly above the ceiling,
             * neither the clamp nor an accidental promote-to-unbounded is ever exercised, and
             * reinstating the ordering this test guards leaves it green.
             */
            TimeSpan[] ascending =
            {
                TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(6),
                TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5), TimeSpan.FromDays(1),
                Connection.s_maxCancellationTokenSourceDelay,
                Connection.s_maxCancellationTokenSourceDelay + TimeSpan.FromDays(1),
                TimeSpan.FromDays(60), TimeSpan.MaxValue
            };

            foreach (bool abort in new[] { false, true })
            {
                for (int i = 1; i < ascending.Length; i++)
                {
                    TimeSpan lower = Connection.ResolveCloseTimeout(ascending[i - 1], abort);
                    TimeSpan higher = Connection.ResolveCloseTimeout(ascending[i], abort);

                    Assert.True(higher >= lower,
                        $"resolution went backwards between {ascending[i - 1]} and {ascending[i]} " +
                        $"(abort: {abort}): {lower} then {higher}");
                }
            }
        }

        [Fact]
        public void ResolvedTimeoutIsAlwaysAcceptedByCancellationTokenSource_GH1973()
        {
            /*
             * The resolved value is handed straight to a CancellationTokenSource, so every
             * resolution must be constructible. This is the invariant that the over-large and
             * negative handling exists to maintain.
             */
            TimeSpan[] inputs =
            {
                Timeout.InfiniteTimeSpan, TimeSpan.Zero, TimeSpan.FromSeconds(6),
                TimeSpan.FromSeconds(60), TimeSpan.FromMilliseconds(-2), TimeSpan.FromTicks(-10001),
                TimeSpan.FromDays(30), TimeSpan.FromDays(60), TimeSpan.MaxValue, TimeSpan.MinValue
            };

            foreach (TimeSpan input in inputs)
            {
                foreach (bool abort in new[] { false, true })
                {
                    TimeSpan resolved = Connection.ResolveCloseTimeout(input, abort);

                    /*
                     * Assert on the resolved value, not on cts.IsCancellationRequested: on .NET
                     * Framework a zero delay arms a timer rather than completing the source
                     * immediately, so reading the flag there is a race. Zero is a legitimate
                     * resolution for a graceful close - it means "do not wait" - so the invariant is
                     * that the value is either unbounded or non-negative.
                     */
                    Assert.True(resolved == Timeout.InfiniteTimeSpan || resolved >= TimeSpan.Zero,
                        $"resolved timeout {resolved} for input {input} (abort: {abort}) is neither " +
                        "unbounded nor a usable duration");

                    using var cts = new CancellationTokenSource(resolved);
                }
            }
        }
    }
}
