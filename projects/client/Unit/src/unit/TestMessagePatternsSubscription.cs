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
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Timers;

using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.MessagePatterns;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestMessagePatternsSubscription : IntegrationFixture
    {
        [Test]
        public void TestChannelClosureIsObservableOnSubscription()
        {
            string q = Model.QueueDeclare();
            Subscription sub = new Subscription(Model, q, true);

            BasicDeliverEventArgs r1;
            Assert.IsFalse(sub.Next(100, out r1));

            Model.BasicPublish("", q, null, enc.GetBytes("a message"));
            Model.BasicPublish("", q, null, enc.GetBytes("a message"));

            BasicDeliverEventArgs r2;
            Assert.IsTrue(sub.Next(1000, out r2));
            Assert.IsNotNull(sub.Next());

            Model.Close();
            Assert.IsNull(sub.Next());

            BasicDeliverEventArgs r3;
            Assert.IsFalse(sub.Next(100, out r3));
        }

        [Test]
        public void TestSubscriptionAck()
        {
            TestSubscriptionAction((s) => s.Ack());
        }

        [Test]
        public void TestSubscriptionNack()
        {
            TestSubscriptionAction((s) => s.Nack(false, false));
        }

        [Test]
        public void TestConcurrentIterationAndAck()
        {
            TestConcurrentIterationWithDrainer((s) => s.Ack());
        }

        [Test]
        public void TestConcurrentIterationAndNack()
        {
            TestConcurrentIterationWithDrainer((s) => s.Nack(false, false));
        }

        protected void TestConcurrentIterationWithDrainer(SubscriptionAction act)
        {
            IDictionary<string, object> args = new Dictionary<string, object>
            {
                {"x-message-ttl", 5000}
            };
            string q = Model.QueueDeclare("", false, true, false, args);
            Subscription sub = new Subscription(Model, q, false);

            PreparedQueue(q);

            List<Thread> ts = new List<Thread>();
            for (int i = 0; i < 50; i++)
            {
                SubscriptionDrainer drainer = new SubscriptionDrainer(sub, act);
                Thread t = new Thread(drainer.Drain);
                ts.Add(t);
                t.Start();
            }

            foreach(Thread t in ts)
            {
                t.Join();
            }
        }

        private void TestSubscriptionAction(SubscriptionAction action)
        {
            Model.BasicQos(0, 1, false);
            string q = Model.QueueDeclare();
            Subscription sub = new Subscription(Model, q, false);

            Model.BasicPublish("", q, null, enc.GetBytes("a message"));
            BasicDeliverEventArgs res = sub.Next();
            Assert.IsNotNull(res);
            action(sub);
            QueueDeclareOk ok = Model.QueueDeclarePassive(q);
            Assert.AreEqual(0, ok.MessageCount);
        }

        protected delegate void SubscriptionAction(Subscription s);

        protected class SubscriptionDrainer
        {
            protected Subscription m_subscription;
            private SubscriptionAction PostProcess { get; set; }

            public SubscriptionDrainer(Subscription sub, SubscriptionAction op)
            {
                m_subscription = sub;
                PostProcess = op;
            }

            public void Drain()
            {
                #pragma warning disable 0168
                try
                {
                    for(int i = 0; i < 100; i++)
                    {
                        BasicDeliverEventArgs ea = m_subscription.Next();
                        if(ea != null)
                        {
                            Assert.That(ea, Is.TypeOf(typeof(BasicDeliverEventArgs)));
                            this.PostProcess(m_subscription);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                catch (AlreadyClosedException ace)
                {
                    // expected
                }
                finally
                {
                    m_subscription.Close();
                }
                #pragma warning restore

            }
        }

        private void PreparedQueue(string q)
        {
            for (int i = 0; i < 1024; i++)
            {
                Model.BasicPublish("", q, null, enc.GetBytes("a message"));
            }
        }
    }
}
