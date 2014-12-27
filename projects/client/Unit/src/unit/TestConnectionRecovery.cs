// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2014 GoPivotal, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
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
//  at http://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;

using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

#pragma warning disable 0168
namespace RabbitMQ.Client.Unit {
    [TestFixture]
    public class TestConnectionRecovery : IntegrationFixture {

        public static TimeSpan RECOVERY_INTERVAL = TimeSpan.FromSeconds(2);

        [SetUp]
        public override void Init()
        {
            Conn  = CreateAutorecoveringConnection();
            Model = Conn.CreateModel();
        }

        [Test]
        public void TestBasicConnectionRecovery()
        {
            Assert.IsTrue(Conn.IsOpen);
            CloseAndWaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);
        }

        [Test]
        public void TestBasicConnectionRecoveryOnBrokerRestart()
        {
            Assert.IsTrue(Conn.IsOpen);
            RestartServerAndWaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);
        }

        [Test]
        public void TestShutdownEventHandlersRecoveryOnConnectionAfterDelayedServerRestart()
        {
            Int32 counter = 0;
            Conn.ConnectionShutdown += (c, args) =>
            {
                Interlocked.Increment(ref counter);
            };

            Assert.IsTrue(Conn.IsOpen);
            StopRabbitMQ();
            Console.WriteLine("About to sleep for 9 seconds...");
            Thread.Sleep(9000);
            StartRabbitMQ();
            WaitForShutdown();
            WaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);

            Assert.IsTrue(counter >= 1);
        }

        [Test]
        public void TestShutdownEventHandlersRecoveryOnConnection()
        {
            Int32 counter = 0;
            Conn.ConnectionShutdown += (c, args) =>
            {
                Interlocked.Increment(ref counter);
            };

            Assert.IsTrue(Conn.IsOpen);
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);

            Assert.IsTrue(counter >= 3);
        }

        [Test]
        public void TestRecoveryEventHandlersOnConnection()
        {
            Int32 counter = 0;
            ((AutorecoveringConnection)Conn).Recovery += (c) =>
            {
                Interlocked.Increment(ref counter);
            };

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);

            Assert.IsTrue(counter >= 3);
        }

        [Test]
        public void TestBlockedListenersRecovery()
        {
            var latch = new AutoResetEvent(false);
            Conn.ConnectionBlocked += (c, reason) =>
            {
                latch.Set();
            };
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();

            Block();
            Wait(latch);

            Unblock();
        }

        [Test]
        public void TestUnblockedListenersRecovery()
        {
            var latch = new AutoResetEvent(false);
            Conn.ConnectionUnblocked += (c) =>
            {
                latch.Set();
            };
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();

            Block();
            Unblock();
            Wait(latch);
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
        public void TestShutdownEventHandlersRecoveryOnModel()
        {
            Int32 counter = 0;
            Model.ModelShutdown += (c, args) =>
            {
                Interlocked.Increment(ref counter);
            };

            Assert.IsTrue(Model.IsOpen);
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);

            Assert.IsTrue(counter >= 3);
        }

        [Test]
        public void TestRecoveryEventHandlersOnModel()
        {
            Int32 counter = 0;
            ((AutorecoveringModel)Model).Recovery += (m) =>
            {
                Interlocked.Increment(ref counter);
            };

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);

            Assert.IsTrue(counter >= 3);
        }

        [Test]
        public void TestBasicAckEventHandlerRecovery()
        {
            Model.ConfirmSelect();
            var latch = new AutoResetEvent(false);
            ((AutorecoveringModel)Model).BasicAcks += (m, args) =>
            {
                latch.Set();
            };
            ((AutorecoveringModel)Model).BasicNacks += (m, args) =>
            {
                latch.Set();
            };

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);

            WithTemporaryNonExclusiveQueue(Model, (m, q) => { m.BasicPublish("", q, null, enc.GetBytes("")); });
            Wait(latch);
        }

        [Test]
        public void TestRecoveryWithTopologyDisabled()
        {
            AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryDisabled();
            IModel ch = conn.CreateModel();
            string s  = "dotnet-client.test.recovery.q2";
            ch.QueueDelete(s);
            ch.QueueDeclare(s, false, true, false, null);
            ch.QueueDeclarePassive(s);
            Assert.IsTrue(ch.IsOpen);

            try
            {
                CloseAndWaitForRecovery(conn);
                Assert.IsTrue(ch.IsOpen);
                ch.QueueDeclarePassive(s);
                Assert.Fail("Expected an exception");
            } catch(OperationInterruptedException e)
            {
                // expected
            } finally
            {
                conn.Abort();
            }
        }


        [Test]
        public void TestExchangeRecovery()
        {
            var x = "dotnet-client.test.recovery.x1";
            DeclareNonDurableExchange(Model, x);
            CloseAndWaitForRecovery();
            AssertExchangeRecovery(Model, x);
            Model.ExchangeDelete(x);
        }

        [Test]
        public void TestExchangeRecoveryWithNoWait()
        {
            var x = "dotnet-client.test.recovery.x1-nowait";
            DeclareNonDurableExchangeNoWait(Model, x);
            CloseAndWaitForRecovery();
            AssertExchangeRecovery(Model, x);
            Model.ExchangeDelete(x);
        }

        [Test]
        public void TestClientNamedQueueRecovery()
        {
            string s = "dotnet-client.test.recovery.q1";
            WithTemporaryNonExclusiveQueue(Model, (m, q) => {
                CloseAndWaitForRecovery();
                AssertQueueRecovery(m, q, false);
                Model.QueueDelete(q);
            }, s);
        }

        [Test]
        public void TestClientNamedQueueRecoveryOnServerRestart()
        {
            string s = "dotnet-client.test.recovery.q1";
            WithTemporaryNonExclusiveQueue(Model, (m, q) => {
                RestartServerAndWaitForRecovery();
                AssertQueueRecovery(m, q, false);
                Model.QueueDelete(q);
            }, s);
        }

        [Test]
        public void TestClientNamedQueueRecoveryNoWait()
        {
            string s = "dotnet-client.test.recovery.q1-nowait";
            WithTemporaryQueueNoWait(Model, (m, q) => {
                CloseAndWaitForRecovery();
                AssertQueueRecovery(m, q);
            }, s);
        }

        [Test]
        public void TestDeclarationOfManyAutoDeleteQueuesWithTransientConsumer()
        {
            AssertRecordedQueues((AutorecoveringConnection)Conn, 0);
            for(var i = 0; i < 1000; i++)
            {
                var q     = Guid.NewGuid().ToString();
                Model.QueueDeclare(q, false, false, true, null);
                var dummy = new QueueingBasicConsumer(Model);
                var tag   = Model.BasicConsume(q, true, dummy);
                Model.BasicCancel(tag);
            }
            AssertRecordedQueues((AutorecoveringConnection)Conn, 0);
        }

        [Test]
        public void TestDeclarationOfManyAutoDeleteExchangesWithTransientQueuesThatAreUnbound()
        {
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
            for(var i = 0; i < 1000; i++)
            {
                var x = Guid.NewGuid().ToString();
                Model.ExchangeDeclare(x, "fanout", false, true, null);
                var q = Model.QueueDeclare();
                Model.QueueBind(q, x, "");
                Model.QueueUnbind(q, x, "", null);
            }
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
        }

        [Test]
        public void TestDeclarationOfManyAutoDeleteExchangesWithTransientQueuesThatAreDeleted()
        {
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
            for(var i = 0; i < 1000; i++)
            {
                var x = Guid.NewGuid().ToString();
                Model.ExchangeDeclare(x, "fanout", false, true, null);
                var q = Model.QueueDeclare();
                Model.QueueBind(q, x, "");
                Model.QueueDelete(q);
            }
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
        }

        [Test]
        public void TestDeclarationOfManyAutoDeleteExchangesWithTransientExchangesThatAreUnbound()
        {
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
            for(var i = 0; i < 1000; i++)
            {
                var x1 = "source-" + Guid.NewGuid().ToString();
                Model.ExchangeDeclare(x1, "fanout", false, true, null);
                var x2 = "destination-"+Guid.NewGuid().ToString();
                Model.ExchangeDeclare(x2, "fanout", false, false, null);
                Model.ExchangeBind(x2, x1, "");
                Model.ExchangeUnbind(x2, x1, "");
                Model.ExchangeDelete(x2);
            }
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
        }

        [Test]
        public void TestDeclarationOfManyAutoDeleteExchangesWithTransientExchangesThatAreDeleted()
        {
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
            for(var i = 0; i < 3; i++)
            {
                var x1 = "source-" + Guid.NewGuid().ToString();
                Model.ExchangeDeclare(x1, "fanout", false, true, null);
                var x2 = "destination-"+Guid.NewGuid().ToString();
                Model.ExchangeDeclare(x2, "fanout", false, false, null);
                Model.ExchangeBind(x2, x1, "");
                Model.ExchangeDelete(x2);
            }
            AssertRecordedExchanges((AutorecoveringConnection)Conn, 0);
        }

        [Test]
        public void TestServerNamedQueueRecovery()
        {
            var q = Model.QueueDeclare("", false, false, false, null).QueueName;
            var x = "amq.fanout";
            Model.QueueBind(q, x, "");

            string nameBefore = q;
            string nameAfter  = null;

            var latch = new AutoResetEvent(false);
            ((AutorecoveringConnection)Conn).Recovery += (m) => { latch.Set(); };
            ((AutorecoveringConnection)Conn).QueueNameChangeAfterRecovery += (prev, current) =>
            {
                nameAfter = current;
            };

            CloseAndWaitForRecovery();
            Wait(latch);

            Assert.IsNotNull(nameAfter);
            Assert.IsTrue(nameBefore.StartsWith("amq."));
            Assert.IsTrue(nameAfter.StartsWith("amq."));
            Assert.AreNotEqual(nameBefore, nameAfter);

            Model.QueueDeclarePassive(nameAfter);
        }

        [Test]
        public void TestExchangeToExchangeBindingRecovery()
        {
            var q  = Model.QueueDeclare("", false, false, false, null).QueueName;
            var x1 = "amq.fanout";
            var x2 = GenerateExchangeName();

            Model.ExchangeDeclare(x2, "fanout");
            Model.ExchangeBind(x1, x2, "");
            Model.QueueBind(q, x1, "");

            try
            {
                CloseAndWaitForRecovery();
                Assert.IsTrue(Model.IsOpen);
                Model.BasicPublish(x2, "", null, enc.GetBytes("msg"));
                AssertMessageCount(q, 1);
            } finally
            {
                WithTemporaryModel((m) => {
                    m.ExchangeDelete(x2);
                    m.QueueDelete(q);
                });
            }
        }

        [Test]
        public void TestThatDeletedQueueBindingsDontReappearOnRecovery()
        {
            var q  = Model.QueueDeclare("", false, false, false, null).QueueName;
            var x1 = "amq.fanout";
            var x2 = GenerateExchangeName();

            Model.ExchangeDeclare(x2, "fanout");
            Model.ExchangeBind(x1, x2, "");
            Model.QueueBind(q, x1, "");
            Model.QueueUnbind(q, x1, "", null);

            try
            {
                CloseAndWaitForRecovery();
                Assert.IsTrue(Model.IsOpen);
                Model.BasicPublish(x2, "", null, enc.GetBytes("msg"));
                AssertMessageCount(q, 0);
            } finally
            {
                WithTemporaryModel((m) => {
                    m.ExchangeDelete(x2);
                    m.QueueDelete(q);
                });
            }
        }

        [Test]
        public void TestThatDeletedExchangeBindingsDontReappearOnRecovery()
        {
            var q  = Model.QueueDeclare("", false, false, false, null).QueueName;
            var x1 = "amq.fanout";
            var x2 = GenerateExchangeName();

            Model.ExchangeDeclare(x2, "fanout");
            Model.ExchangeBind(x1, x2, "");
            Model.QueueBind(q, x1, "");
            Model.ExchangeUnbind(x1, x2, "", null);

            try
            {
                CloseAndWaitForRecovery();
                Assert.IsTrue(Model.IsOpen);
                Model.BasicPublish(x2, "", null, enc.GetBytes("msg"));
                AssertMessageCount(q, 0);
            } finally
            {
                WithTemporaryModel((m) => {
                    m.ExchangeDelete(x2);
                    m.QueueDelete(q);
                });
            }
        }

        [Test]
        public void TestThatDeletedExchangesDontReappearOnRecovery()
        {
            var x = GenerateExchangeName();
            Model.ExchangeDeclare(x, "fanout");
            Model.ExchangeDelete(x);

            try
            {
                CloseAndWaitForRecovery();
                Assert.IsTrue(Model.IsOpen);
                Model.ExchangeDeclarePassive(x);
                Assert.Fail("Expected an exception");
            } catch (OperationInterruptedException e)
            {
                // expected
                AssertShutdownError(e.ShutdownReason, 404);
            }
        }

        [Test]
        public void TestThatDeletedQueuesDontReappearOnRecovery()
        {
            var q = "dotnet-client.recovery.q1";
            Model.QueueDeclare(q, false, false, false, null);
            Model.QueueDelete(q);

            try
            {
                CloseAndWaitForRecovery();
                Assert.IsTrue(Model.IsOpen);
                Model.QueueDeclarePassive(q);
                Assert.Fail("Expected an exception");
            } catch (OperationInterruptedException e)
            {
                // expected
                AssertShutdownError(e.ShutdownReason, 404);
            }
        }

        [Test]
        public void TestQueueRecoveryWithManyQueues()
        {
            List<string> qs = new List<string>();
            var n = 1024;
            for(var i = 0; i < n; i++)
            {
                qs.Add(Model.QueueDeclare(GenerateQueueName(), false, false, false, null).QueueName);
            }
            CloseAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);
            foreach (var q in qs)
            {
                AssertQueueRecovery(Model, q, false);
                Model.QueueDelete(q);
            }
        }

        [Test]
        public void TestConsumerRecoveryWithManyConsumers()
        {
            var q = Model.QueueDeclare(GenerateQueueName(), false, false, false, null).QueueName;
            var n = 1024;

            for(var i = 0; i < n; i++)
            {
                var cons = new QueueingBasicConsumer(Model);
                Model.BasicConsume(q, true, cons);
            }

            var latch = new AutoResetEvent(false);
            ((AutorecoveringConnection)Conn).ConsumerTagChangeAfterRecovery += (prev, current) =>
            {
                latch.Set();
            };

            CloseAndWaitForRecovery();
            Wait(latch);
            Assert.IsTrue(Model.IsOpen);
            AssertConsumerCount(q, n);
        }

        [Test]
        public void TestConsumerRecoveryOnClientNamedQueueWithOneRecovery()
        {
            var c    = CreateAutorecoveringConnection();
            var m    = c.CreateModel();
            var q    = m.QueueDeclare("dotnet-client.recovery.queue1",
                                      false, false, false, null).QueueName;
            var cons = new EventingBasicConsumer(m);
            m.BasicConsume(q, true, cons);
            AssertConsumerCount(m, q, 1);

            string latestName  = null;

            c.QueueNameChangeAfterRecovery += (prev, current) =>
            {
                latestName = current;
            };

            CloseAndWaitForRecovery(c);
            AssertConsumerCount(m, latestName, 1);
            CloseAndWaitForRecovery(c);
            AssertConsumerCount(m, latestName, 1);
            CloseAndWaitForRecovery(c);
            AssertConsumerCount(m, latestName, 1);

            var latch = new AutoResetEvent(false);
            cons.Received += (s, args) => { latch.Set(); };

            m.BasicPublish("", q, null, enc.GetBytes("msg"));
            Wait(latch);

            m.QueueDelete(q);
        }

        [Test]
        public void TestRecoveryEventHandlersOnChannel()
        {
            Int32 counter = 0;
            ((AutorecoveringModel)Model).Recovery += (c) =>
            {
                Interlocked.Increment(ref counter);
            };

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);

            Assert.IsTrue(counter >= 1);
        }

        [Test]
        public void TestThatCancelledConsumerDoesNotReappearOnRecovery()
        {
            var q = Model.QueueDeclare(GenerateQueueName(), false, false, false, null).QueueName;
            var n = 1024;

            for(var i = 0; i < n; i++)
            {
                var cons = new QueueingBasicConsumer(Model);
                var tag  = Model.BasicConsume(q, true, cons);
                Model.BasicCancel(tag);
            }
            CloseAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);
            AssertConsumerCount(q, 0);
        }

        public class TestBasicConsumer1 : DefaultBasicConsumer
        {
            private ushort counter = 0;
            private AutoResetEvent latch;
            private Action action;

            public TestBasicConsumer1(IModel model, AutoResetEvent latch, Action fn) : base(model) {
                this.latch  = latch;
                this.action = fn;
            }

            public override void HandleBasicDeliver(string consumerTag,
                                                    ulong deliveryTag,
                                                    bool redelivered,
                                                    string exchange,
                                                    string routingKey,
                                                    IBasicProperties properties,
                                                    byte[] body)
            {
                try
                {
                    if(deliveryTag == 7 && counter < 10)
                    {
                        this.action();
                    }
                    if(counter == 9)
                    {
                        this.latch.Set();
                    }
                    this.PostHandleDelivery(deliveryTag);
                } finally
                {
                    counter += 1;
                }
            }

            public virtual void PostHandleDelivery(ulong deliveryTag)
            {
            }
        }

        public class AckingBasicConsumer : TestBasicConsumer1
        {
            public AckingBasicConsumer(IModel model, AutoResetEvent latch, Action fn) : base(model, latch, fn) {}

            public override void PostHandleDelivery(ulong deliveryTag)
            {
                base.Model.BasicAck(deliveryTag, false);
            }
        }

        public class NackingBasicConsumer : TestBasicConsumer1
        {
            public NackingBasicConsumer(IModel model, AutoResetEvent latch, Action fn) : base(model, latch, fn) {}

            public override void PostHandleDelivery(ulong deliveryTag)
            {
                base.Model.BasicNack(deliveryTag, false, false);
            }
        }

        public class RejectingBasicConsumer : TestBasicConsumer1
        {
            public RejectingBasicConsumer(IModel model, AutoResetEvent latch, Action fn) : base(model, latch, fn) {}

            public override void PostHandleDelivery(ulong deliveryTag)
            {
                base.Model.BasicReject(deliveryTag, false);
            }
        }

        [Test]
        public void TestBasicAckAfterChannelRecovery()
        {
            var latch = new AutoResetEvent(false);
            var cons  = new AckingBasicConsumer(Model, latch, () => {
                CloseAndWaitForRecovery();
            });

            TestDelayedBasicAckNackAfterChannelRecovery(cons, latch);
        }

        [Test]
        public void TestBasicNackAfterChannelRecovery()
        {
            var latch = new AutoResetEvent(false);
            var cons  = new NackingBasicConsumer(Model, latch, () => {
                CloseAndWaitForRecovery();
            });

            TestDelayedBasicAckNackAfterChannelRecovery(cons, latch);
        }

        [Test]
        public void TestBasicRejectAfterChannelRecovery()
        {
            var latch = new AutoResetEvent(false);
            var cons  = new RejectingBasicConsumer(Model, latch, () => {
                CloseAndWaitForRecovery();
            });

            TestDelayedBasicAckNackAfterChannelRecovery(cons, latch);
        }

        [Test]
        public void TestCreateModelOnClosedAutorecoveringConnectionDoesNotHang()
        {
            // we don't want this to recover quickly in this test
            var c  = CreateAutorecoveringConnection(TimeSpan.FromSeconds(20));

            try
            {
                c.Close();
                WaitForShutdown(c);
                Assert.IsFalse(c.IsOpen);
                c.CreateModel();
                Assert.Fail("Expected an exception");
            } catch (AlreadyClosedException ace)
            {
                // expected
            } finally
            {
                StartRabbitMQ();
                if(c.IsOpen)
                {
                    c.Abort();
                }
            }
        }

        protected void TestDelayedBasicAckNackAfterChannelRecovery(TestBasicConsumer1 cons, AutoResetEvent latch)
        {
            var q = Model.QueueDeclare(GenerateQueueName(), false, false, false, null).QueueName;
            var n = 30;
            Model.BasicQos(0, 1, false);
            Model.BasicConsume(q, false, cons);

            var publishingConn  = CreateAutorecoveringConnection();
            var publishingModel = publishingConn.CreateModel();

            for(var i = 0; i < n; i++)
            {
                publishingModel.BasicPublish("", q, null, enc.GetBytes(""));
            }

            Wait(latch, TimeSpan.FromSeconds(20));
            Model.QueueDelete(q);
            publishingModel.Close();
            publishingConn.Close();
        }


        //
        // Implementation
        //

        protected void CloseAndWaitForRecovery()
        {
            CloseAndWaitForRecovery((AutorecoveringConnection)this.Conn);
        }

        protected void CloseAllAndWaitForRecovery()
        {
            CloseAllAndWaitForRecovery((AutorecoveringConnection)this.Conn);
        }

        protected void CloseAndWaitForRecovery(AutorecoveringConnection conn)
        {
            var sl = PrepareForShutdown(conn);
            var rl = PrepareForRecovery(conn);
            CloseConnection(conn);
            Wait(sl);
            Wait(rl);
        }

        protected void CloseAllAndWaitForRecovery(AutorecoveringConnection conn)
        {
            var rl = PrepareForRecovery(conn);
            CloseAllConnections();
            Wait(rl);
        }

        protected void CloseAndWaitForShutdown(AutorecoveringConnection conn)
        {
            var sl = PrepareForShutdown(conn);
            CloseConnection(conn);
            Wait(sl);
        }

        protected void RestartServerAndWaitForRecovery()
        {
            RestartServerAndWaitForRecovery((AutorecoveringConnection)this.Conn);
        }

        protected void RestartServerAndWaitForRecovery(AutorecoveringConnection conn)
        {
            var sl = PrepareForShutdown(conn);
            var rl = PrepareForRecovery(conn);
            RestartRabbitMQ();
            Wait(sl);
            Wait(rl);
        }

        protected void WaitForRecovery()
        {
            Wait(PrepareForRecovery((AutorecoveringConnection)this.Conn));
        }

        protected void WaitForRecovery(AutorecoveringConnection conn)
        {
            Wait(PrepareForRecovery(conn));
        }

        protected void WaitForShutdown()
        {
            Wait(PrepareForShutdown(this.Conn));
        }

        protected void WaitForShutdown(IConnection conn)
        {
            Wait(PrepareForShutdown(conn));
        }

        protected AutoResetEvent PrepareForShutdown(IConnection conn)
        {
            var latch = new AutoResetEvent(false);
            conn.ConnectionShutdown += (c, args) =>
            {
                latch.Set();
            };

            return latch;
        }

        protected AutoResetEvent PrepareForRecovery(AutorecoveringConnection conn)
        {
            var latch = new AutoResetEvent(false);
            conn.Recovery += (c) =>
            {
                latch.Set();
            };

            return latch;
        }

        protected void Wait(AutoResetEvent latch)
        {
            Assert.IsTrue(latch.WaitOne(TimeSpan.FromSeconds(8)));
        }

        protected void Wait(AutoResetEvent latch, TimeSpan timeSpan)
        {
            Assert.IsTrue(latch.WaitOne(timeSpan));
        }

        protected override void ReleaseResources()
        {
            Unblock();
        }

        protected void AssertExchangeRecovery(IModel m, string x)
        {
            m.ConfirmSelect();
            WithTemporaryNonExclusiveQueue(m, (_, q) => {
                var rk = "routing-key";
                m.QueueBind(q, x, rk);
                var mb = RandomMessageBody();
                m.BasicPublish(x, rk, null, mb);

                Assert.IsTrue(WaitForConfirms(m));
                m.ExchangeDeclarePassive(x);
            });
        }

        protected void AssertQueueRecovery(IModel m, string q)
        {
            AssertQueueRecovery(m, q, true);
        }

        protected void AssertQueueRecovery(IModel m, string q, bool exclusive)
        {
            m.ConfirmSelect();
            m.QueueDeclarePassive(q);
            var ok1 = m.QueueDeclare(q, false, exclusive, false, null);
            Assert.AreEqual(ok1.MessageCount, 0);
            m.BasicPublish("", q, null, enc.GetBytes(""));
            Assert.IsTrue(WaitForConfirms(m));
            var ok2 = m.QueueDeclare(q, false, exclusive, false, null);
            Assert.AreEqual(ok2.MessageCount, 1);
        }

        protected void AssertRecordedQueues(AutorecoveringConnection c, int n)
        {
            Assert.AreEqual(n, c.RecordedQueues.Count);
        }

        protected void AssertRecordedExchanges(AutorecoveringConnection c, int n)
        {
            Assert.AreEqual(n, c.RecordedExchanges.Count);
        }

        protected IConnection CreateNonRecoveringConnection()
        {
            var cf = new ConnectionFactory();
            return cf.CreateConnection();
        }

        protected AutorecoveringConnection CreateAutorecoveringConnection()
        {
            return CreateAutorecoveringConnection(RECOVERY_INTERVAL);
        }

       protected AutorecoveringConnection CreateAutorecoveringConnection(TimeSpan interval)
        {
            var cf = new ConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.NetworkRecoveryInterval  = interval;
            return (AutorecoveringConnection)cf.CreateConnection();
        }

       protected AutorecoveringConnection CreateAutorecoveringConnectionWithTopologyRecoveryDisabled()
        {
            var cf = new ConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.TopologyRecoveryEnabled  = false;
            cf.NetworkRecoveryInterval  = RECOVERY_INTERVAL;
            return (AutorecoveringConnection)cf.CreateConnection();
        }
    }
}
#pragma warning restore 0168