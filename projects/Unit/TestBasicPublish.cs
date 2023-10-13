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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Sdk;

namespace RabbitMQ.Client.Unit
{
    [Collection("IntegrationFixture")]
    public class TestBasicPublish
    {
        [Fact]
        public void TestBasicRoundtripArray()
        {
            var cf = new ConnectionFactory();
            using (IConnection c = cf.CreateConnection())
            using (IChannel m = c.CreateChannel())
            {
                QueueDeclareOk q = m.QueueDeclare();
                var bp = new BasicProperties();
                byte[] sendBody = System.Text.Encoding.UTF8.GetBytes("hi");
                byte[] consumeBody = null;
                var consumer = new EventingBasicConsumer(m);
                var are = new AutoResetEvent(false);
                consumer.Received += async (o, a) =>
                {
                    consumeBody = a.Body.ToArray();
                    are.Set();
                    await Task.Yield();
                };
                string tag = m.BasicConsume(q.QueueName, true, consumer);

                m.BasicPublish("", q.QueueName, bp, sendBody);
                bool waitResFalse = are.WaitOne(5000);
                m.BasicCancel(tag);

                Assert.True(waitResFalse);
                Assert.Equal(sendBody, consumeBody);
            }
        }

        [Fact]
        public void TestBasicRoundtripCachedString()
        {
            var cf = new ConnectionFactory();
            using (IConnection c = cf.CreateConnection())
            using (IChannel m = c.CreateChannel())
            {
                CachedString exchangeName = new CachedString(string.Empty);
                CachedString queueName = new CachedString(m.QueueDeclare().QueueName);
                byte[] sendBody = System.Text.Encoding.UTF8.GetBytes("hi");
                byte[] consumeBody = null;
                var consumer = new EventingBasicConsumer(m);
                var are = new AutoResetEvent(false);
                consumer.Received += async (o, a) =>
                {
                    consumeBody = a.Body.ToArray();
                    are.Set();
                    await Task.Yield();
                };
                string tag = m.BasicConsume(queueName.Value, true, consumer);

                m.BasicPublish(exchangeName, queueName, sendBody);
                bool waitResFalse = are.WaitOne(2000);
                m.BasicCancel(tag);

                Assert.True(waitResFalse);
                Assert.Equal(sendBody, consumeBody);
            }
        }

        [Fact]
        public void TestBasicRoundtripReadOnlyMemory()
        {
            var cf = new ConnectionFactory();
            using (IConnection c = cf.CreateConnection())
            using (IChannel m = c.CreateChannel())
            {
                QueueDeclareOk q = m.QueueDeclare();
                byte[] sendBody = System.Text.Encoding.UTF8.GetBytes("hi");
                byte[] consumeBody = null;
                var consumer = new EventingBasicConsumer(m);
                var are = new AutoResetEvent(false);
                consumer.Received += async (o, a) =>
                {
                    consumeBody = a.Body.ToArray();
                    are.Set();
                    await Task.Yield();
                };
                string tag = m.BasicConsume(q.QueueName, true, consumer);

                m.BasicPublish("", q.QueueName, new ReadOnlyMemory<byte>(sendBody));
                bool waitResFalse = are.WaitOne(2000);
                m.BasicCancel(tag);

                Assert.True(waitResFalse);
                Assert.Equal(sendBody, consumeBody);
            }
        }

        [Fact]
        public void CanNotModifyPayloadAfterPublish()
        {
            var cf = new ConnectionFactory();
            using (IConnection c = cf.CreateConnection())
            using (IChannel m = c.CreateChannel())
            {
                QueueDeclareOk q = m.QueueDeclare();
                byte[] sendBody = new byte[1000];
                var consumer = new EventingBasicConsumer(m);
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
                string tag = m.BasicConsume(q.QueueName, true, consumer);

                m.BasicPublish("", q.QueueName, sendBody);
                sendBody.AsSpan().Fill(1);

                Assert.True(receivedMessage.WaitOne(5000));
                Assert.False(modified, "Payload was modified after the return of BasicPublish");

                m.BasicCancel(tag);
            }
        }

        [Fact]
        public void TestMaxMessageSize()
        {
            var re = new ManualResetEventSlim();
            const ushort maxMsgSize = 1024;

            int count = 0;
            byte[] msg0 = Encoding.UTF8.GetBytes("hi");

            var r = new System.Random();
            byte[] msg1 = new byte[maxMsgSize * 2];
            r.NextBytes(msg1);

            var cf = new ConnectionFactory();
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

                using (IChannel m = c.CreateChannel())
                {
                    m.ChannelShutdown += (o, a) =>
                    {
                        sawChannelShutdown = true;
                    };

                    m.CallbackException += (o, a) =>
                    {
                        throw new XunitException("Unexpected m.CallbackException");
                    };

                    QueueDeclareOk q = m.QueueDeclare();

                    var consumer = new EventingBasicConsumer(m);

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

                    string tag = m.BasicConsume(q.QueueName, true, consumer);

                    m.BasicPublish("", q.QueueName, msg0);
                    m.BasicPublish("", q.QueueName, msg1);
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
        public void TestPropertiesRountrip_Headers()
        {
            // Arrange
            var subject = new BasicProperties
            {
                Headers = new Dictionary<string, object>()
            };

            var cf = new ConnectionFactory();
            using (IConnection c = cf.CreateConnection())
            using (IChannel m = c.CreateChannel())
            {
                QueueDeclareOk q = m.QueueDeclare();
                var bp = new BasicProperties() { Headers = new Dictionary<string, object>() };
                bp.Headers["Hello"] = "World";
                byte[] sendBody = Encoding.UTF8.GetBytes("hi");
                byte[] consumeBody = null;
                var consumer = new EventingBasicConsumer(m);
                var are = new AutoResetEvent(false);
                string response = null;
                consumer.Received += async (o, a) =>
                {
                    response = Encoding.UTF8.GetString(a.BasicProperties.Headers["Hello"] as byte[]);
                    consumeBody = a.Body.ToArray();
                    are.Set();
                    await Task.Yield();
                };

                string tag = m.BasicConsume(q.QueueName, true, consumer);
                m.BasicPublish("", q.QueueName, bp, sendBody);
                bool waitResFalse = are.WaitOne(5000);
                m.BasicCancel(tag);
                Assert.True(waitResFalse);
                Assert.Equal(sendBody, consumeBody);
                Assert.Equal("World", response);
            }
        }
    }
}
