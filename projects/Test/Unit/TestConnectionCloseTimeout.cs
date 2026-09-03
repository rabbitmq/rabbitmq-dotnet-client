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
             * The exemption must match Timeout.InfiniteTimeSpan exactly, and no other
             * negative value. Any other negative delay is rejected by the
             * CancellationTokenSource constructor, so it must not reach it. Without this
             * case, loosening the check to `timeout < TimeSpan.Zero` would still pass.
             */
            TimeSpan timeout = TimeSpan.FromMilliseconds(milliseconds);
            Assert.NotEqual(Timeout.InfiniteTimeSpan, timeout);

            Assert.Equal(InternalConstants.DefaultConnectionCloseTimeout,
                Connection.ResolveCloseTimeout(timeout, abort: false));
            Assert.Equal(InternalConstants.DefaultConnectionAbortTimeout,
                Connection.ResolveCloseTimeout(timeout, abort: true));
        }

        [Fact]
        public void OverLargeCloseTimeoutBecomesInfiniteRatherThanThrowing_GH1973()
        {
            /*
             * CancellationTokenSource rejects a delay above roughly 49.7 days. Passing
             * TimeSpan.MaxValue through would throw out of its constructor before the close
             * reason is set, leaving the connection fully open. Such a value means "wait
             * forever", so it resolves to InfiniteTimeSpan for a graceful close and to the
             * bounded floor for an abort.
             */
            Assert.Equal(Timeout.InfiniteTimeSpan,
                Connection.ResolveCloseTimeout(TimeSpan.MaxValue, abort: false));
            Assert.Equal(Timeout.InfiniteTimeSpan,
                Connection.ResolveCloseTimeout(TimeSpan.FromDays(60), abort: false));

            Assert.Equal(InternalConstants.DefaultConnectionAbortTimeout,
                Connection.ResolveCloseTimeout(TimeSpan.MaxValue, abort: true));
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
                TimeSpan.FromDays(60), TimeSpan.MaxValue, TimeSpan.MinValue
            };

            foreach (TimeSpan input in inputs)
            {
                foreach (bool abort in new[] { false, true })
                {
                    TimeSpan resolved = Connection.ResolveCloseTimeout(input, abort);
                    using var cts = new CancellationTokenSource(resolved);
                    Assert.False(cts.IsCancellationRequested,
                        $"resolved timeout {resolved} for input {input} (abort: {abort}) " +
                        "produced an already-cancelled token, which would cancel the close handshake");
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
