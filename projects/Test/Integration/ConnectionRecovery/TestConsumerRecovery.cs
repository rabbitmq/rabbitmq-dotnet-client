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

using System.Threading;
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
            /*
             * rabbitmq/rabbitmq-dotnet-client#2006
             *
             * AsyncDefaultBasicConsumer.ShutdownReason documents itself as null unless the channel
             * has shut down. Recovery re-registers the consumer, so it starts receiving deliveries
             * again, but the reason recorded when the connection dropped was never cleared. The
             * consumer therefore went on reporting a shutdown that was over, indefinitely, which is
             * misleading for anything using it to decide whether the consumer is healthy.
             */
            string q = (await _channel.QueueDeclareAsync(GenerateQueueName(), false, true, false)).QueueName;
            var consumer = new AsyncEventingBasicConsumer(_channel);

            /*
             * HandleBasicConsumeOkAsync runs on the consumer dispatcher, so it has not necessarily
             * happened by the time BasicConsumeAsync returns. Both the initial state and the state
             * after recovery therefore have to be observed from the registration event rather than
             * read straight after the call, otherwise this test races the dispatcher.
             */
            var firstRegistrationTcs =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var registrationAfterRecoveryTcs =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int registrations = 0;
            consumer.RegisteredAsync += (sender, ea) =>
            {
                // The first registration is the original BasicConsumeAsync below; the second is
                // recovery re-registering the consumer.
                if (Interlocked.Increment(ref registrations) == 1)
                {
                    firstRegistrationTcs.TrySetResult(true);
                }
                else
                {
                    registrationAfterRecoveryTcs.TrySetResult(true);
                }
                return Task.CompletedTask;
            };

            await _channel.BasicConsumeAsync(q, true, consumer);
            await WaitAsync(firstRegistrationTcs, "consumer registered");

            Assert.True(consumer.IsRunning);
            Assert.Null(consumer.ShutdownReason);

            await CloseAndWaitForRecoveryAsync();
            await WaitAsync(registrationAfterRecoveryTcs, "consumer re-registered after recovery");

            Assert.True(_channel.IsOpen);
            Assert.True(consumer.IsRunning);
            Assert.Null(consumer.ShutdownReason);
        }
    }
}
