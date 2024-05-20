// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2020 VMware, Inc.
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
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestPublisherConfirms : IntegrationFixture
    {
        private readonly byte[] _messageBody;

        public TestPublisherConfirms(ITestOutputHelper output)
            : base(output, openChannel: false)
        {
            _messageBody = GetRandomBody(4096);
        }

        [Fact]
        public Task TestWaitForConfirmsWithoutTimeoutAsync()
        {
            return TestWaitForConfirmsAsync(200, async (ch) =>
            {
                Assert.True(await ch.WaitForConfirmsAsync());
            });
        }

        [Fact]
        public Task TestWaitForConfirmsWithTimeout()
        {
            return TestWaitForConfirmsAsync(200, async (ch) =>
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4)))
                {
                    Assert.True(await ch.WaitForConfirmsAsync(cts.Token));
                }
            });
        }

        [Fact]
        public async Task TestWaitForConfirmsWithTimeoutAsync_MightThrowTaskCanceledException()
        {
            bool waitResult = false;
            bool sawException = false;

            Task t = TestWaitForConfirmsAsync(10000, async (ch) =>
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1)))
                {
                    try
                    {
                        waitResult = await ch.WaitForConfirmsAsync(cts.Token);
                    }
                    catch
                    {
                        sawException = true;
                    }
                }
            });

            await t;

            if (waitResult == false && sawException == false)
            {
                Assert.Fail("test failed, both waitResult and sawException are still false");
            }
        }

        [Fact]
        public Task TestWaitForConfirmsWithTimeoutAsync_MessageNacked_WaitingHasTimedout_ReturnFalse()
        {
            return TestWaitForConfirmsAsync(2000, async (ch) =>
            {
                IChannel actualChannel = ((AutorecoveringChannel)ch).InnerChannel;
                actualChannel
                    .GetType()
                    .GetMethod("HandleAckNack", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(actualChannel, new object[] { 10UL, false, true });

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4)))
                {
                    Assert.False(await ch.WaitForConfirmsAsync(cts.Token));
                }
            });
        }

        [Fact]
        public async Task TestWaitForConfirmsWithEventsAsync()
        {
            string queueName = string.Format("{0}:{1}", _testDisplayName, Guid.NewGuid());
            using (IChannel ch = await _conn.CreateChannelAsync())
            {
                await ch.ConfirmSelectAsync();
                await ch.QueueDeclareAsync(queue: queueName, passive: false, durable: false, exclusive: false, autoDelete: false, arguments: null);

                int n = 200;
                // number of event handler invocations
                int c = 0;

                ch.BasicAcks += (_, args) =>
                {
                    Interlocked.Increment(ref c);
                };

                try
                {
                    for (int i = 0; i < n; i++)
                    {
                        await ch.BasicPublishAsync(ExchangeName.Empty, queueName, _encoding.GetBytes("msg"));
                    }

                    await ch.WaitForConfirmsAsync();

                    // Note: number of event invocations is not guaranteed
                    // to be equal to N because acks can be batched,
                    // so we primarily care about event handlers being invoked
                    // in this test
                    Assert.True(c >= 1);
                }
                finally
                {
                    await ch.QueueDeleteAsync(queue: queueName, ifUnused: false, ifEmpty: false);
                    await ch.CloseAsync();
                }
            }
        }

        private async Task TestWaitForConfirmsAsync(int numberOfMessagesToPublish, Func<IChannel, Task> fn)
        {
            QueueName queueName = GenerateQueueName();
            using (IChannel ch = await _conn.CreateChannelAsync())
            {
                var props = new BasicProperties { Persistent = true };

                await ch.ConfirmSelectAsync();
                await ch.QueueDeclareAsync(queue: queueName, passive: false, durable: false, exclusive: false, autoDelete: false, arguments: null);

                for (int i = 0; i < numberOfMessagesToPublish; i++)
                {
                    await ch.BasicPublishAsync(exchange: ExchangeName.Empty, routingKey: (RoutingKey)queueName,
                        body: _messageBody, mandatory: true, basicProperties: props);
                }

                try
                {
                    await fn(ch);
                }
                finally
                {
                    await ch.QueueDeleteAsync(queue: queueName, ifUnused: false, ifEmpty: false);
                    await ch.CloseAsync();
                }
            }
        }
    }
}
