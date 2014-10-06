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

using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace RabbitMQ.Client.Unit {
    [TestFixture]
    public class TestConnectionRecovery : IntegrationFixture {
        [SetUp]
        public override void Init()
        {
            ConnectionFactory connFactory = new ConnectionFactory();
            connFactory.AutomaticRecoveryEnabled = true;
            Conn = (AutorecoveringConnection)connFactory.CreateConnection();
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

            WithTemporaryQueue(Model, (m, q) => { m.BasicPublish("", q, null, enc.GetBytes("")); });
            Wait(latch);
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
            WithTemporaryQueue(Model, (m, q) => {
                CloseAndWaitForRecovery();
                AssertQueueRecovery(m, q);
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
        public void TestConsumerRecoveryOnClientNamedQueueWithASingleRecovery()
        {
            var q    = Model.QueueDeclare("dotnet-client.recovery.queue1", false, false, false, null).QueueName;
            var cons = new QueueingBasicConsumer(Model);
            Model.BasicConsume(q, true, cons);
            AssertConsumerCount(q, 1);

            string latestName  = null;

            ((AutorecoveringConnection)Conn).QueueNameChangeAfterRecovery += (prev, current) =>
            {
                latestName = current;
            };

            CloseAllAndWaitForRecovery();
            AssertConsumerCount(latestName, 1);

            // TODO: assert consumer recovery
            Model.QueueDelete(q);
        }

        // TODO: TestThatCancelledConsumerDoesNotReappearOnRecover
        // TODO: TestChannelRecoveryCallback
        // TODO: TestBasicAckAfterChannelRecovery


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
            var sl = PrepareForShutdown(conn);
            var rl = PrepareForRecovery(conn);
            CloseAllConnections();
            Wait(sl);
            Wait(rl);
        }

        protected AutoResetEvent PrepareForShutdown(AutorecoveringConnection conn)
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

        protected override void ReleaseResources()
        {
            Unblock();
        }

        protected void AssertExchangeRecovery(IModel m, string x)
        {
            m.ConfirmSelect();
            WithTemporaryQueue(m, (_, q) => {
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
    }
}