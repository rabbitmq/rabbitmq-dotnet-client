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

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;

using Xunit;

#pragma warning disable 0618

namespace RabbitMQ.Client.Unit
{
    [CollectionDefinition("NoParallelization", DisableParallelization = true)]
    public class NoParallelizationCollection { }
    class DisposableConnection : IDisposable
    {
        public DisposableConnection(AutorecoveringConnection c)
        {
            Connection = c;
        }

        public AutorecoveringConnection Connection { get; }

        public void Dispose()
        {
            Connection.Close();
        }
    }

    [Collection("NoParallelization")]
    public class TestConnectionRecovery : IntegrationFixture
    {
        protected override void SetUp()
        {
            _conn = CreateAutorecoveringConnection();
            _model = _conn.CreateModel();
        }

        public override void Dispose()
        {
            _conn.Close();

        }

        [Fact]
        public void TestBasicAckAfterChannelRecovery()
        {
            var latch = new ManualResetEventSlim(false);
            var cons = new AckingBasicConsumer(_model, latch, CloseAndWaitForRecovery);

            TestDelayedBasicAckNackAfterChannelRecovery(cons, latch);
        }

        [Fact]
        public void TestBasicAckAfterBasicGetAndChannelRecovery()
        {
            string q = GenerateQueueName();
            _model.QueueDeclare(q, false, false, false, null);
            // create an offset
            IBasicProperties bp = _model.CreateBasicProperties();
            _model.BasicPublish("", q, bp, new byte[] { });
            Thread.Sleep(50);
            BasicGetResult g = _model.BasicGet(q, false);
            CloseAndWaitForRecovery();
            Assert.True(_conn.IsOpen);
            Assert.True(_model.IsOpen);
            // ack the message after recovery - this should be out of range and ignored
            _model.BasicAck(g.DeliveryTag, false);
            // do a sync operation to 'check' there is no channel exception 
            _model.BasicGet(q, false);
        }

        [Fact]
        public void TestBasicAckEventHandlerRecovery()
        {
            _model.ConfirmSelect();
            var latch = new ManualResetEventSlim(false);
            ((AutorecoveringModel)_model).BasicAcks += (m, args) => latch.Set();
            ((AutorecoveringModel)_model).BasicNacks += (m, args) => latch.Set();

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.True(_model.IsOpen);

            WithTemporaryNonExclusiveQueue(_model, (m, q) => m.BasicPublish("", q, null, _encoding.GetBytes("")));
            Wait(latch);
        }

        [Fact]
        public void TestBasicConnectionRecovery()
        {
            Assert.True(_conn.IsOpen);
            CloseAndWaitForRecovery();
            Assert.True(_conn.IsOpen);
        }

        [Fact]
        public void TestBasicConnectionRecoveryWithHostnameList()
        {
            using (AutorecoveringConnection c = CreateAutorecoveringConnection(new List<string> { "127.0.0.1", "localhost" }))
            {
                Assert.True(c.IsOpen);
                CloseAndWaitForRecovery(c);
                Assert.True(c.IsOpen);
            }
        }

        [Fact]
        public void TestBasicConnectionRecoveryWithHostnameListAndUnreachableHosts()
        {
            using (AutorecoveringConnection c = CreateAutorecoveringConnection(new List<string> { "191.72.44.22", "127.0.0.1", "localhost" }))
            {
                Assert.True(c.IsOpen);
                CloseAndWaitForRecovery(c);
                Assert.True(c.IsOpen);
            }
        }

        [Fact]
        public void TestBasicConnectionRecoveryWithEndpointList()
        {
            using (AutorecoveringConnection c = CreateAutorecoveringConnection(
                        new List<AmqpTcpEndpoint>
                        {
                            new AmqpTcpEndpoint("127.0.0.1"),
                            new AmqpTcpEndpoint("localhost")
                        }))
            {
                Assert.True(c.IsOpen);
                CloseAndWaitForRecovery(c);
                Assert.True(c.IsOpen);
            }
        }

        [Fact]
        public void TestBasicConnectionRecoveryStopsAfterManualClose()
        {
            Assert.True(_conn.IsOpen);
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
            Assert.False(triedRecoveryAfterClose);
            StartRabbitMQ();
        }

