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

        protected override void SetUp()
        {
            _connFactory = CreateConnectionFactory();
            Assert.Null(_conn);
            Assert.Null(_channel);
        }

        [Fact]
        public void TestBasicRoundtripArray()
        {
            _conn = _connFactory.CreateConnection();
            _channel = _conn.CreateChannel();

            QueueDeclareOk q = _channel.QueueDeclare();
            var bp = new BasicProperties();
            byte[] sendBody = _encoding.GetBytes("hi");
            byte[] consumeBody = null;
            var consumer = new EventingBasicConsumer(_channel);
            var are = new AutoResetEvent(false);
            consumer.Received += (o, a) =>
            {
                consumeBody = a.Body.ToArray();
                are.Set();
            };
            string tag = _channel.BasicConsume(q.QueueName, true, consumer);

            _channel.BasicPublish("", q.QueueName, bp, sendBody);
            bool waitResFalse = are.WaitOne(5000);
            _channel.BasicCancel(tag);

            Assert.True(waitResFalse);
            Assert.Equal(sendBody, consumeBody);
        }

        [Fact]
        public void TestBasicRoundtripCachedString()
        {
            _conn = _connFactory.CreateConnection();
            _channel = _conn.CreateChannel();

            CachedString exchangeName = new CachedString(string.Empty);
            CachedString queueName = new CachedString(_channel.QueueDeclare().QueueName);
            byte[] sendBody = _encoding.GetBytes("hi");
            byte[] consumeBody = null;
            var consumer = new EventingBasicConsumer(_channel);
            var are = new AutoResetEvent(false);
            consumer.Received += (o, a) =>
            {
                consumeBody = a.Body.ToArray();
                are.Set();
            };
            string tag = _channel.BasicConsume(queueName.Value, true, consumer);

            _channel.BasicPublish(exchangeName, queueName, sendBody);
            bool waitResFalse = are.WaitOne(2000);
            _channel.BasicCancel(tag);

            Assert.True(waitResFalse);
            Assert.Equal(sendBody, consumeBody);
        }

        [Fact]
        public void TestBasicRoundtripReadOnlyMemory()
        {
            _conn = _connFactory.CreateConnection();
            _channel = _conn.CreateChannel();

            QueueDeclareOk q = _channel.QueueDeclare();
            byte[] sendBody = _encoding.GetBytes("hi");
            byte[] consumeBody = null;
            var consumer = new EventingBasicConsumer(_channel);
            var are = new AutoResetEvent(false);
            consumer.Received += (o, a) =>
            {
                consumeBody = a.Body.ToArray();
                are.Set();
            };
            string tag = _channel.BasicConsume(q.QueueName, true, consumer);

            _channel.BasicPublish("", q.QueueName, new ReadOnlyMemory<byte>(sendBody));
            bool waitResFalse = are.WaitOne(2000);
            _channel.BasicCancel(tag);

            Assert.True(waitResFalse);
            Assert.Equal(sendBody, consumeBody);
        }

        [Fact]
        public void CanNotModifyPayloadAfterPublish()
        {
            _conn = _connFactory.CreateConnection();
            _channel = _conn.CreateChannel();

            QueueDeclareOk q = _channel.QueueDeclare();
            byte[] sendBody = new byte[1000];
            var consumer = new EventingBasicConsumer(_channel);
            var receivedMessage = new AutoResetEvent(false);
            bool modified = true;
            consumer.Received += (o, a) =>
            {
                if (a.Body.Span.IndexOf((byte)1) < 0)
                {
                    modified = false;
                }
                receivedMessage.Set();
            };
            string tag = _channel.BasicConsume(q.QueueName, true, consumer);

            _channel.BasicPublish("", q.QueueName, sendBody);
            sendBody.AsSpan().Fill(1);

            Assert.True(receivedMessage.WaitOne(5000));
            Assert.False(modified, "Payload was modified after the return of BasicPublish");

            _channel.BasicCancel(tag);
        }

        [Fact]
        public void TestMaxMessageSize()
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

            using (IConnection c = cf.CreateConnection())
            {
                c.ConnectionShutdown += (o, a) =>
                {
                    sawConnectionShutdown = true;
                };

                Assert.Equal(maxMsgSize, cf.MaxMessageSize);
                Assert.Equal(maxMsgSize, cf.Endpoint.MaxMessageSize);
                Assert.Equal(maxMsgSize, c.Endpoint.MaxMessageSize);

                using (IChannel channel = c.CreateChannel())
                {
                    channel.ChannelShutdown += (o, a) =>
                    {
                        sawChannelShutdown = true;
                    };

                    channel.CallbackException += (o, a) =>
                    {
                        throw new XunitException("Unexpected channel.CallbackException");
                    };

                    QueueDeclareOk q = channel.QueueDeclare();

                    var consumer = new EventingBasicConsumer(channel);

                    consumer.Shutdown += (o, a) =>
                    {
                        re.Set();
                    };

                    consumer.Registered += (o, a) =>
                    {
                        sawConsumerRegistered = true;
                    };

                    consumer.Unregistered += (o, a) =>
                    {
                        throw new XunitException("Unexpected consumer.Unregistered");
                    };

                    consumer.ConsumerCancelled += (o, a) =>
                    {
                        sawConsumerCancelled = true;
                    };

                    consumer.Received += (o, a) =>
                    {
                        Interlocked.Increment(ref count);
                    };

                    string tag = channel.BasicConsume(q.QueueName, true, consumer);

                    channel.BasicPublish("", q.QueueName, msg0);
                    channel.BasicPublish("", q.QueueName, msg1);
                    Assert.True(re.Wait(TimeSpan.FromSeconds(5)));

                    Assert.Equal(1, count);
                    Assert.True(sawConnectionShutdown);
                    Assert.True(sawChannelShutdown);
                    Assert.True(sawConsumerRegistered);
                    Assert.True(sawConsumerCancelled);
                }
            }
        }

        [Fact]
        public void TestPropertiesRoundtrip_Headers()
        {
            _conn = _connFactory.CreateConnection();
            _channel = _conn.CreateChannel();

            var subject = new BasicProperties
            {
                Headers = new Dictionary<string, object>()
            };

            QueueDeclareOk q = _channel.QueueDeclare();
            var bp = new BasicProperties() { Headers = new Dictionary<string, object>() };
            bp.Headers["Hello"] = "World";
            byte[] sendBody = _encoding.GetBytes("hi");
            byte[] consumeBody = null;
            var consumer = new EventingBasicConsumer(_channel);
            var are = new AutoResetEvent(false);
            string response = null;
            consumer.Received += (o, a) =>
            {
                response = _encoding.GetString(a.BasicProperties.Headers["Hello"] as byte[]);
                consumeBody = a.Body.ToArray();
                are.Set();
            };

            string tag = _channel.BasicConsume(q.QueueName, true, consumer);
            _channel.BasicPublish("", q.QueueName, bp, sendBody);
            bool waitResFalse = are.WaitOne(5000);
            _channel.BasicCancel(tag);
            Assert.True(waitResFalse);
            Assert.Equal(sendBody, consumeBody);
            Assert.Equal("World", response);
        }
    }
}
