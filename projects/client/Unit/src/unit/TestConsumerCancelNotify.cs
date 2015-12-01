// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2015 Pivotal Software, Inc.
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
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;
using RabbitMQ.Client.Events;
using System;
using System.Threading;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestConsumerCancelNotify : IntegrationFixture
    {
        protected readonly Object lockObject = new Object();
        protected bool notifiedCallback;
        protected bool notifiedEvent;

        [Test]
        public void TestConsumerCancelNotification()
        {
            TestConsumerCancel("queue_consumer_cancel_notify", false, ref notifiedCallback);
        }

        [Test]
        public void TestConsumerCancelEvent()
        {
            TestConsumerCancel("queue_consumer_cancel_event", true, ref notifiedEvent);
        }

        public void TestConsumerCancel(string queue, bool EventMode, ref bool notified)
        {
            Model.QueueDeclare(queue, false, true, false, null);
            IBasicConsumer consumer = new CancelNotificationConsumer(Model, this, EventMode);
            Model.BasicConsume(queue, false, consumer);

            Model.QueueDelete(queue);
            WaitOn(lockObject);
            Assert.IsTrue(notified);
        }

        private class CancelNotificationConsumer : DefaultBasicConsumer
        {
            private TestConsumerCancelNotify testClass;
            private bool EventMode;

            public CancelNotificationConsumer(IModel model, TestConsumerCancelNotify tc, bool EventMode)
                : base(model)
            {
                this.testClass = tc;
                this.EventMode = EventMode;
                if (EventMode)
                {
                    ConsumerCancelled += Cancelled;
                }
            }

            public override void HandleBasicCancel(string consumerTag)
            {
                if (!EventMode)
                {
                    lock (testClass.lockObject)
                    {
                        testClass.notifiedCallback = true;
                        Monitor.PulseAll(testClass.lockObject);
                    }
                }
                base.HandleBasicCancel(consumerTag);
            }

            private void Cancelled(object sender, ConsumerEventArgs arg)
            {
                lock (testClass.lockObject)
                {
                    testClass.notifiedEvent = true;
                    Monitor.PulseAll(testClass.lockObject);
                }
            }
        }
    }
}
