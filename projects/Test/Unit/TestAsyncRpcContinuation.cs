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
using System.Threading.Tasks;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Impl;
using Xunit;

namespace Test.Unit
{
    public class TestAsyncRpcContinuation
    {
        // rabbitmq/rabbitmq-dotnet-client#1964
        // Only one RPC runs per channel at a time, so a caller can sit behind an
        // arbitrary number of queued operations before its own frame is written. The
        // continuation timeout therefore does not start at construction; Channel starts
        // it once the RPC semaphore has been acquired and the operation can run.
        //
        // These are wall-clock tests, so they are written so that load can only ever make
        // them pass: every assertion is one-sided in the safe direction. The simulated wait
        // is comfortably longer than the timeout, and the one elapsed-time assertion has a
        // floor rather than a ceiling, so a delay that overruns is harmless while a timer
        // that fires early cannot happen. Do not tighten these towards each other.
        private static readonly TimeSpan s_continuationTimeout = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan s_simulatedSemaphoreWait = TimeSpan.FromSeconds(5);

        [Fact]
        public async Task TestTimeoutDoesNotStartAtConstruction()
        {
            using var k = new SimpleAsyncRpcContinuation(ProtocolCommandId.ExchangeDeclareOk,
                s_continuationTimeout, CancellationToken.None);

            // Stand in for a long wait on the channel's RPC semaphore. This is more
            // than twice s_continuationTimeout, so an armed-at-construction
            // continuation would already be cancelled by now.
            await Task.Delay(s_simulatedSemaphoreWait);

            Assert.False(k.CancellationToken.IsCancellationRequested);
        }

        [Fact]
        public async Task TestStartTimeoutGivesTheOperationTheFullTimeout()
        {
            using var k = new SimpleAsyncRpcContinuation(ProtocolCommandId.ExchangeDeclareOk,
                s_continuationTimeout, CancellationToken.None);

            await Task.Delay(s_simulatedSemaphoreWait);

            var stopwatch = Stopwatch.StartNew();
            k.StartTimeout();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await k);

            stopwatch.Stop();

            // The operation gets the full budget measured from StartTimeout, not from
            // construction. Allow generous slack for timer resolution and CI load; the
            // point is that the queueing delay was not charged to the operation, which
            // would show up as expiry well under s_continuationTimeout.
            TimeSpan floor = TimeSpan.FromMilliseconds(1500);
            Assert.True(stopwatch.Elapsed > floor,
                $"expected the operation to get its full timeout from StartTimeout, " +
                $"but it expired after only {stopwatch.Elapsed}");
        }

        [Fact]
        public async Task TestStartTimeoutStillTimesOut()
        {
            using var k = new SimpleAsyncRpcContinuation(ProtocolCommandId.ExchangeDeclareOk,
                s_continuationTimeout, CancellationToken.None);

            k.StartTimeout();

            // Starting the timeout must actually arm it, otherwise a stuck operation
            // would hang its caller forever
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await k);
        }

        [Fact]
        public void TestStartTimeoutAfterDisposeDoesNotThrow()
        {
            var k = new SimpleAsyncRpcContinuation(ProtocolCommandId.ExchangeDeclareOk,
                s_continuationTimeout, CancellationToken.None);
            k.Dispose();

            // A continuation that has already completed may still be started by a
            // racing caller; that must not surface an ObjectDisposedException
            k.StartTimeout();
        }

        [Fact]
        public void TestCallerTokenStillCancelsBeforeTimeoutStarts()
        {
            using var cts = new CancellationTokenSource();
            using var k = new SimpleAsyncRpcContinuation(ProtocolCommandId.ExchangeDeclareOk,
                s_continuationTimeout, cts.Token);

            // Before StartTimeout the caller's own token is the only thing bounding the
            // wait for the RPC semaphore, so it must still surface through the token
            // Channel passes to WaitAsync.
            cts.Cancel();

            Assert.True(k.CancellationToken.IsCancellationRequested);

            // Note: this deliberately does not await k. The continuation's task is
            // completed by a response, a channel shutdown, or the timeout callback, and
            // the caller's token is not registered against it. That is why StartTimeout
            // must be called before the operation is awaited, which Channel always does.
        }
    }
}
