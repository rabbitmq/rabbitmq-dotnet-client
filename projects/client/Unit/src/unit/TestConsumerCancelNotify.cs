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

using NUnit.Framework;
using RabbitMQ.Client.Events;
using System;
using System.Linq;
using System.Threading;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestConsumerCancelNotify : IntegrationFixture
    {
        protected readonly Object lockObject = new Object();
        protected bool notifiedCallback;
        protected bool notifiedEvent;
        protected string consumerTag;

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

        [Test]
        public void TestCorrectConsumerTag()
        {
            string q1 = GenerateQueueName();
            string q2 = GenerateQueueName();

            Model.QueueDeclare(q1, false, false, false, null);
            Model.QueueDeclare(q2, false, false, false, null);

            EventingBasicConsumer consumer = new EventingBasicConsumer(Model);
            string consumerTag1 = Model.BasicConsume(q1, true, consumer);
            string consumerTag2 = Model.BasicConsume(q2, true, consumer);

            string notifiedConsumerTag = null;
            consumer.ConsumerCancelled += (sender, args) =>
            {
                lock (lockObject)
                {
                    notifiedConsumerTag = args.ConsumerTags.First();
                    Monitor.PulseAll(lockObject);
                }
            };

            Model.QueueDelete(q1);
            WaitOn(lockObject);
            Assert.AreEqual(consumerTag1, notifiedConsumerTag);

            Model.QueueDelete(q2);
        }

        public void TestConsumerCancel(string queue, bool EventMode, ref bool notified)
        {
            Model.QueueDeclare(queue, false, true, false, null);
            IBasicConsumer consumer = new CancelNotificationConsumer(Model, this, EventMode);
            var actualConsumerTag = Model.BasicConsume(queue, false, consumer);

            Model.QueueDelete(queue);
            WaitOn(lockObject);
            Assert.IsTrue(notified);
            Assert.AreEqual(actualConsumerTag, consumerTag);
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
                        testClass.consumerTag = consumerTag;
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
                    testClass.consumerTag = arg.ConsumerTags[0];
                    Monitor.PulseAll(testClass.lockObject);
                }
            }
        }
    }
}
