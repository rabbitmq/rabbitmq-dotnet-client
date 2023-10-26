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
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestConcurrentAccessWithSharedConnection : IntegrationFixture
    {
        private const ushort _messageCount = 200;

        public TestConcurrentAccessWithSharedConnection(ITestOutputHelper output) : base(output)
        {
        }

        protected override void SetUp()
        {
            _connFactory = CreateConnectionFactory();
            _conn = _connFactory.CreateConnection();
            // NB: not creating _channel because this test suite doesn't use it.
            Assert.Null(_channel);
        }

        [Fact]
        public void TestConcurrentChannelOpenAndPublishingWithBlankMessages()
        {
            TestConcurrentChannelOpenAndPublishingWithBody(Array.Empty<byte>(), 30);
        }

        [Fact]
        public void TestConcurrentChannelOpenAndPublishingSize64()
        {
            TestConcurrentChannelOpenAndPublishingWithBodyOfSize(64);
        }

        [Fact]
        public void TestConcurrentChannelOpenAndPublishingSize256()
        {
            TestConcurrentChannelOpenAndPublishingWithBodyOfSize(256);
        }

        [Fact]
        public void TestConcurrentChannelOpenAndPublishingSize1024()
        {
            TestConcurrentChannelOpenAndPublishingWithBodyOfSize(1024);
        }

        [Fact]
        public Task TestConcurrentChannelOpenAndPublishingWithBlankMessagesAsync()
        {
            return TestConcurrentChannelOpenAndPublishingWithBodyAsync(Array.Empty<byte>(), 30);
        }

        [Fact]
        public Task TestConcurrentChannelOpenAndPublishingSize64Async()
        {
            return TestConcurrentChannelOpenAndPublishingWithBodyOfSizeAsync(64);
        }

        [Fact]
        public Task TestConcurrentChannelOpenAndPublishingSize256Async()
        {
            return TestConcurrentChannelOpenAndPublishingWithBodyOfSizeAsync(256);
        }

        [Fact]
        public Task TestConcurrentChannelOpenAndPublishingSize1024Async()
        {
            return TestConcurrentChannelOpenAndPublishingWithBodyOfSizeAsync(1024);
        }

        [Fact]
        public void TestConcurrentChannelOpenCloseLoop()
        {
            TestConcurrentChannelOperations((conn) =>
            {
                using (IChannel ch = conn.CreateChannel())
                {
                    ch.Close();
                }
            }, 50);
        }

        private void TestConcurrentChannelOpenAndPublishingWithBodyOfSize(ushort length, int iterations = 30)
        {
            byte[] body = GetRandomBody(length);
            TestConcurrentChannelOpenAndPublishingWithBody(body, iterations);
        }

        private Task TestConcurrentChannelOpenAndPublishingWithBodyOfSizeAsync(ushort length, int iterations = 30)
        {
            byte[] body = GetRandomBody(length);
            return TestConcurrentChannelOpenAndPublishingWithBodyAsync(body, iterations);
        }

        private void TestConcurrentChannelOpenAndPublishingWithBody(byte[] body, int iterations)
        {
            TestConcurrentChannelOperations((conn) =>
            {
                using (var localLatch = new ManualResetEvent(false))
                {
                    // publishing on a shared channel is not supported
                    // and would missing the point of this test anyway
                    using (IChannel ch = _conn.CreateChannel())
                    {
                        ch.ConfirmSelect();

                        ch.BasicAcks += (object sender, BasicAckEventArgs e) =>
                        {
                            if (e.DeliveryTag >= _messageCount)
                            {
                                localLatch.Set();
                            }
                        };

                        ch.BasicNacks += (object sender, BasicNackEventArgs e) =>
                        {
                            localLatch.Set();
                            Assert.Fail("should never see a nack");
                        };

                        QueueDeclareOk q = ch.QueueDeclare(queue: string.Empty, exclusive: true, autoDelete: true);
                        for (ushort j = 0; j < _messageCount; j++)
                        {
                            ch.BasicPublish("", q.QueueName, body, true);
                        }

                        Assert.True(localLatch.WaitOne(WaitSpan));
                    }
                }
            }, iterations);
        }

        /// <summary>
        /// Tests the concurrent channel open and publishing with body asynchronous.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <param name="iterations">The iterations.</param>
        /// <returns></returns>
        private Task TestConcurrentChannelOpenAndPublishingWithBodyAsync(byte[] body, int iterations)
        {
            return TestConcurrentChannelOperationsAsync(async (conn) =>
            {
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var tokenSource = new CancellationTokenSource(LongWaitSpan);
                tokenSource.Token.Register(() =>
                {
                    tcs.TrySetResult(false);
                });

                using (IChannel ch = _conn.CreateChannel())
                {
                    await ch.ConfirmSelectAsync();

                    ch.BasicAcks += (object sender, BasicAckEventArgs e) =>
                    {
                        if (e.DeliveryTag >= _messageCount)
                        {
                            tcs.SetResult(true);
                        }
                    };

                    ch.BasicNacks += (object sender, BasicNackEventArgs e) =>
                    {
                        tcs.SetResult(false);
                        Assert.Fail(String.Format("async test channel saw a nack, deliveryTag: {0}, multiple: {1}", e.DeliveryTag, e.Multiple));
                    };

                    QueueDeclareOk q = await ch.QueueDeclareAsync(queue: string.Empty, passive: false, durable: false, exclusive: true, autoDelete: true, arguments: null);
                    for (ushort j = 0; j < _messageCount; j++)
                    {
                        await ch.BasicPublishAsync("", q.QueueName, body, mandatory: true);
                    }

                    Assert.True(await tcs.Task);
                }
            }, iterations);
        }

        private void TestConcurrentChannelOperations(Action<IConnection> actions, int iterations)
        {
            TestConcurrentChannelOperations(actions, iterations, LongWaitSpan);
        }

        private Task TestConcurrentChannelOperationsAsync(Func<IConnection, Task> actions, int iterations)
        {
            return TestConcurrentChannelOperationsAsync(actions, iterations, LongWaitSpan);
        }

        private void TestConcurrentChannelOperations(Action<IConnection> actions, int iterations, TimeSpan timeout)
        {
            var tasks = new List<Task>();
            for (int i = 0; i < _processorCount; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < iterations; j++)
                    {
                        actions(_conn);
                    }
                }));
            }

            Assert.True(Task.WaitAll(tasks.ToArray(), timeout));

            // incorrect frame interleaving in these tests will result
            // in an unrecoverable connection-level exception, thus
            // closing the connection
            Assert.True(_conn.IsOpen);
        }

        private async Task TestConcurrentChannelOperationsAsync(Func<IConnection, Task> actions, int iterations, TimeSpan timeout)
        {
            var tasks = new List<Task>();
            for (int i = 0; i < _processorCount; i++)
            {
                for (int j = 0; j < iterations; j++)
                {
                    tasks.Add(actions(_conn));
                }
            }

            Task t = Task.WhenAll(tasks);
            await t;
            Assert.Equal(TaskStatus.RanToCompletion, t.Status);

            // incorrect frame interleaving in these tests will result
            // in an unrecoverable connection-level exception, thus
            // closing the connection
            Assert.True(_conn.IsOpen);
        }
    }
}
