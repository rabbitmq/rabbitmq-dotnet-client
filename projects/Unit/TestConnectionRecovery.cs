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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;

using Xunit;
using Xunit.Abstractions;

#pragma warning disable 0618

namespace RabbitMQ.Client.Unit
{
    public class TestConnectionRecovery : IntegrationFixture
    {
        private readonly byte[] _messageBody;
        private readonly ushort _totalMessageCount = 1024;
        private readonly ushort _closeAtCount = 16;
        private string _queueName;

        public TestConnectionRecovery(ITestOutputHelper output) : base(output)
        {
            var rnd = new Random();
            _messageBody = new byte[4096];
            rnd.NextBytes(_messageBody);
        }

        protected override void SetUp()
        {
            _queueName = $"TestConnectionRecovery-queue-{Guid.NewGuid()}";
            _conn = CreateAutorecoveringConnection();
            _model = _conn.CreateModel();
            _model.QueueDelete(_queueName);
        }

        protected override void ReleaseResources()
        {
            // TODO LRB not really necessary
            if (_model.IsOpen)
            {
                _model.Close();
            }

            if (_conn.IsOpen)
            {
                _conn.Close();
            }

            //Unblock();
        }

        [Fact]
        public void TestBasicAckAfterChannelRecovery()
        {
            var allMessagesSeenLatch = new ManualResetEventSlim(false);
            var cons = new AckingBasicConsumer(_model, _totalMessageCount, allMessagesSeenLatch);

            string queueName = _model.QueueDeclare(_queueName, false, false, false, null).QueueName;
            Assert.Equal(queueName, _queueName);

            _model.BasicQos(0, 1, false);
            string consumerTag = _model.BasicConsume(queueName, false, cons);

            CountdownEvent countdownEvent = new CountdownEvent(2);
            PrepareForShutdown(_conn, countdownEvent, _output);
            PrepareForRecovery(_conn, countdownEvent, _output);

            PublishMessagesWhileClosingConn(queueName);

            Wait(countdownEvent);
            Wait(allMessagesSeenLatch);
        }

        [Fact]
        public void TestBasicNackAfterChannelRecovery()
        {
            var allMessagesSeenLatch = new ManualResetEventSlim(false);
            var cons = new NackingBasicConsumer(_model, _totalMessageCount, allMessagesSeenLatch);

            string queueName = _model.QueueDeclare(_queueName, false, false, false, null).QueueName;
            Assert.Equal(queueName, _queueName);

            _model.BasicQos(0, 1, false);
            string consumerTag = _model.BasicConsume(queueName, false, cons);

            CountdownEvent countdownEvent = new CountdownEvent(2);
            PrepareForShutdown(_conn, countdownEvent, _output);
            PrepareForRecovery(_conn, countdownEvent, _output);

            PublishMessagesWhileClosingConn(queueName);

            Wait(countdownEvent);
            Wait(allMessagesSeenLatch);
        }

        [Fact]
        public void TestBasicRejectAfterChannelRecovery()
        {
            var allMessagesSeenLatch = new ManualResetEventSlim(false);
            var cons = new RejectingBasicConsumer(_model, _totalMessageCount, allMessagesSeenLatch);

            string queueName = _model.QueueDeclare(_queueName, false, false, false, null).QueueName;
            Assert.Equal(queueName, _queueName);

            _model.BasicQos(0, 1, false);
            string consumerTag = _model.BasicConsume(queueName, false, cons);

            CountdownEvent countdownEvent = new CountdownEvent(2);
            PrepareForShutdown(_conn, countdownEvent, _output);
            PrepareForRecovery(_conn, countdownEvent, _output);

            PublishMessagesWhileClosingConn(queueName);

            
            Wait(allMessagesSeenLatch);
        }

