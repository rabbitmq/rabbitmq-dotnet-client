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
using System.Threading.Tasks;
using NUnit.Framework;
using RabbitMQ.Client.client.impl.Channel;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestConsumerCancelNotify : IntegrationFixture
    {
        private readonly ManualResetEventSlim _latch = new ManualResetEventSlim(false);
        private bool _notifiedCallback;
        private bool _notifiedEvent;
        private string _consumerTag;

        [Test]
        public Task TestConsumerCancelNotification()
        {
            return TestConsumerCancelAsync("queue_consumer_cancel_notify", false);
        }

        [Test]
        public Task TestConsumerCancelEvent()
        {
            return TestConsumerCancelAsync("queue_consumer_cancel_event", true);
        }

        [Test]
        public async Task TestCorrectConsumerTag()
        {
            string q1 = GenerateQueueName();
            string q2 = GenerateQueueName();

            await _channel.DeclareQueueAsync(q1, false, false, false).ConfigureAwait(false);
            await _channel.DeclareQueueAsync(q2, false, false, false).ConfigureAwait(false);

            EventingBasicConsumer consumer = new EventingBasicConsumer(_channel);
            string consumerTag1 = await _channel.ActivateConsumerAsync(consumer, q1, true).ConfigureAwait(false);
            string consumerTag2 = await _channel.ActivateConsumerAsync(consumer, q2, true).ConfigureAwait(false);

            string notifiedConsumerTag = null;
            consumer.ConsumerCancelled += (sender, args) =>
            {
                notifiedConsumerTag = args.ConsumerTags.First();
                _latch.Set();
            };

            _latch.Reset();
            await _channel.DeleteQueueAsync(q1).ConfigureAwait(false);
            Wait(_latch);

            Assert.AreEqual(consumerTag1, notifiedConsumerTag);

            await _channel.DeleteQueueAsync(q2).ConfigureAwait(false);
        }

        public async Task TestConsumerCancelAsync(string queue, bool EventMode)
        {
            await _channel.DeclareQueueAsync(queue, false, true, false).ConfigureAwait(false);
            IBasicConsumer consumer = new CancelNotificationConsumer(_channel, this, EventMode);
            string actualConsumerTag = await _channel.ActivateConsumerAsync(consumer, queue, false).ConfigureAwait(false);

            await _channel.DeleteQueueAsync(queue).ConfigureAwait(false);
            Wait(_latch);

            Assert.IsTrue(EventMode ? _notifiedEvent : _notifiedCallback);
            Assert.AreEqual(actualConsumerTag, _consumerTag);
        }

        private class CancelNotificationConsumer : DefaultBasicConsumer
        {
            private readonly TestConsumerCancelNotify _testClass;
            private readonly bool _eventMode;

            public CancelNotificationConsumer(IChannel channel, TestConsumerCancelNotify tc, bool EventMode)
                : base(channel)
            {
                _testClass = tc;
                _eventMode = EventMode;
                if (EventMode)
                {
                    ConsumerCancelled += Cancelled;
                }

                tc._consumerTag = default;
                tc._notifiedCallback = default;
                tc._notifiedEvent = default;
                tc._latch.Reset();
            }

            public override void HandleBasicCancel(string consumerTag)
            {
                if (!_eventMode)
                {
                    _testClass._notifiedCallback = true;
                    _testClass._consumerTag = consumerTag;
                    _testClass._latch.Set();
                }
                base.HandleBasicCancel(consumerTag);
            }

            private void Cancelled(object sender, ConsumerEventArgs arg)
            {
                _testClass._notifiedEvent = true;
                _testClass._consumerTag = arg.ConsumerTags[0];
                _testClass._latch.Set();
            }
        }
    }
}
