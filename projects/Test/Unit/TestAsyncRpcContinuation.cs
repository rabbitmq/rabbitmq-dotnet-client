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
        // A continuation's timeout starts at construction, which bounds the wait for
        // the channel's RPC semaphore. Once the semaphore is acquired the operation
        // still has not been sent, so the time spent queued must not be charged
        // against it. Channel restarts the timeout at that point.
        private static readonly TimeSpan s_continuationTimeout = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan s_simulatedSemaphoreWait = TimeSpan.FromSeconds(1);

        [Fact]
        public async Task TestRestartTimeout_GivesTheOperationTheFullTimeout()
        {
            using var k = new SimpleAsyncRpcContinuation(ProtocolCommandId.ExchangeDeclareOk,
                s_continuationTimeout, CancellationToken.None);

            var stopwatch = Stopwatch.StartNew();

            await Task.Delay(s_simulatedSemaphoreWait);

            // Precondition: the continuation must still be live, otherwise the delay
            // above overran the timeout and the test measures nothing
            Assert.False(k.CancellationToken.IsCancellationRequested);

            k.RestartTimeout();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await k);

            stopwatch.Stop();

            // Without the restart the continuation expires at s_continuationTimeout.
            // With it, the deadline moves out by the time spent queued.
            TimeSpan floor = s_continuationTimeout + (s_simulatedSemaphoreWait / 2);
            Assert.True(stopwatch.Elapsed > floor,
                $"expected the continuation to survive past {floor}, but it expired after {stopwatch.Elapsed}");
        }

        [Fact]
        public async Task TestRestartTimeout_StillTimesOut()
        {
            using var k = new SimpleAsyncRpcContinuation(ProtocolCommandId.ExchangeDeclareOk,
                s_continuationTimeout, CancellationToken.None);

            k.RestartTimeout();

            // Restarting must not disarm the timeout, only reschedule it
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await k);
        }

        [Fact]
        public void TestRestartTimeout_AfterDisposeDoesNotThrow()
        {
            var k = new SimpleAsyncRpcContinuation(ProtocolCommandId.ExchangeDeclareOk,
                s_continuationTimeout, CancellationToken.None);
            k.Dispose();

            // A continuation that has already completed may still be restarted by a
            // racing caller; that must not surface an ObjectDisposedException
            k.RestartTimeout();
        }
    }
}
