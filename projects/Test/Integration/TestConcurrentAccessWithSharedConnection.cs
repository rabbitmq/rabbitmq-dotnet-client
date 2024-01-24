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
using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestConcurrentAccessWithSharedConnection : IntegrationFixture
    {
        private const ushort MessageCount = 256;

        public TestConcurrentAccessWithSharedConnection(ITestOutputHelper output)
            : base(output)
        {
        }

        public override async Task InitializeAsync()
        {
            _connFactory = CreateConnectionFactory();
            _conn = await _connFactory.CreateConnectionAsync();
            // NB: not creating _channel because this test suite doesn't use it.
            Assert.Null(_channel);
        }

        [Fact]
        public async Task TestConcurrentChannelOpenAndPublishingWithBlankMessages()
        {
            await TestConcurrentChannelOpenAndPublishingWithBodyAsync(Array.Empty<byte>(), 30);
        }

        [Fact]
        public async Task TestConcurrentChannelOpenAndPublishingSize64()
        {
            await TestConcurrentChannelOpenAndPublishingWithBodyOfSizeAsync(64);
        }

        [Fact]
        public async Task TestConcurrentChannelOpenAndPublishingSize256()
        {
            await TestConcurrentChannelOpenAndPublishingWithBodyOfSizeAsync(256);
        }

        [Fact]
        public Task TestConcurrentChannelOpenAndPublishingSize1024()
        {
            return TestConcurrentChannelOpenAndPublishingWithBodyOfSizeAsync(1024);
        }

        [Fact]
        public async Task TestConcurrentChannelOpenCloseLoop()
        {
            await TestConcurrentChannelOperationsAsync(async (conn) =>
            {
                using (IChannel ch = await conn.CreateChannelAsync())
                {
                    await ch.CloseAsync();
                }
            }, 50);
        }

        private Task TestConcurrentChannelOpenAndPublishingWithBodyOfSizeAsync(ushort length, int iterations = 30)
        {
            byte[] body = GetRandomBody(length);
            return TestConcurrentChannelOpenAndPublishingWithBodyAsync(body, iterations);
        }

        private Task TestConcurrentChannelOpenAndPublishingWithBodyAsync(byte[] body, int iterations)
        {
            return TestConcurrentChannelOperationsAsync(async (conn) =>
            {
                var tcs = new TaskCompletionSource<bool>();

                // publishing on a shared channel is not supported
                // and would missing the point of this test anyway
                using (IChannel ch = await _conn.CreateChannelAsync())
                {
                    await ch.ConfirmSelectAsync();

                    ch.BasicAcks += (object sender, BasicAckEventArgs e) =>
                    {
                        if (e.DeliveryTag >= MessageCount)
                        {
                            tcs.SetResult(true);
                        }
                    };

                    ch.BasicNacks += (object sender, BasicNackEventArgs e) =>
                    {
                        tcs.SetResult(false);
                        Assert.Fail("should never see a nack");
                    };

                    QueueDeclareOk q = await ch.QueueDeclareAsync(queue: string.Empty, exclusive: true, autoDelete: true);
                    for (ushort j = 0; j < MessageCount; j++)
                    {
                        await ch.BasicPublishAsync("", q.QueueName, body, true);
                    }

                    Assert.True(await tcs.Task);

                    // NOTE: this is very important before a Dispose();
                    await ch.CloseAsync();
                }
            }, iterations);
        }

        private async Task TestConcurrentChannelOperationsAsync(Func<IConnection, Task> action, int iterations)
        {
            var tasks = new List<Task>();
            for (int i = 0; i < _processorCount; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    for (int j = 0; j < iterations; j++)
                    {
                        await action(_conn);
                    }
                }));
            }

            Task whenTask = Task.WhenAll(tasks);
            await whenTask.WaitAsync(LongWaitSpan);
            Assert.True(whenTask.IsCompleted);
            Assert.False(whenTask.IsCanceled);
            Assert.False(whenTask.IsFaulted);

            // incorrect frame interleaving in these tests will result
            // in an unrecoverable connection-level exception, thus
            // closing the connection
            Assert.True(_conn.IsOpen);
        }
    }
}
