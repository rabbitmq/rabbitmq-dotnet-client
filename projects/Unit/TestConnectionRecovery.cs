// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
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
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at https://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;

#pragma warning disable 0618

namespace RabbitMQ.Client.Unit
{
    internal class DisposableConnection : IDisposable
    {
        public DisposableConnection(AutorecoveringConnection c) => Connection = c;

        public AutorecoveringConnection Connection { get; private set; }

        public void Dispose() => Connection.Close();
    }
    [TestFixture]
    public class TestConnectionRecovery : IntegrationFixture
    {
        [SetUp]
        public override async ValueTask Init()
        {
            Conn = await CreateAutorecoveringConnection();
            Model = await Conn.CreateModel();
        }

        [TearDown]
        public async ValueTask CleanUp() => await Conn.Close();

        [Test]
        public async ValueTask TestBasicAckAfterChannelRecovery()
        {
            var latch = new ManualResetEventSlim(false);
            var cons = new AckingBasicConsumer(Model, latch, CloseAndWaitForRecovery);

            await TestDelayedBasicAckNackAfterChannelRecovery(cons, latch);
        }

        [Test]
        public async ValueTask TestBasicAckAfterBasicGetAndChannelRecovery()
        {
            string q = GenerateQueueName();
            await Model.QueueDeclare(q, false, false, false, null);
            // create an offset
            IBasicProperties bp = Model.CreateBasicProperties();
            await Model.BasicPublish("", q, bp, new byte[] { });
            await Task.Delay(50);
            BasicGetResult g = await Model.BasicGet(q, false);
            CloseAndWaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);
            Assert.IsTrue(Model.IsOpen);
            // ack the message after recovery - this should be out of range and ignored
            await Model.BasicAck(g.DeliveryTag, false);
            // do a sync operation to 'check' there is no channel exception 
            await Model.BasicGet(q, false);
        }

        [Test]
        public async ValueTask TestBasicAckEventHandlerRecovery()
        {
            await Model.ConfirmSelect();
            var latch = new ManualResetEventSlim(false);
            ((AutorecoveringModel)Model).BasicAcks += (m, args) => latch.Set();
            ((AutorecoveringModel)Model).BasicNacks += (m, args) => latch.Set();

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);

