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
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestAsyncConsumer : IntegrationFixture
    {
        public TestAsyncConsumer(ITestOutputHelper output) : base(output)
        {
        }

        protected override void SetUp()
        {
            _connFactory = CreateConnectionFactory();
            _connFactory.DispatchConsumersAsync = true;
            _connFactory.ConsumerDispatchConcurrency = 2;

            _conn = _connFactory.CreateConnection();
            _channel = _conn.CreateChannel();
        }

        [Fact]
        public void TestBasicRoundtrip()
        {
            QueueDeclareOk q = _channel.QueueDeclare();
            byte[] body = _encoding.GetBytes("async-hi");
            _channel.BasicPublish("", q.QueueName, body);
            var consumer = new AsyncEventingBasicConsumer(_channel);
            var are = new AutoResetEvent(false);
            consumer.Received += async (o, a) =>
                {
                    are.Set();
                    await Task.Yield();
                };
            string tag = _channel.BasicConsume(q.QueueName, true, consumer);
            // ensure we get a delivery
            bool waitRes = are.WaitOne(2000);
            Assert.True(waitRes);
            // unsubscribe and ensure no further deliveries
            _channel.BasicCancel(tag);
            _channel.BasicPublish("", q.QueueName, body);
            bool waitResFalse = are.WaitOne(2000);
            Assert.False(waitResFalse);
        }

        [Fact]
        public async Task TestBasicRoundtripConcurrent()
        {
            QueueDeclareOk q = _channel.QueueDeclare();
            string publish1 = get_unique_string(1024);
            byte[] body = _encoding.GetBytes(publish1);
            _channel.BasicPublish("", q.QueueName, body);

            string publish2 = get_unique_string(1024);
            body = _encoding.GetBytes(publish2);
            _channel.BasicPublish("", q.QueueName, body);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            var publish1SyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var publish2SyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var maximumWaitTime = TimeSpan.FromSeconds(10);
            var tokenSource = new CancellationTokenSource(maximumWaitTime);
            tokenSource.Token.Register(() =>
            {
                publish1SyncSource.TrySetResult(false);
                publish2SyncSource.TrySetResult(false);
            });

            consumer.Received += async (o, a) =>
            {
                string decoded = Encoding.ASCII.GetString(a.Body.ToArray());
                if (decoded == publish1)
                {
                    publish1SyncSource.TrySetResult(true);
                    await publish2SyncSource.Task;
                }
                else if (decoded == publish2)
                {
                    publish2SyncSource.TrySetResult(true);
                    await publish1SyncSource.Task;
                }
            };

            _channel.BasicConsume(q.QueueName, true, consumer);

            // ensure we get a delivery
            await Task.WhenAll(publish1SyncSource.Task, publish2SyncSource.Task);

            bool result1 = await publish1SyncSource.Task;
            Assert.True(result1, $"1 - Non concurrent dispatch lead to deadlock after {maximumWaitTime}");

            bool result2 = await publish2SyncSource.Task;
            Assert.True(result2, $"2 - Non concurrent dispatch lead to deadlock after {maximumWaitTime}");
        }

        [Fact]
        public async Task TestBasicRoundtripConcurrentManyMessages()
        {
            const int publish_total = 4096;
            string queueName = $"{nameof(TestBasicRoundtripConcurrentManyMessages)}-{Guid.NewGuid()}";

            string publish1 = get_unique_string(32768);
            byte[] body1 = Encoding.ASCII.GetBytes(publish1);
            string publish2 = get_unique_string(32768);
            byte[] body2 = Encoding.ASCII.GetBytes(publish2);

            QueueDeclareOk q = _channel.QueueDeclare(queue: queueName, exclusive: false, durable: true);
            Assert.Equal(q.QueueName, queueName);

            Task publishTask = Task.Run(async () =>
                    {
                        using (IChannel m = _conn.CreateChannel())
                        {
                            QueueDeclareOk q = _channel.QueueDeclare(queue: queueName, exclusive: false, durable: true);
                            for (int i = 0; i < publish_total; i++)
                            {
                                await _channel.BasicPublishAsync(string.Empty, queueName, body1);
                                await _channel.BasicPublishAsync(string.Empty, queueName, body2);
                            }
                        }
                    });

            Task consumeTask = Task.Run(async () =>
                    {
                        var publish1SyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var publish2SyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var maximumWaitTime = TimeSpan.FromSeconds(30);
                        var tokenSource = new CancellationTokenSource(maximumWaitTime);
                        tokenSource.Token.Register(() =>
                        {
                            publish1SyncSource.TrySetResult(false);
                            publish2SyncSource.TrySetResult(false);
                        });

                        using (IChannel m = _conn.CreateChannel())
                        {
                            var consumer = new AsyncEventingBasicConsumer(m);

                            int publish1_count = 0;
                            int publish2_count = 0;

                            consumer.Received += async (o, a) =>
                            {
                                string decoded = Encoding.ASCII.GetString(a.Body.ToArray());
                                if (decoded == publish1)
                                {
                                    if (Interlocked.Increment(ref publish1_count) >= publish_total)
                                    {
                                        publish1SyncSource.TrySetResult(true);
                                        await publish2SyncSource.Task;
                                    }
                                }
                                else if (decoded == publish2)
                                {
                                    if (Interlocked.Increment(ref publish2_count) >= publish_total)
                                    {
                                        publish2SyncSource.TrySetResult(true);
                                        await publish1SyncSource.Task;
                                    }
                                }
                            };

                            _channel.BasicConsume(queueName, true, consumer);

                            // ensure we get a delivery
                            await Task.WhenAll(publish1SyncSource.Task, publish2SyncSource.Task);

                            bool result1 = await publish1SyncSource.Task;
                            Assert.True(result1, $"Non concurrent dispatch lead to deadlock after {maximumWaitTime}");

                            bool result2 = await publish2SyncSource.Task;
                            Assert.True(result2, $"Non concurrent dispatch lead to deadlock after {maximumWaitTime}");
                        }
                    });

            await Task.WhenAll(publishTask, consumeTask);
        }

        [Fact]
        public void TestBasicRoundtripNoWait()
        {
            QueueDeclareOk q = _channel.QueueDeclare();
            byte[] body = _encoding.GetBytes("async-hi");
            _channel.BasicPublish("", q.QueueName, body);
            var consumer = new AsyncEventingBasicConsumer(_channel);
            var are = new AutoResetEvent(false);
            consumer.Received += async (o, a) =>
                {
                    are.Set();
                    await Task.Yield();
                };
            string tag = _channel.BasicConsume(q.QueueName, true, consumer);
            // ensure we get a delivery
            bool waitRes = are.WaitOne(2000);
            Assert.True(waitRes);
            // unsubscribe and ensure no further deliveries
            _channel.BasicCancelNoWait(tag);
            _channel.BasicPublish("", q.QueueName, body);
            bool waitResFalse = are.WaitOne(2000);
            Assert.False(waitResFalse);
        }

        [Fact]
        public async void ConcurrentEventingTestForReceived()
        {
            const int NumberOfThreads = 4;
            const int NumberOfRegistrations = 5000;

            var called = new byte[NumberOfThreads * NumberOfRegistrations];

            QueueDeclareOk q = _channel.QueueDeclare();
            var consumer = new AsyncEventingBasicConsumer(_channel);
            _channel.BasicConsume(q.QueueName, true, consumer);
            var countdownEvent = new CountdownEvent(NumberOfThreads);
            var tasks = new Task[NumberOfThreads];
            for (int i = 0; i < NumberOfThreads; i++)
            {
                int threadIndex = i;
                tasks[i] = Task.Run(() =>
                {
                    countdownEvent.Signal();
                    countdownEvent.Wait();
                    int start = threadIndex * NumberOfRegistrations;
                    for (int j = start; j < start + NumberOfRegistrations; j++)
                    {
                        int receivedIndex = j;
                        consumer.Received += (sender, eventArgs) =>
                        {
                            called[receivedIndex] = 1;
                            return Task.CompletedTask;
                        };
                    }
                });
            }

            countdownEvent.Wait();
            await Task.WhenAll(tasks);

            // Add last receiver
            var are = new AutoResetEvent(false);
            consumer.Received += (o, a) =>
            {
                are.Set();
                return Task.CompletedTask;
            };

            // Send message
            _channel.BasicPublish("", q.QueueName, ReadOnlyMemory<byte>.Empty);
            are.WaitOne(TimingFixture.TestTimeout);

            // Check received messages
            Assert.Equal(-1, called.AsSpan().IndexOf((byte)0));
        }

        [Fact]
        public void NonAsyncConsumerShouldThrowInvalidOperationException()
        {
            QueueDeclareOk q = _channel.QueueDeclare();
            byte[] body = _encoding.GetBytes("async-hi");
            _channel.BasicPublish("", q.QueueName, body);
            var consumer = new EventingBasicConsumer(_channel);
            Assert.Throws<InvalidOperationException>(() => _channel.BasicConsume(q.QueueName, false, consumer));
        }

        private string get_unique_string(int string_length)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var bit_count = (string_length * 6);
                var byte_count = ((bit_count + 7) / 8); // rounded up
                var bytes = new byte[byte_count];
                rng.GetBytes(bytes);
                return Convert.ToBase64String(bytes);
            }
        }
    }
}
