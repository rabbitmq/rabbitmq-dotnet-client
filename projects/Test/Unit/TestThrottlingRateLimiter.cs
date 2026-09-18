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
using System.Diagnostics;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Xunit;

namespace Test.Unit
{
    /// <summary>
    /// Two behaviours of <see cref="ThrottlingRateLimiter"/> that #1988 changed and that nothing
    /// else covers: that an async dispose really disposes it, and that a cancelled acquisition does
    /// not consume a permit for good. Both are cheap and need no broker.
    /// </summary>
    public class TestThrottlingRateLimiter
    {
        [Fact]
        public async Task DisposeAsyncActuallyDisposesTheLimiter_GH1988()
        {
            /*
             * RateLimiter.DisposeAsync routes to DisposeAsyncCore, not to Dispose(bool), so a
             * subclass that overrides only the synchronous path is left fully usable after an
             * `await using` block. This limiter is now the default for publisher confirmations and
             * the channel no longer disposes it, so the owner's own DisposeAsync is the only thing
             * that ever will.
             */
            var limiter = new ThrottlingRateLimiter(4);
            await limiter.DisposeAsync();

            await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await limiter.AcquireAsync(1));
        }

        [Fact]
        public async Task ACancelledAcquisitionDoesNotConsumeAPermit_GH1988()
        {
            /*
             * The throttle delay observes the caller's token, and it runs after the permit has been
             * taken but before the lease reaches the caller, so a cancellation there would leave the
             * caller with nothing to dispose and the permit held forever. The limiter is shared
             * across every channel built from one CreateChannelOptions and across every recovery, so
             * the loss is process-lifetime rather than per-channel.
             *
             * Throttling starts only once available permits fall below maxConcurrentCalls * pct /
             * 100, so a low percentage never throttles at all: at 1% the threshold is 0 and the
             * measured delay is 0 ms. At 100% the threshold is the whole limit, so the delay is
             * entered from the first acquisition - measured at 255 ms for 4 permits, and the delay
             * grows as permits are taken. Cancelling at 30 ms therefore lands inside it, and the
             * elapsed-time guard below is what proves that rather than assuming it.
             */
            const int MaxPermits = 4;
            using var limiter = new ThrottlingRateLimiter(MaxPermits, throttlingPercentage: 100);

            // Hold one permit so the second acquisition throttles for longer still.
            using RateLimitLease held = await limiter.AcquireAsync(1);
            long before = limiter.GetStatistics().CurrentAvailablePermits;

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(30));

            var stopwatch = Stopwatch.StartNew();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await limiter.AcquireAsync(1, cts.Token));
            stopwatch.Stop();

            Assert.True(stopwatch.ElapsedMilliseconds >= 20,
                $"the acquisition was cancelled after {stopwatch.ElapsedMilliseconds} ms, too early to " +
                "have reached the throttle delay, so this test is not exercising the path where a " +
                "permit is already held when the cancellation arrives");

            Assert.Equal(before, limiter.GetStatistics().CurrentAvailablePermits);
        }
    }
}