        [Fact]
        public async Task TestBasicAckAfterBasicGetAndChannelRecovery()
        {
            string q = GenerateQueueName();
            _model.QueueDeclare(q, false, false, false, null);
            // create an offset
            _model.BasicPublish("", q, _messageBody);
            await Task.Delay(50);
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
        public async Task TestBasicAckEventHandlerRecovery()
        {
            _model.ConfirmSelect();
            var latch = new ManualResetEventSlim(false);
            ((AutorecoveringModel)_model).BasicAcks += (m, args) => latch.Set();
            ((AutorecoveringModel)_model).BasicNacks += (m, args) => latch.Set();

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.True(_model.IsOpen);

            await WithTemporaryNonExclusiveQueueAsync(_model, (m, q) =>
            {
                m.BasicPublish("", q, _messageBody);
                return Task.CompletedTask;
            });
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
        public async Task TestBasicConnectionRecoveryStopsAfterManualClose()
        {
            Assert.True(_conn.IsOpen);
            AutorecoveringConnection c = CreateAutorecoveringConnection();
            var latch = new AutoResetEvent(false);
            c.ConnectionRecoveryError += (o, args) => latch.Set();

            try
            {
                StopRabbitMQ();
                latch.WaitOne(30000); // we got the failed reconnection event.
                bool triedRecoveryAfterClose = false;
                c.Close();
                await Task.Delay(5000);
                c.ConnectionRecoveryError += (o, args) => triedRecoveryAfterClose = true;
                await Task.Delay(10000);
                Assert.False(triedRecoveryAfterClose);
            }
            finally
            {
                StartRabbitMQ();
            }
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
        public async Task TestBasicConnectionRecoveryOnBrokerRestart()
        {
            Assert.True(_conn.IsOpen);
            await RestartServerAndWaitForRecoveryAsync();
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
        public async Task TestBasicModelRecoveryOnServerRestart()
        {
            Assert.True(_model.IsOpen);
            await RestartServerAndWaitForRecoveryAsync();
            Assert.True(_model.IsOpen);
        }

        [Fact]
        public async Task TestBlockedListenersRecovery()
        {
            var latch = new ManualResetEventSlim(false);
            _conn.ConnectionBlocked += (c, reason) => latch.Set();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();

            await BlockAsync();
            Wait(latch);

            Unblock();
        }

        [Fact]
        public async Task TestClientNamedQueueRecovery()
        {
            string s = "dotnet-client.test.recovery.q1";
            await WithTemporaryNonExclusiveQueueAsync(_model, (m, q) =>
            {
                CloseAndWaitForRecovery();
                AssertQueueRecovery(m, q, false);
                _model.QueueDelete(q);
                return Task.CompletedTask;
            }, s);
        }

        [Fact]
        public async Task TestClientNamedQueueRecoveryNoWait()
        {
            string s = "dotnet-client.test.recovery.q1-nowait";
            await WithTemporaryQueueNoWaitAsync(_model, (m, q) =>
            {
                CloseAndWaitForRecovery();
                AssertQueueRecovery(m, q);
                return Task.CompletedTask;
            }, s);
        }

        [Fact]
        public async Task TestClientNamedQueueRecoveryOnServerRestart()
        {
            string s = "dotnet-client.test.recovery.q1";
            await WithTemporaryNonExclusiveQueueAsync(_model, async (m, q) =>
            {
                await RestartServerAndWaitForRecoveryAsync();
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

                m.BasicPublish("", q, _encoding.GetBytes("msg"));
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

                m.BasicPublish("", q1, _encoding.GetBytes("msg"));
                Wait(latch);

                m.QueueDelete(q1);
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
                WaitForShutdown(c, _output);
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
        public async Task TestExchangeRecovery()
        {
            string x = DeclareNonDurableExchange(_model);
            CloseAndWaitForRecovery();
            await AssertExchangeRecoveryAsync(_model, x);
            _model.ExchangeDelete(x);
        }

        [Fact]
        public async Task TestExchangeRecoveryWithNoWait()
        {
            string x = DeclareNonDurableExchangeNoWait(_model);
            CloseAndWaitForRecovery();
            await AssertExchangeRecoveryAsync(_model, x);
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
                _model.BasicPublish(x2, "", _encoding.GetBytes("msg"));
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
        public async Task TestClientNamedTransientAutoDeleteQueueAndBindingRecovery()
        {
            string q = Guid.NewGuid().ToString();
            string x = "tmp-fanout";
            IModel ch = _conn.CreateModel();
            ch.QueueDelete(q);
            ch.ExchangeDelete(x);
            ch.ExchangeDeclare(exchange: x, type: "fanout");
            ch.QueueDeclare(queue: q, durable: false, exclusive: false, autoDelete: true, arguments: null);
            ch.QueueBind(queue: q, exchange: x, routingKey: "");
            await RestartServerAndWaitForRecoveryAsync();
            Assert.True(ch.IsOpen);
            ch.ConfirmSelect();
            ch.QueuePurge(q);
            ch.ExchangeDeclare(exchange: x, type: "fanout");
            ch.BasicPublish(exchange: x, routingKey: "", body: _encoding.GetBytes("msg"));
            WaitForConfirms(ch);
            QueueDeclareOk ok = ch.QueueDeclare(queue: q, durable: false, exclusive: false, autoDelete: true, arguments: null);
            Assert.Equal(1u, ok.MessageCount);
            ch.QueueDelete(q);
            ch.ExchangeDelete(x);
        }

        // rabbitmq/rabbitmq-dotnet-client#43
        [Fact]
        public async Task TestServerNamedTransientAutoDeleteQueueAndBindingRecovery()
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
            await RestartServerAndWaitForRecoveryAsync();
            Wait(latch);
            Assert.True(ch.IsOpen);
            Assert.NotEqual(nameBefore, nameAfter);
            ch.ConfirmSelect();
            ch.ExchangeDeclare(exchange: x, type: "fanout");
            ch.BasicPublish(exchange: x, routingKey: "", body: _encoding.GetBytes("msg"));
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
        public async Task TestShutdownEventHandlersRecoveryOnConnectionAfterDelayedServerRestart()
        {
            int counter = 0;
            _conn.ConnectionShutdown += (c, args) => Interlocked.Increment(ref counter);
            CountdownEvent countdownEvent = new CountdownEvent(2);
            PrepareForShutdown(_conn, countdownEvent, _output);
            PrepareForRecovery((AutorecoveringConnection)_conn, countdownEvent, _output);

            Assert.True(_conn.IsOpen);

            try
            {
                StopRabbitMQ();
                _output.WriteLine("Stopped RabbitMQ. About to sleep for multiple recovery intervals...");
                await Task.Delay(7000);
            }
            finally
            {
                StartRabbitMQ();
            }

            Wait(countdownEvent, TimeSpan.FromSeconds(60));
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
        public void TestRecoverTopologyOnDisposedChannel()
        {
            string x = GenerateExchangeName();
            string q = GenerateQueueName();
            const string rk = "routing-key";

            using (IModel m = _conn.CreateModel())
            {
                m.ExchangeDeclare(exchange: x, type: "fanout");
                m.QueueDeclare(q, false, false, false, null);
                m.QueueBind(q, x, rk);
            }

            var cons = new EventingBasicConsumer(_model);
            _model.BasicConsume(q, true, cons);
            AssertConsumerCount(_model, q, 1);

            CloseAndWaitForRecovery();
            AssertConsumerCount(_model, q, 1);

            var latch = new ManualResetEventSlim(false);
            cons.Received += (s, args) => latch.Set();

            _model.BasicPublish("", q, _messageBody);
            Wait(latch);

            _model.QueueUnbind(q, x, rk);
            _model.ExchangeDelete(x);
            _model.QueueDelete(q);
        }

        [Fact(Skip = "TODO-FLAKY")]
        public void TestPublishRpcRightAfterReconnect()
        {
            string testQueueName = $"dotnet-client.test.{nameof(TestPublishRpcRightAfterReconnect)}";
            _model.QueueDeclare(testQueueName, false, false, false, null);
            var replyConsumer = new EventingBasicConsumer(_model);
            _model.BasicConsume("amq.rabbitmq.reply-to", true, replyConsumer);
            var properties = new BasicProperties();
            properties.ReplyTo = "amq.rabbitmq.reply-to";

            TimeSpan doneSpan = TimeSpan.FromMilliseconds(100);
            var done = new ManualResetEventSlim(false);
            Task.Run(() =>
            {
                try
                {

                    CloseAndWaitForRecovery();
                }
                finally
                {
                    done.Set();
                }
            });

            while (!done.IsSet)
            {
                try
                {
                    _model.BasicPublish(string.Empty, testQueueName, ref properties, _messageBody);
                }
                catch (Exception e)
                {
                    if (e is AlreadyClosedException a)
                    {
                        // 406 is received, when the reply consumer isn't yet recovered
                        Assert.NotEqual(406, a.ShutdownReason.ReplyCode);
                    }
                }
                done.Wait(doneSpan);
            }
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
                _model.BasicPublish(x2, "", _encoding.GetBytes("msg"));
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
                _model.BasicPublish(x2, "", _encoding.GetBytes("msg"));
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
        public async Task TestUnblockedListenersRecovery()
        {
            var latch = new ManualResetEventSlim(false);
            _conn.ConnectionUnblocked += (source, ea) => latch.Set();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();

            await BlockAsync();
            Unblock();
            Wait(latch);
        }

        internal Task AssertExchangeRecoveryAsync(IModel m, string x)
        {
            m.ConfirmSelect();
            return WithTemporaryNonExclusiveQueueAsync(m, (_, q) =>
            {
                string rk = "routing-key";
                m.QueueBind(q, x, rk);
                m.BasicPublish(x, rk, _messageBody);

                Assert.True(WaitForConfirms(m));
                m.ExchangeDeclarePassive(x);
                return Task.CompletedTask;
            });
        }

        internal void AssertQueueRecovery(IModel m, string q, bool exclusive = true)
        {
            m.ConfirmSelect();
            m.QueueDeclarePassive(q);
            QueueDeclareOk ok1 = m.QueueDeclare(q, false, exclusive, false, null);
            Assert.Equal(0u, ok1.MessageCount);
            m.BasicPublish("", q, _messageBody);
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
            Stopwatch timer = Stopwatch.StartNew();
            CountdownEvent countdownEvent = new CountdownEvent(2);
            PrepareForShutdown(conn, countdownEvent, _output);
            PrepareForRecovery(conn, countdownEvent, _output);
            CloseConnection(conn);
            Wait(countdownEvent);
            _output.WriteLine($"Shutdown and recovered RabbitMQ in {timer.ElapsedMilliseconds}ms");
        }

        internal static void PrepareForRecovery(IConnection conn, CountdownEvent countdownEvent, ITestOutputHelper testOutputHelper)
        {
            AutorecoveringConnection aconn = conn as AutorecoveringConnection;
            aconn.RecoverySucceeded += (source, ea) =>
            {
                testOutputHelper.WriteLine("Received recovery succeeded event.");
                countdownEvent.Signal();
            };
        }

        internal static void PrepareForShutdown(IConnection conn, CountdownEvent countdownEvent, ITestOutputHelper testOutputHelper)
        {
            AutorecoveringConnection aconn = conn as AutorecoveringConnection;
            aconn.ConnectionShutdown += (c, args) =>
            {
                testOutputHelper.WriteLine("Received connection shutdown event.");
                countdownEvent.Signal();
            };
        }

        internal async Task RestartServerAndWaitForRecoveryAsync()
        {
            CountdownEvent countdownEvent = new CountdownEvent(2);
            AutorecoveringConnection conn = (AutorecoveringConnection)_conn;
            PrepareForShutdown(conn, countdownEvent, _output);
            PrepareForRecovery(conn, countdownEvent, _output);
            await RestartRabbitMQAsync();
            Wait(countdownEvent);
        }

        internal void WaitForShutdown(IConnection conn, ITestOutputHelper testOutputHelper)
        {
            CountdownEvent countdownEvent = new CountdownEvent(1);
            PrepareForShutdown(conn, countdownEvent, testOutputHelper);
            Wait(countdownEvent);
        }

        internal void PublishMessagesWhileClosingConn(string queueName)
        {
            using (AutorecoveringConnection publishingConn = CreateAutorecoveringConnection())
            {
                using (IModel publishingModel = publishingConn.CreateModel())
                {
                    for (ushort i = 0; i < _totalMessageCount; i++)
                    {
                        if (i == _closeAtCount)
                        {
                            CloseConnection(_conn);
                        }
                        publishingModel.BasicPublish(string.Empty, queueName, _messageBody);
                    }
                }
            }
        }

        public class AckingBasicConsumer : TestBasicConsumer
        {
            public AckingBasicConsumer(IModel model, ushort totalMessageCount, ManualResetEventSlim allMessagesSeenLatch)
                : base(model, totalMessageCount, allMessagesSeenLatch)
            {
            }

            public override void PostHandleDelivery(ulong deliveryTag)
            {
                Model.BasicAck(deliveryTag, false);
            }
        }

        public class NackingBasicConsumer : TestBasicConsumer
        {
            public NackingBasicConsumer(IModel model, ushort totalMessageCount, ManualResetEventSlim allMessagesSeenLatch)
                : base(model, totalMessageCount, allMessagesSeenLatch)
            {
            }

            public override void PostHandleDelivery(ulong deliveryTag)
            {
                Model.BasicNack(deliveryTag, false, false);
            }
        }

        public class RejectingBasicConsumer : TestBasicConsumer
        {
            public RejectingBasicConsumer(IModel model, ushort totalMessageCount, ManualResetEventSlim allMessagesSeenLatch)
                : base(model, totalMessageCount, allMessagesSeenLatch)
            {
            }

            public override void PostHandleDelivery(ulong deliveryTag)
            {
                Model.BasicReject(deliveryTag, false);
            }
        }

        public abstract class TestBasicConsumer : DefaultBasicConsumer
        {
            private readonly ManualResetEventSlim _allMessagesSeenLatch;
            private readonly ushort _totalMessageCount;
            private int _counter = 0;

            public TestBasicConsumer(IModel model, ushort totalMessageCount, ManualResetEventSlim allMessagesSeenLatch)
                : base(model)
            {
                _totalMessageCount = totalMessageCount;
                _allMessagesSeenLatch = allMessagesSeenLatch;
            }

            public override void HandleBasicDeliver(string consumerTag,
                ulong deliveryTag,
                bool redelivered,
                string exchange,
                string routingKey,
                in ReadOnlyBasicProperties properties,
                ReadOnlyMemory<byte> body)
            {
                try
                {
                    PostHandleDelivery(deliveryTag);
                }
                finally
                {
                    if (Interlocked.Increment(ref _counter) == _totalMessageCount)
                    {
                        _allMessagesSeenLatch.Set();
                    }
                }
            }

            public abstract void PostHandleDelivery(ulong deliveryTag);
        }
    }
}

#pragma warning restore 0168
