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
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration.ConnectionRecovery.EventHandlerRecovery.Channel
{
    public class TestRecoveryEventHandlers : TestConnectionRecoveryBase
    {
        public TestRecoveryEventHandlers(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestRecoveryEventHandlers_Called()
        {
            int counter = 0;
            ((AutorecoveringChannel)_channel).RecoveryAsync += (source, ea) =>
            {
                Interlocked.Increment(ref counter);
                return Task.CompletedTask;
            };

            await CloseAndWaitForRecoveryAsync();
            await CloseAndWaitForRecoveryAsync();
            await CloseAndWaitForRecoveryAsync();
            await CloseAndWaitForRecoveryAsync();
            Assert.True(_channel.IsOpen);
            Assert.True(counter >= 3);
        }

        [Fact]
        public async Task TestRecoveryEventHandlerCanRegisterAConsumer_GH2038()
        {
            /*
             * Deliberately not the fixture's connection, and deliberately disposed only on
             * the success path. A regression here wedges _recordedEntitiesSemaphore forever,
             * and closing or disposing a channel then blocks on that same semaphore - the
             * fixture's teardown disposes its channel, so using the fixture's connection
             * would hang the run instead of failing it. See
             * docs/internal/recovery-event-handler-invocation.md.
             */
            AutorecoveringConnection conn = await CreateAutorecoveringConnectionAsync();
            IChannel channel = await conn.CreateChannelAsync();
            string queueName = (await channel.QueueDeclareAsync(GenerateQueueName(), false, true, false)).QueueName;

            TaskCompletionSource<bool> recoverySucceeded = PrepareForRecovery(conn);
            var handlerReturned = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ((AutorecoveringChannel)channel).RecoveryAsync += async (source, ea) =>
            {
                try
                {
                    var recovered = (IChannel)source;
                    var consumer = new AsyncEventingBasicConsumer(recovered);
                    consumer.ReceivedAsync += (_, __) => Task.CompletedTask;
                    await recovered.BasicConsumeAsync(queueName, true, consumer);
                    handlerReturned.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    handlerReturned.TrySetException(ex);
                }
            };

            await CloseConnectionAsync(conn);
            await WaitAsync(handlerReturned, "recovery handler returned");
            Assert.True(channel.IsOpen);

            /*
             * The wait above already catches a wedged semaphore, since a wedged handler never
             * returns. These two catch what it cannot: recovery failing after the handler has
             * returned, which leaves the connection un-recovered while the handler looks fine.
             */
            await WaitAsync(recoverySucceeded, "recovery succeeded");
            await using (IChannel other = await conn.CreateChannelAsync())
            {
                await other.ExchangeDeclareAsync(GenerateExchangeName(), ExchangeType.Direct, false, true);
            }

            await channel.CloseAsync();
            await conn.CloseAsync();
            await channel.DisposeAsync();
            await conn.DisposeAsync();
        }

        [Fact]
        public async Task TestRecoveryEventHandlersRunAfterEveryChannelRecovers_GH2038()
        {
            await using AutorecoveringConnection conn = await CreateAutorecoveringConnectionAsync();
            await using IChannel first = await conn.CreateChannelAsync();
            await using IChannel second = await conn.CreateChannelAsync();

            bool allOpenInFirst = false;
            bool allOpenInSecond = false;
            var firstRan = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondRan = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            /*
             * Asserted from inside both handlers rather than just the first, so the result does
             * not depend on the order _channels happens to be recovered in: whichever channel is
             * recovered first, its handler is the one that used to see the other still closed.
             */
            ((AutorecoveringChannel)first).RecoveryAsync += (source, ea) =>
            {
                allOpenInFirst = first.IsOpen && second.IsOpen;
                firstRan.TrySetResult(true);
                return Task.CompletedTask;
            };

            ((AutorecoveringChannel)second).RecoveryAsync += (source, ea) =>
            {
                allOpenInSecond = first.IsOpen && second.IsOpen;
                secondRan.TrySetResult(true);
                return Task.CompletedTask;
            };

            await CloseConnectionAsync(conn);
            await WaitAsync(firstRan, "first channel recovery handler ran");
            await WaitAsync(secondRan, "second channel recovery handler ran");

            Assert.True(allOpenInFirst, "first channel's handler ran before every channel had recovered");
            Assert.True(allOpenInSecond, "second channel's handler ran before every channel had recovered");
        }
    }
}
