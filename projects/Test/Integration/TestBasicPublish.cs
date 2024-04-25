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
//  Copyright (c) 2011-2020 VMware, Inc. or its affiliates.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Test.Integration
{
    public class TestBasicPublish : IntegrationFixture
    {
        public TestBasicPublish(ITestOutputHelper output) : base(output)
        {
        }

        public override Task InitializeAsync()
        {
            _connFactory = CreateConnectionFactory();
            Assert.Null(_conn);
            Assert.Null(_channel);
            return Task.CompletedTask;
        }

        [Fact]
        public async Task TestBasicRoundtripArray()
        {
            _conn = await _connFactory.CreateConnectionAsync();
            _channel = await _conn.CreateChannelAsync();

            QueueDeclareOk q = await _channel.QueueDeclareAsync();
            var bp = new BasicProperties();
            byte[] sendBody = _encoding.GetBytes("hi");
            byte[] consumeBody = null;
            var consumer = new AsyncEventingBasicConsumer(_channel);
            using (var consumerReceivedSemaphore = new SemaphoreSlim(0, 1))
            {
                consumer.Received += (o, a, ct) =>
                {
                    consumeBody = a.Body.ToArray();
                    consumerReceivedSemaphore.Release();
                    return Task.CompletedTask;
                };
                string tag = await _channel.BasicConsumeAsync(q.QueueName, true, consumer);

                await _channel.BasicPublishAsync("", q.QueueName, bp, sendBody);
                bool waitRes = await consumerReceivedSemaphore.WaitAsync(TimeSpan.FromSeconds(5));
                await _channel.BasicCancelAsync(tag);

                Assert.True(waitRes);
                Assert.Equal(sendBody, consumeBody);
            }
        }

        [Fact]
        public async Task TestBasicRoundtripCachedString()
        {
            _conn = await _connFactory.CreateConnectionAsync();
            _channel = await _conn.CreateChannelAsync();

            CachedString exchangeName = new CachedString(string.Empty);
            CachedString queueName = new CachedString((await _channel.QueueDeclareAsync()).QueueName);
            byte[] sendBody = _encoding.GetBytes("hi");
            byte[] consumeBody = null;
            var consumer = new AsyncEventingBasicConsumer(_channel);
            using (var consumerReceivedSemaphore = new SemaphoreSlim(0, 1))
            {
                consumer.Received += (o, a, ct) =>
                {
                    consumeBody = a.Body.ToArray();
                    consumerReceivedSemaphore.Release();
                    return Task.CompletedTask;
                };
                string tag = await _channel.BasicConsumeAsync(queueName.Value, true, consumer);

                await _channel.BasicPublishAsync(exchangeName, queueName, sendBody);
                bool waitResFalse = await consumerReceivedSemaphore.WaitAsync(TimeSpan.FromSeconds(2));
                await _channel.BasicCancelAsync(tag);

                Assert.True(waitResFalse);
                Assert.Equal(sendBody, consumeBody);
            }
        }

        [Fact]
        public async Task TestBasicRoundtripReadOnlyMemory()
        {
            _conn = await _connFactory.CreateConnectionAsync();
            _channel = await _conn.CreateChannelAsync();

            QueueDeclareOk q = await _channel.QueueDeclareAsync();
            byte[] sendBody = _encoding.GetBytes("hi");
            byte[] consumeBody = null;
            var consumer = new AsyncEventingBasicConsumer(_channel);
            using (var consumerReceivedSemaphore = new SemaphoreSlim(0, 1))
            {
                consumer.Received += (o, a, ct) =>
                {
                    consumeBody = a.Body.ToArray();
                    consumerReceivedSemaphore.Release();
                    return Task.CompletedTask;
                };
                string tag = await _channel.BasicConsumeAsync(q.QueueName, true, consumer);

                await _channel.BasicPublishAsync("", q.QueueName, new ReadOnlyMemory<byte>(sendBody));
                bool waitRes = await consumerReceivedSemaphore.WaitAsync(TimeSpan.FromSeconds(2));
                await _channel.BasicCancelAsync(tag);

                Assert.True(waitRes);
                Assert.Equal(sendBody, consumeBody);
            }
        }

        [Fact]
        public async Task CanNotModifyPayloadAfterPublish()
        {
            _conn = await _connFactory.CreateConnectionAsync();
            _channel = await _conn.CreateChannelAsync();

            QueueDeclareOk q = await _channel.QueueDeclareAsync();
            byte[] sendBody = new byte[1000];
            var consumer = new AsyncEventingBasicConsumer(_channel);
            using (var consumerReceivedSemaphore = new SemaphoreSlim(0, 1))
            {
                bool modified = true;
                consumer.Received += (o, a, ct) =>
                {
                    if (a.Body.Span.IndexOf((byte)1) < 0)
                    {
                        modified = false;
                    }
                    consumerReceivedSemaphore.Release();
                    return Task.CompletedTask;
                };
                string tag = await _channel.BasicConsumeAsync(q.QueueName, true, consumer);

                await _channel.BasicPublishAsync("", q.QueueName, sendBody);
                sendBody.AsSpan().Fill(1);

                Assert.True(await consumerReceivedSemaphore.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.False(modified, "Payload was modified after the return of BasicPublish");

                await _channel.BasicCancelAsync(tag);
            }
        }

        [Fact]
        public async Task TestMaxMessageSize()
        {
            var re = new ManualResetEventSlim();
            const ushort maxMsgSize = 1024;

            int count = 0;
            byte[] msg0 = _encoding.GetBytes("hi");
            byte[] msg1 = GetRandomBody(maxMsgSize * 2);

            var cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = false;
            cf.TopologyRecoveryEnabled = false;
            cf.MaxMessageSize = maxMsgSize;

            bool sawConnectionShutdown = false;
            bool sawChannelShutdown = false;
            bool sawConsumerRegistered = false;
            bool sawConsumerCancelled = false;

            using (IConnection c = await cf.CreateConnectionAsync())
            {
                c.ConnectionShutdown += (o, a) =>
                {
                    sawConnectionShutdown = true;
                };

                Assert.Equal(maxMsgSize, cf.MaxMessageSize);
                Assert.Equal(maxMsgSize, cf.Endpoint.MaxMessageSize);
                Assert.Equal(maxMsgSize, c.Endpoint.MaxMessageSize);

                using (IChannel channel = await c.CreateChannelAsync())
                {
                    channel.ChannelShutdown += (o, a) =>
                    {
                        sawChannelShutdown = true;
                    };

                    channel.CallbackException += (o, a) =>
                    {
                        throw new XunitException("Unexpected channel.CallbackException");
                    };

                    QueueDeclareOk q = await channel.QueueDeclareAsync();

                    var consumer = new AsyncEventingBasicConsumer(channel);

                    consumer.Shutdown += (o, a, ct) =>
                    {
                        re.Set();
                        return Task.CompletedTask;
                    };

                    consumer.Registered += (o, a, ct) =>
                    {
                        sawConsumerRegistered = true;
                        return Task.CompletedTask;
                    };

                    consumer.Unregistered += (o, a, ct) =>
                    {
                        throw new XunitException("Unexpected consumer.Unregistered");
                    };

                    consumer.ConsumerCancelled += (o, a, ct) =>
                    {
                        sawConsumerCancelled = true;
                        return Task.CompletedTask;
                    };

                    consumer.Received += (o, a, ct) =>
                    {
                        Interlocked.Increment(ref count);
                        return Task.CompletedTask;
                    };

                    string tag = await channel.BasicConsumeAsync(q.QueueName, true, consumer);

                    await channel.BasicPublishAsync("", q.QueueName, msg0);
                    await channel.BasicPublishAsync("", q.QueueName, msg1);
                    Assert.True(re.Wait(TimeSpan.FromSeconds(5)));

                    Assert.Equal(1, count);
                    Assert.True(sawConnectionShutdown);
                    Assert.True(sawChannelShutdown);
                    Assert.True(sawConsumerRegistered);
                    Assert.True(sawConsumerCancelled);

                    await channel.CloseAsync();
                }
            }
        }

        [Fact]
        public async Task TestPropertiesRoundtrip_Headers()
        {
            _conn = await _connFactory.CreateConnectionAsync();
            _channel = await _conn.CreateChannelAsync();

            var subject = new BasicProperties
            {
                Headers = new Dictionary<string, object>()
            };

            QueueDeclareOk q = await _channel.QueueDeclareAsync();
            var bp = new BasicProperties() { Headers = new Dictionary<string, object>() };
            bp.Headers["Hello"] = "World";
            byte[] sendBody = _encoding.GetBytes("hi");
            byte[] consumeBody = null;
            var consumer = new AsyncEventingBasicConsumer(_channel);
            using (var consumerReceivedSemaphore = new SemaphoreSlim(0, 1))
            {
                string response = null;
                consumer.Received += (o, a, ct) =>
                {
                    response = _encoding.GetString(a.BasicProperties.Headers["Hello"] as byte[]);
                    consumeBody = a.Body.ToArray();
                    consumerReceivedSemaphore.Release();
                    return Task.CompletedTask;
                };

                string tag = await _channel.BasicConsumeAsync(q.QueueName, true, consumer);
                await _channel.BasicPublishAsync("", q.QueueName, bp, sendBody);
                bool waitResFalse = await consumerReceivedSemaphore.WaitAsync(TimeSpan.FromSeconds(5));
                await _channel.BasicCancelAsync(tag);
                Assert.True(waitResFalse);
                Assert.Equal(sendBody, consumeBody);
                Assert.Equal("World", response);
            }
        }
    }
}