        [Fact]
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
                Assert.True(c.IsOpen);
                CloseAndWaitForRecovery(c);
                Assert.True(c.IsOpen);
            }
        }

        [Fact]
        public void TestBasicConnectionRecoveryOnBrokerRestart()
        {
            Assert.True(_conn.IsOpen);
            RestartServerAndWaitForRecovery();
            Assert.True(_conn.IsOpen);
        }

        [Fact]
        public void TestBasicModelRecovery()
        {
            Assert.True(_model.IsOpen);
            CloseAndWaitForRecovery();
            Assert.True(_model.IsOpen);
        }

        [Fact]
        public void TestBasicModelRecoveryOnServerRestart()
        {
            Assert.True(_model.IsOpen);
            RestartServerAndWaitForRecovery();
            Assert.True(_model.IsOpen);
        }

        [Fact]
        public void TestBasicNackAfterChannelRecovery()
        {
            var latch = new ManualResetEventSlim(false);
            var cons = new NackingBasicConsumer(_model, latch, CloseAndWaitForRecovery);

            TestDelayedBasicAckNackAfterChannelRecovery(cons, latch);
        }

        [Fact]
        public void TestBasicRejectAfterChannelRecovery()
        {
            var latch = new ManualResetEventSlim(false);
            var cons = new RejectingBasicConsumer(_model, latch, CloseAndWaitForRecovery);

            TestDelayedBasicAckNackAfterChannelRecovery(cons, latch);
        }

        [Fact]
        public void TestBlockedListenersRecovery()
        {
            var latch = new ManualResetEventSlim(false);
            _conn.ConnectionBlocked += (c, reason) => latch.Set();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();

            Block();
            Wait(latch);

            Unblock();
        }

        [Fact]
        public void TestClientNamedQueueRecovery()
        {
            string s = "dotnet-client.test.recovery.q1";
            WithTemporaryNonExclusiveQueue(_model, (m, q) =>
            {
                CloseAndWaitForRecovery();
                AssertQueueRecovery(m, q, false);
                _model.QueueDelete(q);
            }, s);
        }

        [Fact]
        public void TestClientNamedQueueRecoveryNoWait()
        {
            string s = "dotnet-client.test.recovery.q1-nowait";
            WithTemporaryQueueNoWait(_model, (m, q) =>
            {
                CloseAndWaitForRecovery();
                AssertQueueRecovery(m, q);
            }, s);
        }

        [Fact]
        public void TestClientNamedQueueRecoveryOnServerRestart()
        {
            string s = "dotnet-client.test.recovery.q1";
            WithTemporaryNonExclusiveQueue(_model, (m, q) =>
            {
                RestartServerAndWaitForRecovery();
                AssertQueueRecovery(m, q, false);
                _model.QueueDelete(q);
            }, s);
        }

        [Fact]
        public void TestConsumerWorkServiceRecovery()
        {
            using (AutorecoveringConnection c = CreateAutorecoveringConnection())
            {
                IModel m = c.CreateModel();
                string q = m.QueueDeclare("dotnet-client.recovery.consumer_work_pool1",
                    false, false, false, null).QueueName;
                var cons = new EventingBasicConsumer(m);
                m.BasicConsume(q, true, cons);
                AssertConsumerCount(m, q, 1);

                CloseAndWaitForRecovery(c);

                Assert.True(m.IsOpen);
                var latch = new ManualResetEventSlim(false);
                cons.Received += (s, args) => latch.Set();

                m.BasicPublish("", q, null, _encoding.GetBytes("msg"));
                Wait(latch);

                m.QueueDelete(q);
            }
        }

        [Fact]
        public void TestConsumerRecoveryOnClientNamedQueueWithOneRecovery()
        {
            string q0 = "dotnet-client.recovery.queue1";
            using (AutorecoveringConnection c = CreateAutorecoveringConnection())
            {
                IModel m = c.CreateModel();
                string q1 = m.QueueDeclare(q0, false, false, false, null).QueueName;
                Assert.Equal(q0, q1);

                var cons = new EventingBasicConsumer(m);
                m.BasicConsume(q1, true, cons);
                AssertConsumerCount(m, q1, 1);

                bool queueNameChangeAfterRecoveryCalled = false;

                c.QueueNameChangeAfterRecovery += (source, ea) => { queueNameChangeAfterRecoveryCalled = true; };

                CloseAndWaitForRecovery(c);
                AssertConsumerCount(m, q1, 1);
                Assert.False(queueNameChangeAfterRecoveryCalled);

                CloseAndWaitForRecovery(c);
                AssertConsumerCount(m, q1, 1);
                Assert.False(queueNameChangeAfterRecoveryCalled);

                CloseAndWaitForRecovery(c);
                AssertConsumerCount(m, q1, 1);
                Assert.False(queueNameChangeAfterRecoveryCalled);

                var latch = new ManualResetEventSlim(false);
                cons.Received += (s, args) => latch.Set();

                m.BasicPublish("", q1, null, _encoding.GetBytes("msg"));
                Wait(latch);

                m.QueueDelete(q1);
            }
        }

        [Fact]
        public void TestConsumerRecoveryOnClientNamedQueueWithDeclarationOnOneChannelAndConsumersOnTheSecondChannel()
        {
            string q0 = "dotnet-client.recovery.queue1";
            using (AutorecoveringConnection c = CreateAutorecoveringConnection())
            {
                using (IModel m = c.CreateModel())
                {
                    string q1 = m.QueueDeclare(q0, false, false, false, null).QueueName;
                    Assert.Equal(q0, q1);
                }
                IModel m1 = c.CreateModel();

                var cons = new EventingBasicConsumer(m1);
                m1.BasicConsume(q0, true, cons);
                AssertConsumerCount(m1, q0, 1);

                bool queueNameChangeAfterRecoveryCalled = false;

                c.QueueNameChangeAfterRecovery += (source, ea) => { queueNameChangeAfterRecoveryCalled = true; };

                CloseAndWaitForRecovery(c);
                AssertConsumerCount(m1, q0, 1);
                Assert.False(queueNameChangeAfterRecoveryCalled);

                CloseAndWaitForRecovery(c);
                AssertConsumerCount(m1, q0, 1);
                Assert.False(queueNameChangeAfterRecoveryCalled);

                CloseAndWaitForRecovery(c);
                AssertConsumerCount(m1, q0, 1);
                Assert.False(queueNameChangeAfterRecoveryCalled);

                var latch = new ManualResetEventSlim(false);
                cons.Received += (s, args) => latch.Set();

                m1.BasicPublish("", q0, null, _encoding.GetBytes("msg"));
                Wait(latch);

                m1.QueueDelete(q0);
            }
        }

        [Fact]
        public void TestConsumerRecoveryWithManyConsumers()
        {
            string q = _model.QueueDeclare(GenerateQueueName(), false, false, false, null).QueueName;
            int n = 1024;

            for (int i = 0; i < n; i++)
            {
                var cons = new EventingBasicConsumer(_model);
                _model.BasicConsume(q, true, cons);
            }

            var latch = new ManualResetEventSlim(false);
            ((AutorecoveringConnection)_conn).ConsumerTagChangeAfterRecovery += (prev, current) => latch.Set();

            CloseAndWaitForRecovery();
            Wait(latch);
            Assert.True(_model.IsOpen);
            AssertConsumerCount(q, n);
        }

        [Fact]
        public void TestCreateModelOnClosedAutorecoveringConnectionDoesNotHang()
        {
            // we don't want this to recover quickly in this test
            AutorecoveringConnection c = CreateAutorecoveringConnection(TimeSpan.FromSeconds(20));

            try
            {
                c.Close();
                WaitForShutdown(c);
                Assert.False(c.IsOpen);
                c.CreateModel();
                Assert.True(false, "Expected an exception");
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

        [Fact]
        public void TestDeclarationOfManyAutoDeleteExchangesWithTransientExchangesThatAreDeleted()
        {
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 3; i++)
            {
                string x1 = $"source-{Guid.NewGuid()}";
                _model.ExchangeDeclare(x1, "fanout", false, true, null);
                string x2 = $"destination-{Guid.NewGuid()}";
                _model.ExchangeDeclare(x2, "fanout", false, false, null);
                _model.ExchangeBind(x2, x1, "");
                _model.ExchangeDelete(x2);
            }
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
        }

        [Fact]
        public void TestDeclarationOfManyAutoDeleteExchangesWithTransientExchangesThatAreUnbound()
        {
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string x1 = $"source-{Guid.NewGuid()}";
                _model.ExchangeDeclare(x1, "fanout", false, true, null);
                string x2 = $"destination-{Guid.NewGuid()}";
                _model.ExchangeDeclare(x2, "fanout", false, false, null);
                _model.ExchangeBind(x2, x1, "");
                _model.ExchangeUnbind(x2, x1, "");
                _model.ExchangeDelete(x2);
            }
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
        }

        [Fact]
        public void TestDeclarationOfManyAutoDeleteExchangesWithTransientQueuesThatAreDeleted()
        {
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string x = Guid.NewGuid().ToString();
                _model.ExchangeDeclare(x, "fanout", false, true, null);
                QueueDeclareOk q = _model.QueueDeclare();
                _model.QueueBind(q, x, "");
                _model.QueueDelete(q);
            }
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
        }

        [Fact]
        public void TestDeclarationOfManyAutoDeleteExchangesWithTransientQueuesThatAreUnbound()
        {
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string x = Guid.NewGuid().ToString();
                _model.ExchangeDeclare(x, "fanout", false, true, null);
                QueueDeclareOk q = _model.QueueDeclare();
                _model.QueueBind(q, x, "");
                _model.QueueUnbind(q, x, "", null);
            }
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
        }

        [Fact]
        public void TestDeclarationOfManyAutoDeleteQueuesWithTransientConsumer()
        {
            AssertRecordedQueues((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string q = Guid.NewGuid().ToString();
                _model.QueueDeclare(q, false, false, true, null);
                var dummy = new EventingBasicConsumer(_model);
                string tag = _model.BasicConsume(q, true, dummy);
                _model.BasicCancel(tag);
            }
            AssertRecordedQueues((AutorecoveringConnection)_conn, 0);
        }

        [Fact]
        public void TestExchangeRecovery()
        {
            string x = "dotnet-client.test.recovery.x1";
            DeclareNonDurableExchange(_model, x);
            CloseAndWaitForRecovery();
            AssertExchangeRecovery(_model, x);
            _model.ExchangeDelete(x);
        }

        [Fact]
        public void TestExchangeRecoveryWithNoWait()
        {
            string x = "dotnet-client.test.recovery.x1-nowait";
            DeclareNonDurableExchangeNoWait(_model, x);
            CloseAndWaitForRecovery();
            AssertExchangeRecovery(_model, x);
            _model.ExchangeDelete(x);
        }

        [Fact]
        public void TestExchangeToExchangeBindingRecovery()
        {
            string q = _model.QueueDeclare("", false, false, false, null).QueueName;
            string x1 = "amq.fanout";
            string x2 = GenerateExchangeName();

            _model.ExchangeDeclare(x2, "fanout");
            _model.ExchangeBind(x1, x2, "");
            _model.QueueBind(q, x1, "");

            try
            {
                CloseAndWaitForRecovery();
                Assert.True(_model.IsOpen);
                _model.BasicPublish(x2, "", null, _encoding.GetBytes("msg"));
                AssertMessageCount(q, 1);
            }
            finally
            {
                WithTemporaryModel(m =>
                {
                    m.ExchangeDelete(x2);
                    m.QueueDelete(q);
                });
            }
        }

        [Fact]
        public void TestQueueRecoveryWithManyQueues()
        {
            var qs = new List<string>();
            int n = 1024;
            for (int i = 0; i < n; i++)
            {
                qs.Add(_model.QueueDeclare(GenerateQueueName(), false, false, false, null).QueueName);
            }
            CloseAndWaitForRecovery();
            Assert.True(_model.IsOpen);
            foreach (string q in qs)
            {
                AssertQueueRecovery(_model, q, false);
                _model.QueueDelete(q);
            }
        }

        // rabbitmq/rabbitmq-dotnet-client#43
        [Fact]
        public void TestClientNamedTransientAutoDeleteQueueAndBindingRecovery()
        {
            string q = Guid.NewGuid().ToString();
            string x = "tmp-fanout";
            IModel ch = _conn.CreateModel();
            ch.QueueDelete(q);
            ch.ExchangeDelete(x);
            ch.ExchangeDeclare(exchange: x, type: "fanout");
            ch.QueueDeclare(queue: q, durable: false, exclusive: false, autoDelete: true, arguments: null);
            ch.QueueBind(queue: q, exchange: x, routingKey: "");
            RestartServerAndWaitForRecovery();
            Assert.True(ch.IsOpen);
            ch.ConfirmSelect();
            ch.QueuePurge(q);
            ch.ExchangeDeclare(exchange: x, type: "fanout");
            ch.BasicPublish(exchange: x, routingKey: "", basicProperties: null, body: _encoding.GetBytes("msg"));
            WaitForConfirms(ch);
            QueueDeclareOk ok = ch.QueueDeclare(queue: q, durable: false, exclusive: false, autoDelete: true, arguments: null);
            Assert.Equal(1u, ok.MessageCount);
            ch.QueueDelete(q);
            ch.ExchangeDelete(x);
        }

        // rabbitmq/rabbitmq-dotnet-client#43
        [Fact]
        public void TestServerNamedTransientAutoDeleteQueueAndBindingRecovery()
        {
            string x = "tmp-fanout";
            IModel ch = _conn.CreateModel();
            ch.ExchangeDelete(x);
            ch.ExchangeDeclare(exchange: x, type: "fanout");
            string q = ch.QueueDeclare(queue: "", durable: false, exclusive: false, autoDelete: true, arguments: null).QueueName;
            string nameBefore = q;
            string nameAfter = null;
            var latch = new ManualResetEventSlim(false);
            ((AutorecoveringConnection)_conn).QueueNameChangeAfterRecovery += (source, ea) =>
            {
                nameBefore = ea.NameBefore;
                nameAfter = ea.NameAfter;
                latch.Set();
            };
            ch.QueueBind(queue: nameBefore, exchange: x, routingKey: "");
            RestartServerAndWaitForRecovery();
            Wait(latch);
            Assert.True(ch.IsOpen);
            Assert.NotEqual(nameBefore, nameAfter);
            ch.ConfirmSelect();
            ch.ExchangeDeclare(exchange: x, type: "fanout");
            ch.BasicPublish(exchange: x, routingKey: "", basicProperties: null, body: _encoding.GetBytes("msg"));
            WaitForConfirms(ch);
            QueueDeclareOk ok = ch.QueueDeclarePassive(nameAfter);
            Assert.Equal(1u, ok.MessageCount);
            ch.QueueDelete(q);
            ch.ExchangeDelete(x);
        }

        [Fact]
        public void TestRecoveryEventHandlersOnChannel()
        {
            int counter = 0;
            ((AutorecoveringModel)_model).Recovery += (source, ea) => Interlocked.Increment(ref counter);

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.True(_conn.IsOpen);

            Assert.True(counter >= 1);
        }

        [Fact]
        public void TestRecoveryEventHandlersOnConnection()
        {
            int counter = 0;
            ((AutorecoveringConnection)_conn).RecoverySucceeded += (source, ea) => Interlocked.Increment(ref counter);

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.True(_conn.IsOpen);

            Assert.True(counter >= 3);
        }

        [Fact]
        public void TestRecoveryEventHandlersOnModel()
        {
            int counter = 0;
            ((AutorecoveringModel)_model).Recovery += (source, ea) => Interlocked.Increment(ref counter);

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.True(_model.IsOpen);

            Assert.True(counter >= 3);
        }

        [Fact]
        public void TestRecoveryWithTopologyDisabled()
        {
            AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryDisabled();
            IModel ch = conn.CreateModel();
            string s = "dotnet-client.test.recovery.q2";
            ch.QueueDelete(s);
            ch.QueueDeclare(s, false, true, false, null);
            ch.QueueDeclarePassive(s);
            Assert.True(ch.IsOpen);

            try
            {
                CloseAndWaitForRecovery(conn);
                Assert.True(ch.IsOpen);
                ch.QueueDeclarePassive(s);
                Assert.True(false, "Expected an exception");
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

        [Fact]
        public void TestServerNamedQueueRecovery()
        {
            string q = _model.QueueDeclare("", false, false, false, null).QueueName;
            string x = "amq.fanout";
            _model.QueueBind(q, x, "");

            string nameBefore = q;
            string nameAfter = null;

            var latch = new ManualResetEventSlim(false);
            var connection = (AutorecoveringConnection)_conn;
            connection.RecoverySucceeded += (source, ea) => latch.Set();
            connection.QueueNameChangeAfterRecovery += (source, ea) => { nameAfter = ea.NameAfter; };

            CloseAndWaitForRecovery();
            Wait(latch);

            Assert.NotNull(nameAfter);
            Assert.StartsWith("amq.", nameBefore);
            Assert.StartsWith("amq.", nameAfter);
            Assert.NotEqual(nameBefore, nameAfter);

            _model.QueueDeclarePassive(nameAfter);
        }

        [Fact]
        public void TestShutdownEventHandlersRecoveryOnConnection()
        {
            int counter = 0;
            _conn.ConnectionShutdown += (c, args) => Interlocked.Increment(ref counter);

            Assert.True(_conn.IsOpen);
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.True(_conn.IsOpen);

            Assert.True(counter >= 3);
        }

        [Fact]
        public void TestShutdownEventHandlersRecoveryOnConnectionAfterDelayedServerRestart()
        {
            int counter = 0;
            _conn.ConnectionShutdown += (c, args) => Interlocked.Increment(ref counter);
            ManualResetEventSlim shutdownLatch = PrepareForShutdown(_conn);
            ManualResetEventSlim recoveryLatch = PrepareForRecovery((AutorecoveringConnection)_conn);

            Assert.True(_conn.IsOpen);
            StopRabbitMQ();
            Console.WriteLine("Stopped RabbitMQ. About to sleep for multiple recovery intervals...");
            Thread.Sleep(7000);
            StartRabbitMQ();
            Wait(shutdownLatch, TimeSpan.FromSeconds(30));
            Wait(recoveryLatch, TimeSpan.FromSeconds(30));
            Assert.True(_conn.IsOpen);

            Assert.True(counter >= 1);
        }

        [Fact]
        public void TestShutdownEventHandlersRecoveryOnModel()
        {
            int counter = 0;
            _model.ModelShutdown += (c, args) => Interlocked.Increment(ref counter);

            Assert.True(_model.IsOpen);
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.True(_model.IsOpen);

            Assert.True(counter >= 3);
        }

        [Fact]
        public void TestThatCancelledConsumerDoesNotReappearOnRecovery()
        {
            string q = _model.QueueDeclare(GenerateQueueName(), false, false, false, null).QueueName;
            int n = 1024;

            for (int i = 0; i < n; i++)
            {
                var cons = new EventingBasicConsumer(_model);
                string tag = _model.BasicConsume(q, true, cons);
                _model.BasicCancel(tag);
            }
            CloseAndWaitForRecovery();
            Assert.True(_model.IsOpen);
            AssertConsumerCount(q, 0);
        }

        [Fact]
        public void TestThatDeletedExchangeBindingsDontReappearOnRecovery()
        {
            string q = _model.QueueDeclare("", false, false, false, null).QueueName;
            string x1 = "amq.fanout";
            string x2 = GenerateExchangeName();

            _model.ExchangeDeclare(x2, "fanout");
            _model.ExchangeBind(x1, x2, "");
            _model.QueueBind(q, x1, "");
            _model.ExchangeUnbind(x1, x2, "", null);

            try
            {
                CloseAndWaitForRecovery();
                Assert.True(_model.IsOpen);
                _model.BasicPublish(x2, "", null, _encoding.GetBytes("msg"));
                AssertMessageCount(q, 0);
            }
            finally
            {
                WithTemporaryModel(m =>
                {
                    m.ExchangeDelete(x2);
                    m.QueueDelete(q);
                });
            }
        }

        [Fact]
        public void TestThatDeletedExchangesDontReappearOnRecovery()
        {
            string x = GenerateExchangeName();
            _model.ExchangeDeclare(x, "fanout");
            _model.ExchangeDelete(x);

            try
            {
                CloseAndWaitForRecovery();
                Assert.True(_model.IsOpen);
                _model.ExchangeDeclarePassive(x);
                Assert.True(false, "Expected an exception");
            }
            catch (OperationInterruptedException e)
            {
                // expected
                AssertShutdownError(e.ShutdownReason, 404);
            }
        }

        [Fact]
        public void TestThatDeletedQueueBindingsDontReappearOnRecovery()
        {
            string q = _model.QueueDeclare("", false, false, false, null).QueueName;
            string x1 = "amq.fanout";
            string x2 = GenerateExchangeName();

            _model.ExchangeDeclare(x2, "fanout");
            _model.ExchangeBind(x1, x2, "");
            _model.QueueBind(q, x1, "");
            _model.QueueUnbind(q, x1, "", null);

            try
            {
                CloseAndWaitForRecovery();
                Assert.True(_model.IsOpen);
                _model.BasicPublish(x2, "", null, _encoding.GetBytes("msg"));
                AssertMessageCount(q, 0);
            }
            finally
            {
                WithTemporaryModel(m =>
                {
                    m.ExchangeDelete(x2);
                    m.QueueDelete(q);
                });
            }
        }

        [Fact]
        public void TestThatDeletedQueuesDontReappearOnRecovery()
        {
            string q = "dotnet-client.recovery.q1";
            _model.QueueDeclare(q, false, false, false, null);
            _model.QueueDelete(q);

            try
            {
                CloseAndWaitForRecovery();
                Assert.True(_model.IsOpen);
                _model.QueueDeclarePassive(q);
                Assert.True(false, "Expected an exception");
            }
            catch (OperationInterruptedException e)
            {
                // expected
                AssertShutdownError(e.ShutdownReason, 404);
            }
        }

        [Fact]
        public void TestUnblockedListenersRecovery()
        {
            var latch = new ManualResetEventSlim(false);
            _conn.ConnectionUnblocked += (source, ea) => latch.Set();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();

            Block();
            Unblock();
            Wait(latch);
        }

        internal void AssertExchangeRecovery(IModel m, string x)
        {
            m.ConfirmSelect();
            WithTemporaryNonExclusiveQueue(m, (_, q) =>
            {
                string rk = "routing-key";
                m.QueueBind(q, x, rk);
                byte[] mb = RandomMessageBody();
                m.BasicPublish(x, rk, null, mb);

                Assert.True(WaitForConfirms(m));
                m.ExchangeDeclarePassive(x);
            });
        }

        internal void AssertQueueRecovery(IModel m, string q)
        {
            AssertQueueRecovery(m, q, true);
        }

        internal void AssertQueueRecovery(IModel m, string q, bool exclusive)
        {
            m.ConfirmSelect();
            m.QueueDeclarePassive(q);
            QueueDeclareOk ok1 = m.QueueDeclare(q, false, exclusive, false, null);
            Assert.Equal(0u, ok1.MessageCount);
            m.BasicPublish("", q, null, _encoding.GetBytes(""));
            Assert.True(WaitForConfirms(m));
            QueueDeclareOk ok2 = m.QueueDeclare(q, false, exclusive, false, null);
            Assert.Equal(1u, ok2.MessageCount);
        }

        internal void AssertRecordedExchanges(AutorecoveringConnection c, int n)
        {
            Assert.Equal(n, c.RecordedExchangesCount);
        }

        internal void AssertRecordedQueues(AutorecoveringConnection c, int n)
        {
            Assert.Equal(n, c.RecordedQueuesCount);
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

        internal void TestDelayedBasicAckNackAfterChannelRecovery(TestBasicConsumer1 cons, ManualResetEventSlim latch)
        {
            string q = _model.QueueDeclare(GenerateQueueName(), false, false, false, null).QueueName;
            int n = 30;
            _model.BasicQos(0, 1, false);
            _model.BasicConsume(q, false, cons);

            AutorecoveringConnection publishingConn = CreateAutorecoveringConnection();
            IModel publishingModel = publishingConn.CreateModel();

            for (int i = 0; i < n; i++)
            {
                publishingModel.BasicPublish("", q, null, _encoding.GetBytes(""));
            }

            Wait(latch, TimeSpan.FromSeconds(20));
            _model.QueueDelete(q);
            publishingModel.Close();
            publishingConn.Close();
        }

        internal void WaitForShutdown(IConnection conn)
        {
            Wait(PrepareForShutdown(conn));
        }

        public class AckingBasicConsumer : TestBasicConsumer1
        {
            public AckingBasicConsumer(IModel model, ManualResetEventSlim latch, Action fn)
                : base(model, latch, fn)
            {
            }

            public override void PostHandleDelivery(ulong deliveryTag)
            {
                Model.BasicAck(deliveryTag, false);
            }
        }

        public class NackingBasicConsumer : TestBasicConsumer1
        {
            public NackingBasicConsumer(IModel model, ManualResetEventSlim latch, Action fn)
                : base(model, latch, fn)
            {
            }

            public override void PostHandleDelivery(ulong deliveryTag)
            {
                Model.BasicNack(deliveryTag, false, false);
            }
        }

        public class RejectingBasicConsumer : TestBasicConsumer1
        {
            public RejectingBasicConsumer(IModel model, ManualResetEventSlim latch, Action fn)
                : base(model, latch, fn)
            {
            }

            public override void PostHandleDelivery(ulong deliveryTag)
            {
                Model.BasicReject(deliveryTag, false);
            }
        }

        public class TestBasicConsumer1 : DefaultBasicConsumer
        {
            private readonly Action _action;
            private readonly ManualResetEventSlim _latch;
            private ushort _counter = 0;

            public TestBasicConsumer1(IModel model, ManualResetEventSlim latch, Action fn)
                : base(model)
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
