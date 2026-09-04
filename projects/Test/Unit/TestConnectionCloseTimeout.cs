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
    /// The floors applied to a caller-supplied close timeout are deliberate: the
    /// timeout feeds the same linked <see cref="CancellationTokenSource"/> as the
    /// caller's own token, so a zero or very small value would cancel the close
    /// handshake itself rather than bounding the wait for it, which is the
    /// <see cref="ObjectDisposedException"/> of #1802.
    ///
    /// <see cref="Timeout.InfiniteTimeSpan"/> is the exception. It means "take as
    /// long as needed", so it cannot cause that truncation, but because it is
    /// negative it compared as less than both floors and was silently lowered to a
    /// finite 30 seconds, making the documented infinite wait unreachable.
    ///
    /// These cases are asserted against the timeout resolution directly because the
    /// difference is otherwise unobservable: a healthy connection closes in roughly
    /// 175ms, so no close timeout is ever reached and the clamp leaves no trace.
    /// That is exactly why this went unnoticed, and why an integration test here
    /// would be vacuous.
    /// </summary>
    public class TestConnectionCloseTimeout
    {
        [Fact]
        public void InfiniteTimeSpanIsNotLoweredToTheCloseFloor_GH1973()
        {
            Assert.Equal(Timeout.InfiniteTimeSpan,
                Connection.ResolveCloseTimeout(Timeout.InfiniteTimeSpan, abort: false));
        }

        [Fact]
        public void AbortStaysBoundedWhenGivenInfiniteTimeSpan_GH1973()
        {
            /*
             * An abort's wait on the main loop uses the timeout alone, with the caller's
             * token deliberately neutralized, so an unbounded abort would make the forced
             * socket close unreachable and could hang for good when the main loop is
             * stranded. Abort therefore keeps its floor.
             */
            Assert.Equal(InternalConstants.DefaultConnectionAbortTimeout,
                Connection.ResolveCloseTimeout(Timeout.InfiniteTimeSpan, abort: true));
        }

        [Theory]
        [InlineData(-5000)]
        [InlineData(-2)]
        public void NegativeTimeoutOtherThanInfiniteIsRaisedToTheFloor_GH1973(int milliseconds)
        {
            /*
             * The exemption must match Timeout.InfiniteTimeSpan exactly, and no other negative
             * value, because the floors are what keep every other negative away from the
             * CancellationTokenSource. Do not assume the constructor would reject them: it
             * validates (long)delay.TotalMilliseconds >= -1, which truncates toward zero, so
             * anything strictly between -2ms and 0 is accepted and cancels within milliseconds,
             * which is the truncated-handshake failure of #1802 with no exception to flag it.
             * Without this case, loosening the check to `timeout < TimeSpan.Zero` would still pass.
             */
            TimeSpan timeout = TimeSpan.FromMilliseconds(milliseconds);
            Assert.NotEqual(Timeout.InfiniteTimeSpan, timeout);

            Assert.Equal(InternalConstants.DefaultConnectionCloseTimeout,
                Connection.ResolveCloseTimeout(timeout, abort: false));
            Assert.Equal(InternalConstants.DefaultConnectionAbortTimeout,
                Connection.ResolveCloseTimeout(timeout, abort: true));
        }

        [Fact]
        public void OverLargeCloseTimeoutIsClampedRatherThanThrowing_GH1973()
        {
            /*
             * CancellationTokenSource rejects a delay above its ceiling, so passing a larger value
             * through would throw out of its constructor before the close reason is set, leaving
             * the connection fully open. Such a value means "as long as possible", so it is clamped
             * to the ceiling. It is deliberately NOT promoted to InfiniteTimeSpan: that would turn
             * a bounded wait into one nothing can end, and would make the abort branch
             * non-monotonic.
             */
            Assert.Equal(Connection.s_maxCancellationTokenSourceDelay,
                Connection.ResolveCloseTimeout(TimeSpan.MaxValue, abort: false));
            Assert.Equal(Connection.s_maxCancellationTokenSourceDelay,
                Connection.ResolveCloseTimeout(TimeSpan.FromDays(60), abort: false));

            // An abort honours a large finite value as given, once clamped; only an unbounded or
            // too-small request resolves to the 5 second floor.
            Assert.Equal(Connection.s_maxCancellationTokenSourceDelay,
                Connection.ResolveCloseTimeout(TimeSpan.MaxValue, abort: true));
        }

        [Fact]
        public void ResolutionIsMonotonicAcrossTheCeiling_GH1973()
        {
            /*
             * Asking for more time must never yield less. When an over-large value was promoted to
             * InfiniteTimeSpan, an abort of 49 days resolved to 49 days while 50 days resolved to
             * the 5 second floor: a 5-order-of-magnitude reversal from one extra day.
             */
            TimeSpan ceiling = Connection.s_maxCancellationTokenSourceDelay;
            TimeSpan justUnder = ceiling - TimeSpan.FromDays(1);

            foreach (bool abort in new[] { false, true })
            {
                Assert.True(Connection.ResolveCloseTimeout(ceiling + TimeSpan.FromDays(1), abort)
                    >= Connection.ResolveCloseTimeout(justUnder, abort),
                    $"resolution went backwards across the ceiling (abort: {abort})");
            }
        }

        [Fact]
        public void TheCeilingItselfIsAcceptedAndOneTickAboveIsClamped_GH1973()
        {
            /*
             * The guard is `timeout > ceiling`, so the ceiling itself must pass through and the
             * smallest value above it must clamp. Without these two inputs the guard's boundary is
             * untested: mutating the ceiling to a value the runtime rejects, or the comparison to
             * >=, left the whole suite green, while the first of those reintroduces the
             * ArgumentOutOfRangeException-before-shutdown bug this test file exists for.
             */
            TimeSpan ceiling = Connection.s_maxCancellationTokenSourceDelay;

            Assert.Equal(ceiling, Connection.ResolveCloseTimeout(ceiling, abort: false));
            Assert.Equal(ceiling,
                Connection.ResolveCloseTimeout(ceiling + TimeSpan.FromTicks(1), abort: false));

            // The ceiling must be a value CancellationTokenSource actually accepts on this runtime.
            using var cts = new CancellationTokenSource(ceiling);
            Assert.False(cts.IsCancellationRequested);
        }

        [Fact]
        public void ResolvedTimeoutIsAlwaysAcceptedByCancellationTokenSource_GH1973()
        {
            /*
             * The resolved value is handed straight to a CancellationTokenSource, so every
             * resolution must be constructible. This is the invariant that the over-large
             * and negative handling above exists to maintain.
             */
            TimeSpan[] inputs =
            {
                Timeout.InfiniteTimeSpan, TimeSpan.Zero, TimeSpan.FromSeconds(6),
                TimeSpan.FromSeconds(60), TimeSpan.FromMilliseconds(-2),
                // FromDays(30) is above the .NET Framework CancellationTokenSource limit
                // (~24.86 days) but below the modern .NET limit, so it is what catches a
                // miscalibrated MaxCancellationTokenSourceDelay on net472. See #1973.
                TimeSpan.FromDays(30),
                TimeSpan.FromDays(60), TimeSpan.MaxValue, TimeSpan.MinValue
            };

            foreach (TimeSpan input in inputs)
            {
                foreach (bool abort in new[] { false, true })
                {
                    TimeSpan resolved = Connection.ResolveCloseTimeout(input, abort);

                    /*
                     * Assert on the resolved value, not on cts.IsCancellationRequested. On .NET
                     * Framework a zero delay arms a timer rather than completing the source
                     * immediately, so reading the flag there is a race that reports false even for
                     * a resolution that cancels microseconds later. A resolution must be either
                     * unbounded or strictly positive.
                     */
                    Assert.True(resolved == Timeout.InfiniteTimeSpan || resolved > TimeSpan.Zero,
                        $"resolved timeout {resolved} for input {input} (abort: {abort}) would " +
                        "cancel the close handshake rather than bound the wait for it");

                    using var cts = new CancellationTokenSource(resolved);
                }
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(6)]
        [InlineData(29)]
        public void SmallCloseTimeoutIsRaisedToTheCloseFloor_GH1973(int seconds)
        {
            Assert.Equal(InternalConstants.DefaultConnectionCloseTimeout,
                Connection.ResolveCloseTimeout(TimeSpan.FromSeconds(seconds), abort: false));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(4)]
        public void SmallAbortTimeoutIsRaisedToTheAbortFloor_GH1973(int seconds)
        {
            Assert.Equal(InternalConstants.DefaultConnectionAbortTimeout,
                Connection.ResolveCloseTimeout(TimeSpan.FromSeconds(seconds), abort: true));
        }

        [Fact]
        public void TimeoutAboveTheFloorIsUsedAsGiven_GH1973()
        {
            TimeSpan timeout = TimeSpan.FromSeconds(60);

            Assert.Equal(timeout, Connection.ResolveCloseTimeout(timeout, abort: false));
            Assert.Equal(timeout, Connection.ResolveCloseTimeout(timeout, abort: true));
        }
    }
}
