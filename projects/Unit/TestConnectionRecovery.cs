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
using NUnit.Framework;
using RabbitMQ.Client.client.impl.Channel;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;

#pragma warning disable 0618

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestConnectionRecovery : IntegrationFixture
    {
        [SetUp]
        public override async Task Init()
        {
            _conn = CreateAutorecoveringConnection();
            _channel = await _conn.CreateChannelAsync().ConfigureAwait(false);
        }

        [TearDown]
        public void CleanUp()
        {
            _conn.Close();
        }

        [Test]
        public Task TestBasicAckAfterChannelRecovery()
        {
            var latch = new ManualResetEventSlim(false);
            var cons = new AckingBasicConsumer(_channel, latch, CloseAndWaitForRecovery);

            return TestDelayedBasicAckNackAfterChannelRecovery(cons, latch);
        }

        [Test]
        public async Task TestBasicAckAfterBasicGetAndChannelRecovery()
        {
            string q = GenerateQueueName();
            await _channel.DeclareQueueAsync(q, false, false, false).ConfigureAwait(false);
            // create an offset
            await _channel.PublishMessageAsync("", q, null, Array.Empty<byte>()).ConfigureAwait(false);
            Thread.Sleep(50);
            SingleMessageRetrieval g = await _channel.RetrieveSingleMessageAsync(q, false).ConfigureAwait(false);
            Assert.IsFalse(g.IsEmpty);
            CloseAndWaitForRecovery();
            Assert.IsTrue(_conn.IsOpen);
            Assert.IsTrue(_channel.IsOpen);
            // ack the message after recovery - this should be out of range and ignored
            await _channel.AckMessageAsync(g.DeliveryTag, false);
            // do a sync operation to 'check' there is no channel exception 
            await _channel.RetrieveSingleMessageAsync(q, false).ConfigureAwait(false);
        }

        [Test]
        public async Task TestBasicAckEventHandlerRecovery()
        {
            await _channel.ActivatePublishTagsAsync().ConfigureAwait(false);
            var latch = new ManualResetEventSlim(false);
            ((AutorecoveringChannel)_channel).PublishTagAcknowledged += (_, __, ___) => latch.Set();

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(_channel.IsOpen);

            await WithTemporaryNonExclusiveQueueAsync(
                _channel,
                (ch, q) => ch.PublishMessageAsync("", q, null, _encoding.GetBytes("")).AsTask())
                .ConfigureAwait(false);
            Wait(latch);
        }

        [Test]
        public void TestBasicConnectionRecovery()
        {
            Assert.IsTrue(_conn.IsOpen);
            CloseAndWaitForRecovery();
            Assert.IsTrue(_conn.IsOpen);
        }

        [Test]
        public void TestBasicConnectionRecoveryWithHostnameList()
        {
            using (AutorecoveringConnection c = CreateAutorecoveringConnection(new List<string> { "127.0.0.1", "localhost" }))
            {
                Assert.IsTrue(c.IsOpen);
                CloseAndWaitForRecovery(c);
                Assert.IsTrue(c.IsOpen);
            }
        }

        [Test]
        public void TestBasicConnectionRecoveryWithHostnameListAndUnreachableHosts()
        {
            using (AutorecoveringConnection c = CreateAutorecoveringConnection(new List<string> { "191.72.44.22", "127.0.0.1", "localhost" }))
            {
                Assert.IsTrue(c.IsOpen);
                CloseAndWaitForRecovery(c);
                Assert.IsTrue(c.IsOpen);
            }
        }

        [Test]
        public void TestBasicConnectionRecoveryWithEndpointList()
        {
            using (AutorecoveringConnection c = CreateAutorecoveringConnection(
                        new List<AmqpTcpEndpoint>
                        {
                            new AmqpTcpEndpoint("127.0.0.1"),
                            new AmqpTcpEndpoint("localhost")
                        }))
            {
                Assert.IsTrue(c.IsOpen);
                CloseAndWaitForRecovery(c);
                Assert.IsTrue(c.IsOpen);
            }
        }

        [Test]
        public void TestBasicConnectionRecoveryStopsAfterManualClose()
        {
            Assert.IsTrue(_conn.IsOpen);
            AutorecoveringConnection c = CreateAutorecoveringConnection();
            var latch = new AutoResetEvent(false);
            c.ConnectionRecoveryError += (o, args) => latch.Set();
            StopRabbitMQ();
            latch.WaitOne(30000); // we got the failed reconnection event.
            bool triedRecoveryAfterClose = false;
            c.Close();
            Thread.Sleep(5000);
            c.ConnectionRecoveryError += (o, args) => triedRecoveryAfterClose = true;
            Thread.Sleep(10000);
            Assert.IsFalse(triedRecoveryAfterClose);
            StartRabbitMQ();
        }

        [Test]
        public void TestBasicConnectionRecoveryWithEndpointListAndUnreachableHosts()
        {
            using (AutorecoveringConnection c = CreateAutorecoveringConnection(
                        new List<AmqpTcpEndpoint>
                        {
                            new AmqpTcpEndpoint("191.72.44.22"),
                            new AmqpTcpEndpoint("127.0.0.1"),
                            new AmqpTcpEndpoint("localhost")
                        }))
            {
                Assert.IsTrue(c.IsOpen);
                CloseAndWaitForRecovery(c);
                Assert.IsTrue(c.IsOpen);
            }
        }

        [Test]
        public void TestBasicConnectionRecoveryOnBrokerRestart()
        {
            Assert.IsTrue(_conn.IsOpen);
            RestartServerAndWaitForRecovery();
            Assert.IsTrue(_conn.IsOpen);
        }

        [Test]
        public void TestChannelRecovery()
        {
            Assert.IsTrue(_channel.IsOpen);
            CloseAndWaitForRecovery();
            Assert.IsTrue(_channel.IsOpen);
        }

        [Test]
        public void TestChannelRecoveryOnServerRestart()
        {
            Assert.IsTrue(_channel.IsOpen);
            RestartServerAndWaitForRecovery();
            Assert.IsTrue(_channel.IsOpen);
        }

        [Test]
        public Task TestBasicNackAfterChannelRecovery()
        {
            var latch = new ManualResetEventSlim(false);
            var cons = new NackingBasicConsumer(_channel, latch, CloseAndWaitForRecovery);

            return TestDelayedBasicAckNackAfterChannelRecovery(cons, latch);
        }

        [Test]
        public async Task TestBlockedListenersRecovery()
        {
            var latch = new ManualResetEventSlim(false);
            _conn.ConnectionBlocked += (c, reason) => latch.Set();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();

            await BlockAsync().ConfigureAwait(false);
            Wait(latch);

            Unblock();
        }

        [Test]
        public Task TestClientNamedQueueRecovery()
        {
            string s = "dotnet-client.test.recovery.q1";
            return WithTemporaryNonExclusiveQueueAsync(_channel, async (ch, q) =>
            {
                CloseAndWaitForRecovery();
                await AssertQueueRecoveryAsync(ch, q, false).ConfigureAwait(false);
                await _channel.DeleteQueueAsync(q).ConfigureAwait(false);
            }, s);
        }

        [Test]
        public Task TestClientNamedQueueRecoveryNoWait()
        {
            string s = "dotnet-client.test.recovery.q1-nowait";
            return WithTemporaryQueueNoWaitAsync(_channel, (ch, q) =>
            {
                CloseAndWaitForRecovery();
                return AssertQueueRecoveryAsync(ch, q);
            }, s);
        }

        [Test]
        public Task TestClientNamedQueueRecoveryOnServerRestart()
        {
            string s = "dotnet-client.test.recovery.q1";
            return WithTemporaryNonExclusiveQueueAsync(_channel, async (ch, q) =>
            {
                RestartServerAndWaitForRecovery();
                await AssertQueueRecoveryAsync(ch, q, false).ConfigureAwait(false);
                await _channel.DeleteQueueAsync(q).ConfigureAwait(false);
            }, s);
        }

        [Test]
        public async Task TestConsumerWorkServiceRecovery()
        {
            using (AutorecoveringConnection c = CreateAutorecoveringConnection())
            {
                IChannel ch = await c.CreateChannelAsync().ConfigureAwait(false);
                (string q, _, _) = await ch.DeclareQueueAsync("dotnet-client.recovery.consumer_work_pool1", false, false, false).ConfigureAwait(false);
                var cons = new EventingBasicConsumer(ch);
                await ch.ActivateConsumerAsync(cons, q, true).ConfigureAwait(false);
                await AssertConsumerCountAsync(ch, q, 1).ConfigureAwait(false);

                CloseAndWaitForRecovery(c);

                Assert.IsTrue(ch.IsOpen);
                var latch = new ManualResetEventSlim(false);
                cons.Received += (s, args) => latch.Set();

                await ch.PublishMessageAsync("", q, null, _encoding.GetBytes("msg")).ConfigureAwait(false);
                Wait(latch);

                await ch.DeleteQueueAsync(q).ConfigureAwait(false);
            }
        }

        [Test]
        public async Task TestConsumerRecoveryOnClientNamedQueueWithOneRecovery()
        {
            string q0 = "dotnet-client.recovery.queue1";
            using (AutorecoveringConnection c = CreateAutorecoveringConnection())
            {
                IChannel ch = await c.CreateChannelAsync().ConfigureAwait(false);
                (string q1, _, _) = await ch.DeclareQueueAsync(q0, false, false, false).ConfigureAwait(false);
                Assert.AreEqual(q0, q1);

                var cons = new EventingBasicConsumer(ch);
                await ch.ActivateConsumerAsync(cons, q1, true).ConfigureAwait(false);
                await AssertConsumerCountAsync(ch, q1, 1).ConfigureAwait(false);

                bool queueNameChangeAfterRecoveryCalled = false;

                c.QueueNameChangeAfterRecovery += (source, ea) => { queueNameChangeAfterRecoveryCalled = true; };

                CloseAndWaitForRecovery(c);
                await AssertConsumerCountAsync(ch, q1, 1).ConfigureAwait(false);
                Assert.False(queueNameChangeAfterRecoveryCalled);

                CloseAndWaitForRecovery(c);
                await AssertConsumerCountAsync(ch, q1, 1).ConfigureAwait(false);
                Assert.False(queueNameChangeAfterRecoveryCalled);

                CloseAndWaitForRecovery(c);
                await AssertConsumerCountAsync(ch, q1, 1).ConfigureAwait(false);
                Assert.False(queueNameChangeAfterRecoveryCalled);

                var latch = new ManualResetEventSlim(false);
                cons.Received += (s, args) => latch.Set();

                await ch.PublishMessageAsync("", q1, null, _encoding.GetBytes("msg")).ConfigureAwait(false);
                Wait(latch);

                await ch.DeleteQueueAsync(q1).ConfigureAwait(false);
            }
        }

        [Test]
        public async Task TestConsumerRecoveryWithManyConsumers()
        {
            (string q, _, _) = await _channel.DeclareQueueAsync(GenerateQueueName(), false, false, false).ConfigureAwait(false);
            int n = 1024;

            for (int i = 0; i < n; i++)
            {
                var cons = new EventingBasicConsumer(_channel);
                await _channel.ActivateConsumerAsync(cons, q, true).ConfigureAwait(false);
            }

            var latch = new ManualResetEventSlim(false);
            ((AutorecoveringConnection)_conn).ConsumerTagChangeAfterRecovery += (prev, current) => latch.Set();

            CloseAndWaitForRecovery();
            Wait(latch);
            Assert.IsTrue(_channel.IsOpen);
            await AssertConsumerCountAsync(q, n).ConfigureAwait(false);
        }

        [Test]
        public async Task TestCreateChannelOnClosedAutorecoveringConnectionDoesNotHang()
        {
            // we don't want this to recover quickly in this test
            AutorecoveringConnection c = CreateAutorecoveringConnection(TimeSpan.FromSeconds(20));

            try
            {
                c.Close();
                WaitForShutdown(c);
                Assert.IsFalse(c.IsOpen);
                await c.CreateChannelAsync().ConfigureAwait(false);
                Assert.Fail("Expected an exception");
            }
            catch (AlreadyClosedException)
            {
                // expected
            }
            finally
            {
                StartRabbitMQ();
                if (c.IsOpen)
                {
                    c.Abort();
                }
            }
        }

        [Test]
        public async Task TestDeclarationOfManyAutoDeleteExchangesWithTransientExchangesThatAreDeleted()
        {
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 3; i++)
            {
                string x1 = $"source-{Guid.NewGuid()}";
                await _channel.DeclareExchangeAsync(x1, "fanout", false, true).ConfigureAwait(false);
                string x2 = $"destination-{Guid.NewGuid()}";
                await _channel.DeclareExchangeAsync(x2, "fanout", false, false).ConfigureAwait(false);
                await _channel.BindExchangeAsync(x2, x1, "").ConfigureAwait(false);
                await _channel.DeleteExchangeAsync(x2).ConfigureAwait(false);
            }
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
        }

        [Test]
        public async Task TestDeclarationOfManyAutoDeleteExchangesWithTransientExchangesThatAreUnbound()
        {
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string x1 = $"source-{Guid.NewGuid()}";
                await _channel.DeclareExchangeAsync(x1, "fanout", false, true).ConfigureAwait(false);
                string x2 = $"destination-{Guid.NewGuid()}";
                await _channel.DeclareExchangeAsync(x2, "fanout", false, false).ConfigureAwait(false);
                await _channel.BindExchangeAsync(x2, x1, "").ConfigureAwait(false);
                await _channel.UnbindExchangeAsync(x2, x1, "").ConfigureAwait(false);
                await _channel.DeleteExchangeAsync(x2).ConfigureAwait(false);
            }
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
        }

        [Test]
        public async Task TestDeclarationOfManyAutoDeleteExchangesWithTransientQueuesThatAreDeleted()
        {
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string x = Guid.NewGuid().ToString();
                await _channel.DeclareExchangeAsync(x, "fanout", false, true).ConfigureAwait(false);
                (string q, _, _) = await _channel.DeclareQueueAsync().ConfigureAwait(false);
                await _channel.BindQueueAsync(q, x, "").ConfigureAwait(false);
                await _channel.DeleteQueueAsync(q).ConfigureAwait(false);
            }
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
        }

        [Test]
        public async Task TestDeclarationOfManyAutoDeleteExchangesWithTransientQueuesThatAreUnbound()
        {
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string x = Guid.NewGuid().ToString();
                await _channel.DeclareExchangeAsync(x, "fanout", false, true).ConfigureAwait(false);
                (string q, _, _) = await _channel.DeclareQueueAsync().ConfigureAwait(false);
                await _channel.BindQueueAsync(q, x, "").ConfigureAwait(false);
                await _channel.UnbindQueueAsync(q, x, "").ConfigureAwait(false);
            }
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
        }

        [Test]
        public async Task TestDeclarationOfManyAutoDeleteQueuesWithTransientConsumer()
        {
            AssertRecordedQueues((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string q = Guid.NewGuid().ToString();
                await _channel.DeclareQueueAsync(q, false, false, true).ConfigureAwait(false);
                var dummy = new EventingBasicConsumer(_channel);
                string tag = await _channel.ActivateConsumerAsync(dummy, q, true).ConfigureAwait(false);
                await _channel.CancelConsumerAsync(tag).ConfigureAwait(false);
            }
            AssertRecordedQueues((AutorecoveringConnection)_conn, 0);
        }

        [Test]
        public async Task TestExchangeRecovery()
        {
            string x = "dotnet-client.test.recovery.x1";
            await _channel.DeclareExchangeAsync(x, "fanout", false).ConfigureAwait(false);
            CloseAndWaitForRecovery();
            await AssertExchangeRecoveryAsync(_channel, x).ConfigureAwait(false);
            await _channel.DeleteExchangeAsync(x).ConfigureAwait(false);
        }

        [Test]
        public async Task TestExchangeRecoveryWithNoWait()
        {
            string x = "dotnet-client.test.recovery.x1-nowait";
            await _channel.DeclareExchangeAsync(x, "fanout", false, false, waitForConfirmation: false);
            CloseAndWaitForRecovery();
            await AssertExchangeRecoveryAsync(_channel, x).ConfigureAwait(false);
            await _channel.DeleteExchangeAsync(x).ConfigureAwait(false);
        }

        [Test]
        public async Task TestExchangeToExchangeBindingRecovery()
        {
            (string q, _, _) = await _channel.DeclareQueueAsync("", false, false, false).ConfigureAwait(false);
            string x1 = "amq.fanout";
            string x2 = GenerateExchangeName();

            await _channel.DeclareExchangeAsync(x2, "fanout").ConfigureAwait(false);
            await _channel.BindExchangeAsync(x1, x2, "").ConfigureAwait(false);
            await _channel.BindQueueAsync(q, x1, "").ConfigureAwait(false);

            try
            {
                CloseAndWaitForRecovery();
                Assert.IsTrue(_channel.IsOpen);
                await _channel.PublishMessageAsync(x2, "", null, _encoding.GetBytes("msg")).ConfigureAwait(false);
                await AssertMessageCountAsync(q, 1).ConfigureAwait(false);
            }
            finally
            {
                await WithTemporaryChannelAsync(async ch =>
                {
                    await ch.DeleteExchangeAsync(x2).ConfigureAwait(false);
                    await ch.DeleteQueueAsync(q).ConfigureAwait(false);
                });
            }
        }

        [Test]
        public async Task TestQueueRecoveryWithManyQueues()
        {
            var qs = new List<string>();
            int n = 1024;
            for (int i = 0; i < n; i++)
            {
                (string queueName, _, _) = await _channel.DeclareQueueAsync(GenerateQueueName(), false, false, false).ConfigureAwait(false);
                qs.Add(queueName);
            }
            CloseAndWaitForRecovery();
            Assert.IsTrue(_channel.IsOpen);
            foreach (string q in qs)
            {
                await AssertQueueRecoveryAsync(_channel, q, false).ConfigureAwait(false);
                await _channel.DeleteQueueAsync(q).ConfigureAwait(false);
            }
        }

        // rabbitmq/rabbitmq-dotnet-client#43
        [Test]
        public async Task TestClientNamedTransientAutoDeleteQueueAndBindingRecovery()
        {
            string q = Guid.NewGuid().ToString();
            string x = "tmp-fanout";
            IChannel ch = await _conn.CreateChannelAsync().ConfigureAwait(false);
            await ch.DeleteQueueAsync(q).ConfigureAwait(false);
            await ch.DeleteExchangeAsync(x).ConfigureAwait(false);
            await ch.DeclareExchangeAsync(exchange: x, type: "fanout").ConfigureAwait(false);
            await ch.DeclareQueueAsync(queue: q, durable: false, exclusive: false, autoDelete: true, arguments: null).ConfigureAwait(false);
            await ch.BindQueueAsync(queue: q, exchange: x, routingKey: "").ConfigureAwait(false);
            RestartServerAndWaitForRecovery();
            Assert.IsTrue(ch.IsOpen);
            await ch.ActivatePublishTagsAsync().ConfigureAwait(false);
            await ch.PurgeQueueAsync(q).ConfigureAwait(false);
            await ch.DeclareExchangeAsync(exchange: x, type: "fanout").ConfigureAwait(false);
            await ch.PublishMessageAsync(exchange: x, routingKey: "", basicProperties: null, body: _encoding.GetBytes("msg")).ConfigureAwait(false);
            WaitForConfirms(ch);
            uint messageCount = await ch.GetQueueMessageCountAsync(q).ConfigureAwait(false);
            Assert.AreEqual(1, messageCount);
            await ch.DeleteQueueAsync(q).ConfigureAwait(false);
            await ch.DeleteExchangeAsync(x).ConfigureAwait(false);
        }

        // rabbitmq/rabbitmq-dotnet-client#43
        [Test]
        public async Task TestServerNamedTransientAutoDeleteQueueAndBindingRecovery()
        {
            string x = "tmp-fanout";
            IChannel ch = await _conn.CreateChannelAsync().ConfigureAwait(false);
            await ch.DeleteExchangeAsync(x).ConfigureAwait(false);
            await ch.DeclareExchangeAsync(exchange: x, type: "fanout").ConfigureAwait(false);
            (string q, _, _) = await ch.DeclareQueueAsync(queue: "", durable: false, exclusive: false, autoDelete: true).ConfigureAwait(false);
            string nameBefore = q;
            string nameAfter = null;
            var latch = new ManualResetEventSlim(false);
            ((AutorecoveringConnection)_conn).QueueNameChangeAfterRecovery += (source, ea) =>
            {
                nameBefore = ea.NameBefore;
                nameAfter = ea.NameAfter;
                latch.Set();
            };
            await ch.BindQueueAsync(queue: nameBefore, exchange: x, routingKey: "").ConfigureAwait(false);
            RestartServerAndWaitForRecovery();
            Wait(latch);
            Assert.IsTrue(ch.IsOpen);
            Assert.AreNotEqual(nameBefore, nameAfter);
            await ch.ActivatePublishTagsAsync().ConfigureAwait(false);
            await ch.DeclareExchangeAsync(exchange: x, type: "fanout").ConfigureAwait(false);
            await ch.PublishMessageAsync(exchange: x, routingKey: "", basicProperties: null, body: _encoding.GetBytes("msg")).ConfigureAwait(false);
            WaitForConfirms(ch);
            uint messageCount = await ch.GetQueueMessageCountAsync(nameAfter).ConfigureAwait(false);
            Assert.AreEqual(1, messageCount);
            await ch.DeleteQueueAsync(q).ConfigureAwait(false);
            await ch.DeleteExchangeAsync(x).ConfigureAwait(false);
        }

        [Test]
        public void TestRecoveryEventHandlersOnChannel()
        {
            int counter = 0;
            ((AutorecoveringChannel)_channel).Recovery += (source, ea) => System.Threading.Interlocked.Increment(ref counter);

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(_conn.IsOpen);

            Assert.IsTrue(counter >= 1);
        }

        [Test]
        public async Task TestRecoveryWithTopologyDisabled()
        {
            AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryDisabled();
            IChannel ch = await conn.CreateChannelAsync().ConfigureAwait(false);
            string s = "dotnet-client.test.recovery.q2";
            await ch.DeleteQueueAsync(s).ConfigureAwait(false);
            await ch.DeclareQueueAsync(s, false, true, false).ConfigureAwait(false);
            await ch.DeclareQueuePassiveAsync(s).ConfigureAwait(false);
            Assert.IsTrue(ch.IsOpen);

            try
            {
                CloseAndWaitForRecovery(conn);
                Assert.IsTrue(ch.IsOpen);
                await ch.DeclareQueuePassiveAsync(s).ConfigureAwait(false);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException)
            {
                // expected
            }
            finally
            {
                conn.Abort();
            }
        }

        [Test]
        public async Task TestServerNamedQueueRecovery()
        {
            (string q, _, _) = await _channel.DeclareQueueAsync("", false, false, false).ConfigureAwait(false);
            string x = "amq.fanout";
            await _channel.BindQueueAsync(q, x, "").ConfigureAwait(false);

            string nameBefore = q;
            string nameAfter = null;

            var latch = new ManualResetEventSlim(false);
            var connection = (AutorecoveringConnection)_conn;
            connection.RecoverySucceeded += (source, ea) => latch.Set();
            connection.QueueNameChangeAfterRecovery += (source, ea) => { nameAfter = ea.NameAfter; };

            CloseAndWaitForRecovery();
            Wait(latch);

            Assert.IsNotNull(nameAfter);
            Assert.IsTrue(nameBefore.StartsWith("amq."));
            Assert.IsTrue(nameAfter.StartsWith("amq."));
            Assert.AreNotEqual(nameBefore, nameAfter);

            await _channel.DeclareQueuePassiveAsync(nameAfter).ConfigureAwait(false);
        }

        [Test]
        public void TestShutdownEventHandlersRecoveryOnConnection()
        {
            int counter = 0;
            _conn.ConnectionShutdown += (c, args) => System.Threading.Interlocked.Increment(ref counter);

            Assert.IsTrue(_conn.IsOpen);
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(_conn.IsOpen);

            Assert.IsTrue(counter >= 3);
        }

        [Test]
        public void TestShutdownEventHandlersRecoveryOnConnectionAfterDelayedServerRestart()
        {
            int counter = 0;
            _conn.ConnectionShutdown += (c, args) => System.Threading.Interlocked.Increment(ref counter);
            ManualResetEventSlim shutdownLatch = PrepareForShutdown(_conn);
            ManualResetEventSlim recoveryLatch = PrepareForRecovery((AutorecoveringConnection)_conn);

            Assert.IsTrue(_conn.IsOpen);
            StopRabbitMQ();
            Console.WriteLine("Stopped RabbitMQ. About to sleep for multiple recovery intervals...");
            Thread.Sleep(7000);
            StartRabbitMQ();
            Wait(shutdownLatch, TimeSpan.FromSeconds(30));
            Wait(recoveryLatch, TimeSpan.FromSeconds(30));
            Assert.IsTrue(_conn.IsOpen);

            Assert.IsTrue(counter >= 1);
        }

        [Test]
        public void TestShutdownEventHandlersRecoveryOnChannel()
        {
            int counter = 0;
            _channel.Shutdown += args => System.Threading.Interlocked.Increment(ref counter);

            Assert.IsTrue(_channel.IsOpen);
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(_channel.IsOpen);

            Assert.GreaterOrEqual(3, counter);
        }

        [Test]
        public async Task TestThatCancelledConsumerDoesNotReappearOnRecovery()
        {
            (string q, _, _) = await _channel.DeclareQueueAsync(GenerateQueueName(), false, false, false).ConfigureAwait(false);
            int n = 1024;

            for (int i = 0; i < n; i++)
            {
                var cons = new EventingBasicConsumer(_channel);
                string tag = await _channel.ActivateConsumerAsync(cons, q, true);
                await _channel.CancelConsumerAsync(tag).ConfigureAwait(false);
            }
            CloseAndWaitForRecovery();
            Assert.IsTrue(_channel.IsOpen);
            await AssertConsumerCountAsync(q, 0);
        }

        [Test]
        public async Task TestThatDeletedExchangeBindingsDontReappearOnRecovery()
        {
            (string q, _, _) = await _channel.DeclareQueueAsync("", false, false, false).ConfigureAwait(false);
            string x1 = "amq.fanout";
            string x2 = GenerateExchangeName();

            await _channel.DeclareExchangeAsync(x2, "fanout").ConfigureAwait(false);
            await _channel.BindExchangeAsync(x1, x2, "").ConfigureAwait(false);
            await _channel.BindQueueAsync(q, x1, "").ConfigureAwait(false);
            await _channel.UnbindExchangeAsync(x1, x2, "").ConfigureAwait(false);

            try
            {
                CloseAndWaitForRecovery();
                Assert.IsTrue(_channel.IsOpen);
                await _channel.PublishMessageAsync(x2, "", null, _encoding.GetBytes("msg")).ConfigureAwait(false);
                await AssertMessageCountAsync(q, 0).ConfigureAwait(false);
            }
            finally
            {
                await WithTemporaryChannelAsync(async ch =>
                {
                    await ch.DeleteExchangeAsync(x2).ConfigureAwait(false);
                    await ch.DeleteQueueAsync(q).ConfigureAwait(false);
                });
            }
        }

        [Test]
        public async Task TestThatDeletedExchangesDontReappearOnRecovery()
        {
            string x = GenerateExchangeName();
            await _channel.DeclareExchangeAsync(x, "fanout").ConfigureAwait(false);
            await _channel.DeleteExchangeAsync(x).ConfigureAwait(false);

            try
            {
                CloseAndWaitForRecovery();
                Assert.IsTrue(_channel.IsOpen);
                await _channel.DeclareExchangePassiveAsync(x).ConfigureAwait(false);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException e)
            {
                // expected
                AssertShutdownError(e.ShutdownReason, 404);
            }
        }

        [Test]
        public async Task TestThatDeletedQueueBindingsDontReappearOnRecovery()
        {
            (string q, _, _) = await _channel.DeclareQueueAsync("", false, false, false).ConfigureAwait(false);
            string x1 = "amq.fanout";
            string x2 = GenerateExchangeName();

            await _channel.DeclareExchangeAsync(x2, "fanout").ConfigureAwait(false);
            await _channel.BindExchangeAsync(x1, x2, "").ConfigureAwait(false);
            await _channel.BindQueueAsync(q, x1, "").ConfigureAwait(false);
            await _channel.UnbindQueueAsync(q, x1, "").ConfigureAwait(false);

            try
            {
                CloseAndWaitForRecovery();
                Assert.IsTrue(_channel.IsOpen);
                await _channel.PublishMessageAsync(x2, "", null, _encoding.GetBytes("msg")).ConfigureAwait(false);
                await AssertMessageCountAsync(q, 0);
            }
            finally
            {
                await WithTemporaryChannelAsync(async ch =>
                {
                    await ch.DeleteExchangeAsync(x2).ConfigureAwait(false);
                    await ch.DeleteQueueAsync(q).ConfigureAwait(false);
                });
            }
        }

        [Test]
        public async Task TestThatDeletedQueuesDontReappearOnRecovery()
        {
            string q = "dotnet-client.recovery.q1";
            await _channel.DeclareQueueAsync(q, false, false, false).ConfigureAwait(false);
            await _channel.DeleteQueueAsync(q).ConfigureAwait(false);

            try
            {
                CloseAndWaitForRecovery();
                Assert.IsTrue(_channel.IsOpen);
                await _channel.DeclareQueuePassiveAsync(q).ConfigureAwait(false);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException e)
            {
                // expected
                AssertShutdownError(e.ShutdownReason, 404);
            }
        }

        [Test]
        public async Task TestUnblockedListenersRecovery()
        {
            var latch = new ManualResetEventSlim(false);
            _conn.ConnectionUnblocked += (source, ea) => latch.Set();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();

            await BlockAsync().ConfigureAwait(false);
            Unblock();
            Wait(latch);
        }

        internal async Task AssertExchangeRecoveryAsync(IChannel ch, string x)
        {
            await ch.ActivatePublishTagsAsync().ConfigureAwait(false);
            await WithTemporaryNonExclusiveQueueAsync(ch, async (_, q) =>
            {
                string rk = "routing-key";
                await ch.BindQueueAsync(q, x, rk).ConfigureAwait(false);
                byte[] mb = RandomMessageBody();
                await ch.PublishMessageAsync(x, rk, null, mb).ConfigureAwait(false);

                Assert.IsTrue(WaitForConfirms(ch));
                await ch.DeclareExchangePassiveAsync(x).ConfigureAwait(false);
            });
        }

        internal async Task AssertQueueRecoveryAsync(IChannel ch, string q, bool exclusive = true)
        {
            await ch.ActivatePublishTagsAsync().ConfigureAwait(false);
            await ch.DeclareQueuePassiveAsync(q).ConfigureAwait(false);
            var ok1 = await ch.DeclareQueueAsync(q, false, exclusive, false).ConfigureAwait(false);
            Assert.AreEqual(ok1.MessageCount, 0);
            await ch.PublishMessageAsync("", q, null, _encoding.GetBytes("")).ConfigureAwait(false);
            Assert.IsTrue(WaitForConfirms(ch));
            var ok2 = await ch.DeclareQueueAsync(q, false, exclusive, false).ConfigureAwait(false);
            Assert.AreEqual(ok2.MessageCount, 1);
        }

        internal void AssertRecordedExchanges(AutorecoveringConnection c, int n)
        {
            Assert.AreEqual(n, c.RecordedExchangesCount);
        }

        internal void AssertRecordedQueues(AutorecoveringConnection c, int n)
        {
            Assert.AreEqual(n, c.RecordedQueuesCount);
        }

        internal void CloseAllAndWaitForRecovery(AutorecoveringConnection conn)
        {
            ManualResetEventSlim rl = PrepareForRecovery(conn);
            CloseAllConnections();
            Wait(rl);
        }

        internal void CloseAndWaitForRecovery()
        {
            CloseAndWaitForRecovery((AutorecoveringConnection)_conn);
        }

        internal void CloseAndWaitForRecovery(AutorecoveringConnection conn)
        {
            ManualResetEventSlim sl = PrepareForShutdown(conn);
            ManualResetEventSlim rl = PrepareForRecovery(conn);
            CloseConnection(conn);
            Wait(sl);
            Wait(rl);
        }

        internal ManualResetEventSlim PrepareForRecovery(AutorecoveringConnection conn)
        {
            var latch = new ManualResetEventSlim(false);
            conn.RecoverySucceeded += (source, ea) => latch.Set();

            return latch;
        }

        internal ManualResetEventSlim PrepareForShutdown(IConnection conn)
        {
            var latch = new ManualResetEventSlim(false);
            conn.ConnectionShutdown += (c, args) => latch.Set();

            return latch;
        }

        protected override void ReleaseResources()
        {
            Unblock();
        }

        internal void RestartServerAndWaitForRecovery()
        {
            RestartServerAndWaitForRecovery((AutorecoveringConnection)_conn);
        }

        internal void RestartServerAndWaitForRecovery(AutorecoveringConnection conn)
        {
            ManualResetEventSlim sl = PrepareForShutdown(conn);
            ManualResetEventSlim rl = PrepareForRecovery(conn);
            RestartRabbitMQ();
            Wait(sl);
            Wait(rl);
        }

        internal async Task TestDelayedBasicAckNackAfterChannelRecovery(TestBasicConsumer1 cons, ManualResetEventSlim latch)
        {
            (string q, _, _) = await _channel.DeclareQueueAsync(GenerateQueueName(), false, false, false).ConfigureAwait(false);
            int n = 30;
            await _channel.SetQosAsync(0, 1, false).ConfigureAwait(false);
            await _channel.ActivateConsumerAsync(cons, q, false).ConfigureAwait(false);

            AutorecoveringConnection publishingConn = CreateAutorecoveringConnection();
            IChannel publishingChannel = await publishingConn.CreateChannelAsync().ConfigureAwait(false);

            for (int i = 0; i < n; i++)
            {
                await publishingChannel.PublishMessageAsync("", q, null, _encoding.GetBytes("")).ConfigureAwait(false);
            }

            Wait(latch, TimeSpan.FromSeconds(20));
            await _channel.DeleteQueueAsync(q);
            await publishingChannel.CloseAsync().ConfigureAwait(false);
            publishingConn.Close();
        }

        internal void WaitForShutdown(IConnection conn)
        {
            Wait(PrepareForShutdown(conn));
        }

        public class AckingBasicConsumer : TestBasicConsumer1
        {
            public AckingBasicConsumer(IChannel channel, ManualResetEventSlim latch, Action fn)
                : base(channel, latch, fn)
            {
            }

            public override void PostHandleDelivery(ulong deliveryTag)
            {
                Channel.AckMessageAsync(deliveryTag, false).AsTask().GetAwaiter().GetResult();
            }
        }

        public class NackingBasicConsumer : TestBasicConsumer1
        {
            public NackingBasicConsumer(IChannel channel, ManualResetEventSlim latch, Action fn)
                : base(channel, latch, fn)
            {
            }

            public override void PostHandleDelivery(ulong deliveryTag)
            {
                Channel.NackMessageAsync(deliveryTag, false, false).AsTask().GetAwaiter().GetResult();
            }
        }
        public class TestBasicConsumer1 : DefaultBasicConsumer
        {
            private readonly Action _action;
            private readonly ManualResetEventSlim _latch;
            private ushort _counter = 0;

            public TestBasicConsumer1(IChannel channel, ManualResetEventSlim latch, Action fn)
                : base(channel)
            {
                _latch = latch;
                _action = fn;
            }

            public override void HandleBasicDeliver(string consumerTag,
                ulong deliveryTag,
                bool redelivered,
                string exchange,
                string routingKey,
                IBasicProperties properties,
                ReadOnlyMemory<byte> body)
            {
                try
                {
                    if (deliveryTag == 7 && _counter < 10)
                    {
                        _action();
                    }
                    if (_counter == 9)
                    {
                        _latch.Set();
                    }
                    PostHandleDelivery(deliveryTag);
                }
                finally
                {
                    _counter += 1;
                }
            }

            public virtual void PostHandleDelivery(ulong deliveryTag)
            {
            }
        }
    }
}

#pragma warning restore 0168
