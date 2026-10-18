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

using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Impl;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration.ConnectionRecovery
{
    public class TestConsumerRecovery : TestConnectionRecoveryBase
    {
        public TestConsumerRecovery(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task TestConsumerRecoveryWithManyConsumers()
        {
            string q = (await _channel.QueueDeclareAsync(GenerateQueueName(), false, true, false)).QueueName;
            int n = 1024;

            for (int i = 0; i < n; i++)
            {
                var cons = new AsyncEventingBasicConsumer(_channel);
                await _channel.BasicConsumeAsync(q, true, cons);
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ((AutorecoveringConnection)_conn).ConsumerTagChangeAfterRecoveryAsync += (prev, current) =>
            {
                tcs.TrySetResult(true);
                return Task.CompletedTask;
            };

            await CloseAndWaitForRecoveryAsync();
            await WaitAsync(tcs, "consumer tag change after recovery");
            Assert.True(_channel.IsOpen);
            await AssertConsumerCountAsync(q, n);
        }

        [Fact]
        public async Task TestThatCancelledConsumerDoesNotReappearOnRecovery()
        {
            string q = (await _channel.QueueDeclareAsync(GenerateQueueName(), false, true, false)).QueueName;
            int n = 1024;

            for (int i = 0; i < n; i++)
            {
                var cons = new AsyncEventingBasicConsumer(_channel);
                string tag = await _channel.BasicConsumeAsync(q, true, cons);
                await _channel.BasicCancelAsync(tag);
            }
            await CloseAndWaitForRecoveryAsync();
            Assert.True(_channel.IsOpen);
            await AssertConsumerCountAsync(q, 0);
        }

        [Fact]
        public async Task TestConsumerShutdownReasonIsClearedAfterRecovery_GH2006()
        {
            // The delivery barrier below holds only at a concurrency of one, so pin the fixture's
            // default. #2006, docs/internal/connection-shutdown-and-cancellation.md.
            Assert.Equal((ushort)1, _consumerDispatchConcurrency);

            string q = (await _channel.QueueDeclareAsync(GenerateQueueName(), false, true, false)).QueueName;
            var consumer = new AsyncEventingBasicConsumer(_channel);

            // The consume-ok runs on the dispatcher, so it need not have happened when
            // BasicConsumeAsync returns; observe the initial state from the event, not the call.
            var firstRegistrationTcs =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            consumer.RegisteredAsync += (sender, ea) =>
            {
                firstRegistrationTcs.TrySetResult(true);
                return Task.CompletedTask;
            };

            // Asserts the transition, not "null before, null after": without this a change that
            // stopped delivering the shutdown would leave both assertions passing.
            ShutdownEventArgs shutdownReasonSeen = null;
            consumer.ShutdownAsync += (sender, ea) =>
            {
                shutdownReasonSeen = consumer.ShutdownReason;
                return Task.CompletedTask;
            };

            var deliveredAfterRecoveryTcs =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            consumer.ReceivedAsync += (sender, ea) =>
            {
                deliveredAfterRecoveryTcs.TrySetResult(true);
                return Task.CompletedTask;
            };

            await _channel.BasicConsumeAsync(q, true, consumer);
            await WaitAsync(firstRegistrationTcs, "consumer registered");

            Assert.True(consumer.IsRunning);
            Assert.Null(consumer.ShutdownReason);

            await CloseAndWaitForRecoveryAsync();

            Assert.True(shutdownReasonSeen is not null,
                "the consumer never observed a shutdown reason, so the connection drop did not " +
                "reach this consumer and the clear-on-recovery assertions below prove nothing");

            // A delivery, not the registration event: the registration count can be satisfied by a
            // recovery that re-registered and then failed, leaving the state read mid-attempt.
            await _channel.BasicPublishAsync(string.Empty, q, _encoding.GetBytes("after recovery"));
            await WaitAsync(deliveredAfterRecoveryTcs, "delivery after recovery");

            Assert.True(_channel.IsOpen);
            Assert.True(consumer.IsRunning);
            Assert.Null(consumer.ShutdownReason);
        }
    }
}
