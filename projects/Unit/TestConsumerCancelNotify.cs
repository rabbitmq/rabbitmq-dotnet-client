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

using System.Linq;
using System.Threading;

using RabbitMQ.Client.Events;

using Xunit;
using Xunit.Abstractions;

namespace RabbitMQ.Client.Unit
{

    public class TestConsumerCancelNotify : IntegrationFixture
    {
        protected readonly object lockObject = new object();
        protected bool notifiedCallback;
        protected bool notifiedEvent;
        protected string consumerTag;

        public TestConsumerCancelNotify(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void TestConsumerCancelNotification()
        {
            TestConsumerCancel("queue_consumer_cancel_notify", false, ref notifiedCallback);
        }

        [Fact]
        public void TestConsumerCancelEvent()
        {
            TestConsumerCancel("queue_consumer_cancel_event", true, ref notifiedEvent);
        }

        [Fact]
        public void TestCorrectConsumerTag()
        {
            ManualResetEventSlim manualResetEventSlim = new ManualResetEventSlim();
            string q1 = GenerateQueueName();
            string q2 = GenerateQueueName();

            _model.QueueDeclare(q1, false, false, false, null);
            _model.QueueDeclare(q2, false, false, false, null);

            EventingBasicConsumer consumer = new EventingBasicConsumer(_model);
            string consumerTag1 = _model.BasicConsume(q1, true, consumer);
            string consumerTag2 = _model.BasicConsume(q2, true, consumer);

            string notifiedConsumerTag = null;
            consumer.ConsumerCancelled += (sender, args) =>
            {
                    notifiedConsumerTag = args.ConsumerTags.First();
                    manualResetEventSlim.Set();
            };

            _model.QueueDelete(q1);
            Assert.True(manualResetEventSlim.Wait(TimingFixture.TestTimeout));
            Assert.Equal(consumerTag1, notifiedConsumerTag);

            _model.QueueDelete(q2);
        }

        private void TestConsumerCancel(string queue, bool EventMode, ref bool notified)
        {
            ManualResetEventSlim manualResetEventSlim = new ManualResetEventSlim();
            _model.QueueDeclare(queue, false, true, false, null);
            IBasicConsumer consumer = new CancelNotificationConsumer(_model, this, EventMode, manualResetEventSlim);
            string actualConsumerTag = _model.BasicConsume(queue, false, consumer);
            _model.QueueDelete(queue);
            Assert.True(manualResetEventSlim.Wait(TimingFixture.TestTimeout));
            Assert.True(notified);
            Assert.Equal(actualConsumerTag, consumerTag);
        }

        private class CancelNotificationConsumer : DefaultBasicConsumer
        {
            private readonly TestConsumerCancelNotify _testClass;
            private readonly bool _eventMode;
            private readonly ManualResetEventSlim _manualResetEventSlim;

            public CancelNotificationConsumer(IModel model, TestConsumerCancelNotify tc, bool EventMode, ManualResetEventSlim manualResetEventSlim)
                : base(model)
            {
                _testClass = tc;
                _eventMode = EventMode;
                _manualResetEventSlim = manualResetEventSlim;
                if (EventMode)
                {
                    ConsumerCancelled += Cancelled;
                }
            }

            public override void HandleBasicCancel(string consumerTag)
            {
                if (!_eventMode)
                {
                    _testClass.notifiedCallback = true;
                    _testClass.consumerTag = consumerTag;
                    _manualResetEventSlim.Set();
                }

                base.HandleBasicCancel(consumerTag);
            }

            private void Cancelled(object sender, ConsumerEventArgs arg)
            {
                    _testClass.notifiedEvent = true;
                    _testClass.consumerTag = arg.ConsumerTags[0];
                _manualResetEventSlim.Set();
            }
        }
    }
}