            await WithTemporaryNonExclusiveQueue(Model, (m, q) => { m.BasicPublish("", q, null, encoding.GetBytes("")); return default; });
            Wait(latch);
        }

        [Test]
        public void TestBasicConnectionRecovery()
        {
            Assert.IsTrue(Conn.IsOpen);
            CloseAndWaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);
        }

        [Test]
        public async ValueTask TestBasicConnectionRecoveryWithHostnameList()
        {
            using (AutorecoveringConnection c = await CreateAutorecoveringConnection(new List<string> { "127.0.0.1", "localhost" }))
            {
                Assert.IsTrue(c.IsOpen);
                CloseAndWaitForRecovery(c);
                Assert.IsTrue(c.IsOpen);
            }
        }

        [Test]
        public async ValueTask TestBasicConnectionRecoveryWithHostnameListAndUnreachableHosts()
        {
            using (AutorecoveringConnection c = await CreateAutorecoveringConnection(new List<string> { "191.72.44.22", "127.0.0.1", "localhost" }))
            {
                Assert.IsTrue(c.IsOpen);
                CloseAndWaitForRecovery(c);
                Assert.IsTrue(c.IsOpen);
            }
        }

        [Test]
        public async ValueTask TestBasicConnectionRecoveryWithEndpointList()
        {
            using (AutorecoveringConnection c = await CreateAutorecoveringConnection(
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
        public async ValueTask TestBasicConnectionRecoveryStopsAfterManualClose()
        {
            Assert.IsTrue(Conn.IsOpen);
            AutorecoveringConnection c = await CreateAutorecoveringConnection();
            var latch = new AutoResetEvent(false);
            c.ConnectionRecoveryError += (o, args) => latch.Set();
            StopRabbitMQ();
            latch.WaitOne(30000); // we got the failed reconnection event.
            bool triedRecoveryAfterClose = false;
            await c.Close();
            await Task.Delay(5000);
            c.ConnectionRecoveryError += (o, args) => triedRecoveryAfterClose = true;
            await Task.Delay(10000);
            Assert.IsFalse(triedRecoveryAfterClose);
            StartRabbitMQ();
        }

        [Test]
        public async ValueTask TestBasicConnectionRecoveryWithEndpointListAndUnreachableHosts()
        {
            using (AutorecoveringConnection c = await CreateAutorecoveringConnection(
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
            Assert.IsTrue(Conn.IsOpen);
            RestartServerAndWaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);
        }

        [Test]
        public void TestBasicModelRecovery()
        {
            Assert.IsTrue(Model.IsOpen);
            CloseAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);
        }

        [Test]
        public void TestBasicModelRecoveryOnServerRestart()
        {
            Assert.IsTrue(Model.IsOpen);
            RestartServerAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);
        }

        [Test]
        public async ValueTask TestBasicNackAfterChannelRecovery()
        {
            var latch = new ManualResetEventSlim(false);
            var cons = new NackingBasicConsumer(Model, latch, CloseAndWaitForRecovery);

            await TestDelayedBasicAckNackAfterChannelRecovery(cons, latch);
        }

        [Test]
        public async ValueTask TestBasicRejectAfterChannelRecovery()
        {
            var latch = new ManualResetEventSlim(false);
            var cons = new RejectingBasicConsumer(Model, latch, CloseAndWaitForRecovery);

            await TestDelayedBasicAckNackAfterChannelRecovery(cons, latch);
        }

        [Test]
        public async ValueTask TestBlockedListenersRecovery()
        {
            var latch = new ManualResetEventSlim(false);
            Conn.ConnectionBlocked += (c, reason) => latch.Set();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();

            await Block();
            Wait(latch);

            Unblock();
        }

        [Test]
        public async ValueTask TestClientNamedQueueRecovery()
        {
            string s = "dotnet-client.test.recovery.q1";
            await WithTemporaryNonExclusiveQueue(Model, async (m, q) =>
            {
                CloseAndWaitForRecovery();
                await AssertQueueRecovery(m, q, false);
                await Model.QueueDelete(q);
            }, s);
        }

        [Test]
        public async ValueTask TestClientNamedQueueRecoveryNoWait()
        {
            string s = "dotnet-client.test.recovery.q1-nowait";
            await WithTemporaryQueueNoWait(Model, async (m, q) =>
            {
                CloseAndWaitForRecovery();
                await AssertQueueRecovery(m, q);
            }, s);
        }

        [Test]
        public async ValueTask TestClientNamedQueueRecoveryOnServerRestart()
        {
            string s = "dotnet-client.test.recovery.q1";
            await WithTemporaryNonExclusiveQueue(Model, async (m, q) =>
            {
                RestartServerAndWaitForRecovery();
                await AssertQueueRecovery(m, q, false);
                await Model.QueueDelete(q);
            }, s);
        }

        [Test]
        public async ValueTask TestConsumerWorkServiceRecovery()
        {
            using (AutorecoveringConnection c = await CreateAutorecoveringConnection())
            {
                IModel m = await c.CreateModel();
                string q = (await m.QueueDeclare("dotnet-client.recovery.consumer_work_pool1",
                    false, false, false, null)).QueueName;
                var cons = new EventingBasicConsumer(m);
                await m.BasicConsume(q, true, cons);
                await AssertConsumerCount(m, q, 1);

                CloseAndWaitForRecovery(c);

                Assert.IsTrue(m.IsOpen);
                var latch = new ManualResetEventSlim(false);
                cons.Received += (s, args) => latch.Set();

                await m.BasicPublish("", q, null, encoding.GetBytes("msg"));
                Wait(latch);

                await m.QueueDelete(q);
            }
        }

        [Test]
        public async ValueTask TestConsumerRecoveryOnClientNamedQueueWithOneRecovery()
        {
            string q0 = "dotnet-client.recovery.queue1";
            using (AutorecoveringConnection c = await CreateAutorecoveringConnection())
            {
                IModel m = await c.CreateModel();
                string q1 = (await m.QueueDeclare(q0, false, false, false, null)).QueueName;
                Assert.AreEqual(q0, q1);

                var cons = new EventingBasicConsumer(m);
                await m.BasicConsume(q1, true, cons);
                await AssertConsumerCount(m, q1, 1);

                bool queueNameChangeAfterRecoveryCalled = false;

                c.QueueNameChangeAfterRecovery += (source, ea) => { queueNameChangeAfterRecoveryCalled = true; };

                CloseAndWaitForRecovery(c);
                await AssertConsumerCount(m, q1, 1);
                Assert.False(queueNameChangeAfterRecoveryCalled);

                CloseAndWaitForRecovery(c);
                await AssertConsumerCount(m, q1, 1);
                Assert.False(queueNameChangeAfterRecoveryCalled);

                CloseAndWaitForRecovery(c);
                await AssertConsumerCount(m, q1, 1);
                Assert.False(queueNameChangeAfterRecoveryCalled);

                var latch = new ManualResetEventSlim(false);
                cons.Received += (s, args) => latch.Set();

                await m.BasicPublish("", q1, null, encoding.GetBytes("msg"));
                Wait(latch);

                await m.QueueDelete(q1);
            }
        }

        [Test]
        public async ValueTask TestConsumerRecoveryWithManyConsumers()
        {
            string q = (await Model.QueueDeclare(GenerateQueueName(), false, false, false, null)).QueueName;
            int n = 1024;

            for (int i = 0; i < n; i++)
            {
                var cons = new EventingBasicConsumer(Model);
                await Model.BasicConsume(q, true, cons);
            }

            var latch = new ManualResetEventSlim(false);
            ((AutorecoveringConnection)Conn).ConsumerTagChangeAfterRecovery += (prev, current) => latch.Set();

            CloseAndWaitForRecovery();
            Wait(latch);
            Assert.IsTrue(Model.IsOpen);
            await AssertConsumerCount(q, n);
        }

        [Test]
        public async ValueTask TestCreateModelOnClosedAutorecoveringConnectionDoesNotHang()
        {
            // we don't want this to recover quickly in this test
            AutorecoveringConnection c = await CreateAutorecoveringConnection(TimeSpan.FromSeconds(20));

            try
            {
                await c.Close();
                WaitForShutdown(c);
                Assert.IsFalse(c.IsOpen);
                await c.CreateModel();
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
                    await c.Abort();
                }
            }
        }

        [Test]
        public async ValueTask TestDeclarationOfManyAutoDeleteExchangesWithTransientExchangesThatAreDeleted()
        {
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
            for (int i = 0; i < 3; i++)
            {
                string x1 = $"source-{Guid.NewGuid()}";
                await Model.ExchangeDeclare(x1, "fanout", false, true, null);
                string x2 = $"destination-{Guid.NewGuid()}";
                await Model.ExchangeDeclare(x2, "fanout", false, false, null);
                await Model.ExchangeBind(x2, x1, "");
                await Model.ExchangeDelete(x2);
            }
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
        }

        [Test]
        public async ValueTask TestDeclarationOfManyAutoDeleteExchangesWithTransientExchangesThatAreUnbound()
        {
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string x1 = $"source-{Guid.NewGuid()}";
                await Model.ExchangeDeclare(x1, "fanout", false, true, null);
                string x2 = $"destination-{Guid.NewGuid()}";
                await Model.ExchangeDeclare(x2, "fanout", false, false, null);
                await Model.ExchangeBind(x2, x1, "");
                await Model.ExchangeUnbind(x2, x1, "");
                await Model.ExchangeDelete(x2);
            }
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
        }

        [Test]
        public async ValueTask TestDeclarationOfManyAutoDeleteExchangesWithTransientQueuesThatAreDeleted()
        {
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string x = Guid.NewGuid().ToString();
                await Model.ExchangeDeclare(x, "fanout", false, true, null);
                QueueDeclareOk q = await Model.QueueDeclare();
                await Model.QueueBind(q, x, "");
                await Model.QueueDelete(q);
            }
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
        }

        [Test]
        public async ValueTask TestDeclarationOfManyAutoDeleteExchangesWithTransientQueuesThatAreUnbound()
        {
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string x = Guid.NewGuid().ToString();
                await Model.ExchangeDeclare(x, "fanout", false, true, null);
                QueueDeclareOk q = await Model.QueueDeclare();
                await Model.QueueBind(q, x, "");
                await Model.QueueUnbind(q, x, "", null);
            }
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
        }

        [Test]
        public async ValueTask TestDeclarationOfManyAutoDeleteQueuesWithTransientConsumer()
        {
            AssertRecordedQueues((AutorecoveringConnection)Conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string q = Guid.NewGuid().ToString();
                await Model.QueueDeclare(q, false, false, true, null);
                var dummy = new EventingBasicConsumer(Model);
                string tag = await Model.BasicConsume(q, true, dummy);
                await Model.BasicCancel(tag);
            }
            AssertRecordedQueues((AutorecoveringConnection)Conn, 0);
        }

        [Test]
        public async ValueTask TestExchangeRecovery()
        {
            string x = "dotnet-client.test.recovery.x1";
            DeclareNonDurableExchange(Model, x);
            CloseAndWaitForRecovery();
            await AssertExchangeRecovery(Model, x);
            await Model.ExchangeDelete(x);
        }

        [Test]
        public async ValueTask TestExchangeRecoveryWithNoWait()
        {
            string x = "dotnet-client.test.recovery.x1-nowait";
            DeclareNonDurableExchangeNoWait(Model, x);
            CloseAndWaitForRecovery();
            await AssertExchangeRecovery(Model, x);
            await Model.ExchangeDelete(x);
        }

        [Test]
        public async ValueTask TestExchangeToExchangeBindingRecovery()
        {
            string q = (await Model.QueueDeclare("", false, false, false, null)).QueueName;
            string x1 = "amq.fanout";
            string x2 = GenerateExchangeName();

            await Model.ExchangeDeclare(x2, "fanout");
            await Model.ExchangeBind(x1, x2, "");
            await Model.QueueBind(q, x1, "");

            try
            {
                CloseAndWaitForRecovery();
                Assert.IsTrue(Model.IsOpen);
                await Model.BasicPublish(x2, "", null, encoding.GetBytes("msg"));
                await AssertMessageCount(q, 1);
            }
            finally
            {
                await WithTemporaryModel(async m =>
                {
                    await m.ExchangeDelete(x2);
                    await m.QueueDelete(q);
                });
            }
        }

        [Test]
        public async ValueTask TestQueueRecoveryWithManyQueues()
        {
            var qs = new List<string>();
            int n = 1024;
            for (int i = 0; i < n; i++)
            {
                qs.Add((await Model.QueueDeclare(GenerateQueueName(), false, false, false, null)).QueueName);
            }
            CloseAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);
            foreach (string q in qs)
            {
                await AssertQueueRecovery(Model, q, false);
                await Model.QueueDelete(q);
            }
        }

        // rabbitmq/rabbitmq-dotnet-client#43
        [Test]
        public async ValueTask TestClientNamedTransientAutoDeleteQueueAndBindingRecovery()
        {
            string q = Guid.NewGuid().ToString();
            string x = "tmp-fanout";
            IModel ch = await Conn.CreateModel();
            await ch.QueueDelete(q);
            await ch.ExchangeDelete(x);
            await ch.ExchangeDeclare(exchange: x, type: "fanout");
            await ch.QueueDeclare(queue: q, durable: false, exclusive: false, autoDelete: true, arguments: null);
            await ch.QueueBind(queue: q, exchange: x, routingKey: "");
            RestartServerAndWaitForRecovery();
            Assert.IsTrue(ch.IsOpen);
            await ch.ConfirmSelect();
            await ch.QueuePurge(q);
            await ch.ExchangeDeclare(exchange: x, type: "fanout");
            await ch.BasicPublish(exchange: x, routingKey: "", basicProperties: null, body: encoding.GetBytes("msg"));
            await WaitForConfirms(ch);
            QueueDeclareOk ok = await ch.QueueDeclare(queue: q, durable: false, exclusive: false, autoDelete: true, arguments: null);
            Assert.AreEqual(1, ok.MessageCount);
            await ch.QueueDelete(q);
            await ch.ExchangeDelete(x);
        }

        // rabbitmq/rabbitmq-dotnet-client#43
        [Test]
        public async ValueTask TestServerNamedTransientAutoDeleteQueueAndBindingRecovery()
        {
            string x = "tmp-fanout";
            IModel ch = await Conn.CreateModel();
            await ch.ExchangeDelete(x);
            await ch.ExchangeDeclare(exchange: x, type: "fanout");
            string q = (await ch.QueueDeclare(queue: "", durable: false, exclusive: false, autoDelete: true, arguments: null)).QueueName;
            string nameBefore = q;
            string nameAfter = null;
            var latch = new ManualResetEventSlim(false);
            ((AutorecoveringConnection)Conn).QueueNameChangeAfterRecovery += (source, ea) =>
            {
                nameBefore = ea.NameBefore;
                nameAfter = ea.NameAfter;
                latch.Set();
            };
            await ch.QueueBind(queue: nameBefore, exchange: x, routingKey: "");
            RestartServerAndWaitForRecovery();
            Wait(latch);
            Assert.IsTrue(ch.IsOpen);
            Assert.AreNotEqual(nameBefore, nameAfter);
            await ch.ConfirmSelect();
            await ch.ExchangeDeclare(exchange: x, type: "fanout");
            await ch.BasicPublish(exchange: x, routingKey: "", basicProperties: null, body: encoding.GetBytes("msg"));
            await WaitForConfirms(ch);
            QueueDeclareOk ok = await ch.QueueDeclarePassive(nameAfter);
            Assert.AreEqual(1, ok.MessageCount);
            await ch.QueueDelete(q);
            await ch.ExchangeDelete(x);
        }

        [Test]
        public void TestRecoveryEventHandlersOnChannel()
        {
            int counter = 0;
            ((AutorecoveringModel)Model).Recovery += (source, ea) => Interlocked.Increment(ref counter);

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);

            Assert.IsTrue(counter >= 1);
        }

        [Test]
        public void TestRecoveryEventHandlersOnConnection()
        {
            int counter = 0;
            ((AutorecoveringConnection)Conn).RecoverySucceeded += (source, ea) => Interlocked.Increment(ref counter);

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);

            Assert.IsTrue(counter >= 3);
        }

        [Test]
        public void TestRecoveryEventHandlersOnModel()
        {
            int counter = 0;
            ((AutorecoveringModel)Model).Recovery += (source, ea) => Interlocked.Increment(ref counter);

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);

            Assert.IsTrue(counter >= 3);
        }

        [Test]
        public async ValueTask TestRecoveryWithTopologyDisabled()
        {
            AutorecoveringConnection conn = await CreateAutorecoveringConnectionWithTopologyRecoveryDisabled();
            IModel ch = await conn.CreateModel();
            string s = "dotnet-client.test.recovery.q2";
            await ch.QueueDelete(s);
            await ch.QueueDeclare(s, false, true, false, null);
            await ch.QueueDeclarePassive(s);
            Assert.IsTrue(ch.IsOpen);

            try
            {
                CloseAndWaitForRecovery(conn);
                Assert.IsTrue(ch.IsOpen);
                await ch.QueueDeclarePassive(s);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException)
            {
                // expected
            }
            finally
            {
                await conn.Abort();
            }
        }

        [Test]
        public async ValueTask TestServerNamedQueueRecovery()
        {
            string q = (await Model.QueueDeclare("", false, false, false, null)).QueueName;
            string x = "amq.fanout";
            await Model.QueueBind(q, x, "");

            string nameBefore = q;
            string nameAfter = null;

            var latch = new ManualResetEventSlim(false);
            var connection = (AutorecoveringConnection)Conn;
            connection.RecoverySucceeded += (source, ea) => latch.Set();
            connection.QueueNameChangeAfterRecovery += (source, ea) => { nameAfter = ea.NameAfter; };

            CloseAndWaitForRecovery();
            Wait(latch);

            Assert.IsNotNull(nameAfter);
            Assert.IsTrue(nameBefore.StartsWith("amq."));
            Assert.IsTrue(nameAfter.StartsWith("amq."));
            Assert.AreNotEqual(nameBefore, nameAfter);

            await Model.QueueDeclarePassive(nameAfter);
        }

        [Test]
        public void TestShutdownEventHandlersRecoveryOnConnection()
        {
            int counter = 0;
            Conn.ConnectionShutdown += (c, args) => { Interlocked.Increment(ref counter); return default; };

            Assert.IsTrue(Conn.IsOpen);
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);

            Assert.IsTrue(counter >= 3);
        }

        [Test]
        public async ValueTask TestShutdownEventHandlersRecoveryOnConnectionAfterDelayedServerRestart()
        {
            int counter = 0;
            Conn.ConnectionShutdown += (c, args) => { Interlocked.Increment(ref counter); return default; };
            ManualResetEventSlim shutdownLatch = PrepareForShutdown(Conn);
            ManualResetEventSlim recoveryLatch = PrepareForRecovery((AutorecoveringConnection)Conn);

            Assert.IsTrue(Conn.IsOpen);
            StopRabbitMQ();
            Console.WriteLine("Stopped RabbitMQ. About to sleep for multiple recovery intervals...");
            await Task.Delay(7000);
            StartRabbitMQ();
            Wait(shutdownLatch, TimeSpan.FromSeconds(30));
            Wait(recoveryLatch, TimeSpan.FromSeconds(30));
            Assert.IsTrue(Conn.IsOpen);

            Assert.IsTrue(counter >= 1);
        }

        [Test]
        public void TestShutdownEventHandlersRecoveryOnModel()
        {
            int counter = 0;
            Model.ModelShutdown += (c, args) => { Interlocked.Increment(ref counter); return default; };

            Assert.IsTrue(Model.IsOpen);
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);

            Assert.IsTrue(counter >= 3);
        }

        [Test]
        public async ValueTask TestThatCancelledConsumerDoesNotReappearOnRecovery()
        {
            string q = (await Model.QueueDeclare(GenerateQueueName(), false, false, false, null)).QueueName;
            int n = 1024;

            for (int i = 0; i < n; i++)
            {
                var cons = new EventingBasicConsumer(Model);
                string tag = await Model.BasicConsume(q, true, cons);
                await Model.BasicCancel(tag);
            }
            CloseAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);
            await AssertConsumerCount(q, 0);
        }

        [Test]
        public async ValueTask TestThatDeletedExchangeBindingsDontReappearOnRecovery()
        {
            string q = (await Model.QueueDeclare("", false, false, false, null)).QueueName;
            string x1 = "amq.fanout";
            string x2 = GenerateExchangeName();

            await Model.ExchangeDeclare(x2, "fanout");
            await Model.ExchangeBind(x1, x2, "");
            await Model.QueueBind(q, x1, "");
            await Model.ExchangeUnbind(x1, x2, "", null);

            try
            {
                CloseAndWaitForRecovery();
                Assert.IsTrue(Model.IsOpen);
                await Model.BasicPublish(x2, "", null, encoding.GetBytes("msg"));
                await AssertMessageCount(q, 0);
            }
            finally
            {
                await WithTemporaryModel(async m =>
                {
                    await m.ExchangeDelete(x2);
                    await m.QueueDelete(q);
                });
            }
        }

        [Test]
        public async ValueTask TestThatDeletedExchangesDontReappearOnRecovery()
        {
            string x = GenerateExchangeName();
            await Model.ExchangeDeclare(x, "fanout");
            await Model.ExchangeDelete(x);

            try
            {
                CloseAndWaitForRecovery();
                Assert.IsTrue(Model.IsOpen);
                await Model.ExchangeDeclarePassive(x);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException e)
            {
                // expected
                AssertShutdownError(e.ShutdownReason, 404);
            }
        }

        [Test]
        public async ValueTask TestThatDeletedQueueBindingsDontReappearOnRecovery()
        {
            string q = (await Model.QueueDeclare("", false, false, false, null)).QueueName;
            string x1 = "amq.fanout";
            string x2 = GenerateExchangeName();

            await Model.ExchangeDeclare(x2, "fanout");
            await Model.ExchangeBind(x1, x2, "");
            await Model.QueueBind(q, x1, "");
            await Model.QueueUnbind(q, x1, "", null);

            try
            {
                CloseAndWaitForRecovery();
                Assert.IsTrue(Model.IsOpen);
                await Model.BasicPublish(x2, "", null, encoding.GetBytes("msg"));
                await AssertMessageCount(q, 0);
            }
            finally
            {
                await WithTemporaryModel(async m =>
                {
                    await m.ExchangeDelete(x2);
                    await m.QueueDelete(q);
                });
            }
        }

        [Test]
        public async ValueTask TestThatDeletedQueuesDontReappearOnRecovery()
        {
            string q = "dotnet-client.recovery.q1";
            await Model.QueueDeclare(q, false, false, false, null);
            await Model.QueueDelete(q);

            try
            {
                CloseAndWaitForRecovery();
                Assert.IsTrue(Model.IsOpen);
                await Model.QueueDeclarePassive(q);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException e)
            {
                // expected
                AssertShutdownError(e.ShutdownReason, 404);
            }
        }

        [Test]
        public async ValueTask TestUnblockedListenersRecovery()
        {
            var latch = new ManualResetEventSlim(false);
            Conn.ConnectionUnblocked += (source, ea) => latch.Set();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();

            await Block();
            Unblock();
            Wait(latch);
        }

        internal async ValueTask AssertExchangeRecovery(IModel m, string x)
        {
            await m.ConfirmSelect();
            await WithTemporaryNonExclusiveQueue(m, async (_, q) =>
            {
                string rk = "routing-key";
                await m.QueueBind(q, x, rk);
                byte[] mb = RandomMessageBody();
                await m.BasicPublish(x, rk, null, mb);

                Assert.IsTrue(await WaitForConfirms(m));
                await m.ExchangeDeclarePassive(x);
            });
        }

        internal async ValueTask AssertQueueRecovery(IModel m, string q) => await AssertQueueRecovery(m, q, true);

        internal async ValueTask AssertQueueRecovery(IModel m, string q, bool exclusive)
        {
            await m.ConfirmSelect();
            await m.QueueDeclarePassive(q);
            QueueDeclareOk ok1 = await m.QueueDeclare(q, false, exclusive, false, null);
            Assert.AreEqual(ok1.MessageCount, 0);
            await m.BasicPublish("", q, null, encoding.GetBytes(""));
            Assert.IsTrue(await WaitForConfirms(m));
            QueueDeclareOk ok2 = await m.QueueDeclare(q, false, exclusive, false, null);
            Assert.AreEqual(ok2.MessageCount, 1);
        }

        internal void AssertRecordedExchanges(AutorecoveringConnection c, int n) => Assert.AreEqual(n, c.RecordedExchangesCount);

        internal void AssertRecordedQueues(AutorecoveringConnection c, int n) => Assert.AreEqual(n, c.RecordedQueuesCount);

        internal void CloseAllAndWaitForRecovery() => CloseAllAndWaitForRecovery((AutorecoveringConnection)Conn);

        internal void CloseAllAndWaitForRecovery(AutorecoveringConnection conn)
        {
            ManualResetEventSlim rl = PrepareForRecovery(conn);
            CloseAllConnections();
            Wait(rl);
        }

        internal void CloseAndWaitForRecovery() => CloseAndWaitForRecovery((AutorecoveringConnection)Conn);

        internal void CloseAndWaitForRecovery(AutorecoveringConnection conn)
        {
            ManualResetEventSlim sl = PrepareForShutdown(conn);
            ManualResetEventSlim rl = PrepareForRecovery(conn);
            CloseConnection(conn);
            Wait(sl);
            Wait(rl);
        }

        internal void CloseAndWaitForShutdown(AutorecoveringConnection conn)
        {
            ManualResetEventSlim sl = PrepareForShutdown(conn);
            CloseConnection(conn);
            Wait(sl);
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
            conn.ConnectionShutdown += (c, args) => { latch.Set(); return default; };

            return latch;
        }

        protected override void ReleaseResources() => Unblock();

        internal void RestartServerAndWaitForRecovery() => RestartServerAndWaitForRecovery((AutorecoveringConnection)Conn);

        internal void RestartServerAndWaitForRecovery(AutorecoveringConnection conn)
        {
            ManualResetEventSlim sl = PrepareForShutdown(conn);
            ManualResetEventSlim rl = PrepareForRecovery(conn);
            RestartRabbitMQ();
            Wait(sl);
            Wait(rl);
        }

        internal async ValueTask TestDelayedBasicAckNackAfterChannelRecovery(TestBasicConsumer1 cons, ManualResetEventSlim latch)
        {
            string q = GenerateQueueName();
            await Model.QueueDeclare(q, false, false, false, null);
            int n = 30;
            await Model.BasicQos(0, 1, false);
            await Model.BasicConsume(q, false, cons);

            using (AutorecoveringConnection publishingConn = await CreateAutorecoveringConnection())
            {
                using (IModel publishingModel = await publishingConn.CreateModel())
                {
                    for (int i = 0; i < n; i++)
                    {
                        await publishingModel.BasicPublish("", q, null, encoding.GetBytes(""));
                    }
                }
            }

            Wait(latch, TimeSpan.FromSeconds(20));
            await Model.QueueDelete(q);
        }

        internal void WaitForRecovery() => Wait(PrepareForRecovery((AutorecoveringConnection)Conn));

        internal void WaitForRecovery(AutorecoveringConnection conn) => Wait(PrepareForRecovery(conn));

        internal void WaitForShutdown() => Wait(PrepareForShutdown(Conn));

        internal void WaitForShutdown(IConnection conn) => Wait(PrepareForShutdown(conn));

        public class AckingBasicConsumer : TestBasicConsumer1
        {
            public AckingBasicConsumer(IModel model, ManualResetEventSlim latch, Action fn) : base(model, latch, fn)
            {
            }

            public override ValueTask PostHandleDelivery(ulong deliveryTag) => Model.BasicAck(deliveryTag, false);
        }

        public class NackingBasicConsumer : TestBasicConsumer1
        {
            public NackingBasicConsumer(IModel model, ManualResetEventSlim latch, Action fn)
                : base(model, latch, fn)
            {
            }

            public override ValueTask PostHandleDelivery(ulong deliveryTag) => Model.BasicNack(deliveryTag, false, false);
        }

        public class RejectingBasicConsumer : TestBasicConsumer1
        {
            public RejectingBasicConsumer(IModel model, ManualResetEventSlim latch, Action fn)
                : base(model, latch, fn)
            {
            }

            public override ValueTask PostHandleDelivery(ulong deliveryTag) => Model.BasicReject(deliveryTag, false);
        }

        public class TestBasicConsumer1 : DefaultBasicConsumer
        {
            private readonly Action _action;
            private readonly ManualResetEventSlim _latch;
            private int _counter = 0;

            public TestBasicConsumer1(IModel model, ManualResetEventSlim latch, Action fn)
                : base(model)
            {
                _latch = latch;
                _action = fn;
            }

            public override async ValueTask HandleBasicDeliver(string consumerTag,
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

                    await PostHandleDelivery(deliveryTag);
                }
                finally
                {
                    Interlocked.Increment(ref _counter);
                }
            }

            public virtual ValueTask PostHandleDelivery(ulong deliveryTag) => default;
        }
    }
}

#pragma warning restore 0168
