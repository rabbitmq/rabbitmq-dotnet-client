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

using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestConsumerCancelNotify : IntegrationFixture
    {
        protected readonly object lockObject = new object();
        protected bool notifiedCallback;
        protected bool notifiedEvent;
        protected string consumerTag;

        [Test]
        public async ValueTask TestConsumerCancelNotification()
        {
            await Model.QueueDeclare("queue_consumer_cancel_notify", false, true, false, null);
            IAsyncBasicConsumer consumer = new CancelNotificationConsumer(Model, this, false);
            string actualConsumerTag = await Model.BasicConsume("queue_consumer_cancel_notify", false, consumer);

            await Model.QueueDelete("queue_consumer_cancel_notify");
            WaitOn(lockObject);
            Assert.IsTrue(notifiedCallback);
            Assert.AreEqual(actualConsumerTag, consumerTag);
        }

        [Test]
        public async ValueTask TestConsumerCancelEvent()
        {
            await Model.QueueDeclare("queue_consumer_cancel_event", false, true, false, null);
            IAsyncBasicConsumer consumer = new CancelNotificationConsumer(Model, this, true);
            string actualConsumerTag = await Model.BasicConsume("queue_consumer_cancel_event", false, consumer);

            await Model.QueueDelete("queue_consumer_cancel_event");
            WaitOn(lockObject);
            Assert.IsTrue(notifiedEvent);
            Assert.AreEqual(actualConsumerTag, consumerTag);
        }

        [Test]
        public async ValueTask TestCorrectConsumerTag()
        {
            string q1 = GenerateQueueName();
            string q2 = GenerateQueueName();

            await Model.QueueDeclare(q1, false, false, false, null);
            await Model.QueueDeclare(q2, false, false, false, null);

            EventingBasicConsumer consumer = new EventingBasicConsumer(Model);
            string consumerTag1 = await Model.BasicConsume(q1, true, consumer);
            string consumerTag2 = await Model.BasicConsume(q2, true, consumer);

            bool notifiedConsumerTag1 = false;
            consumer.ConsumerCancelled += (sender, args) =>
            {
                lock (lockObject)
                {
                    notifiedConsumerTag1 = consumerTag1.Equals(args.ConsumerTags[0], System.StringComparison.OrdinalIgnoreCase);
                    Monitor.PulseAll(lockObject);
                }
                return default;
            };

            await Model.QueueDelete(q1);
            WaitOn(lockObject);
            Assert.IsTrue(notifiedConsumerTag1);
            await Model.QueueDelete(q2);
        }

        private class CancelNotificationConsumer : DefaultBasicConsumer
        {
            private readonly TestConsumerCancelNotify _testClass;
            private readonly bool _eventMode;

            public CancelNotificationConsumer(IModel model, TestConsumerCancelNotify tc, bool EventMode)
                : base(model)
            {
                _testClass = tc;
                _eventMode = EventMode;
                if (EventMode)
                {
                    ConsumerCancelled += Cancelled;
                }
            }

            public override ValueTask HandleBasicCancel(string consumerTag)
            {
                if (!_eventMode)
                {
                    lock (_testClass.lockObject)
                    {
                        _testClass.notifiedCallback = true;
                        _testClass.consumerTag = consumerTag;
                        Monitor.PulseAll(_testClass.lockObject);
                    }
                }
                return base.HandleBasicCancel(consumerTag);
            }

            private ValueTask Cancelled(object sender, ConsumerEventArgs arg)
            {
                lock (_testClass.lockObject)
                {
                    _testClass.notifiedEvent = true;
                    _testClass.consumerTag = arg.ConsumerTags[0];
                    Monitor.PulseAll(_testClass.lockObject);
                }

                return default;
            }
        }
    }
}
