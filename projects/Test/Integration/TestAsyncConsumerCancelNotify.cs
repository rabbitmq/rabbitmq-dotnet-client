// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestAsyncConsumerCancelNotify : IntegrationFixture
    {
        private readonly TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private string _consumerTag;

        public TestAsyncConsumerCancelNotify(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public Task TestConsumerCancelNotification()
        {
            return TestConsumerCancelAsync("queue_consumer_cancel_notify", false);
        }

        [Fact]
        public Task TestConsumerCancelEvent()
        {
            return TestConsumerCancelAsync("queue_consumer_cancel_event", true);
        }

        [Fact]
        public async Task TestCorrectConsumerTag()
        {
            string q1 = GenerateQueueName();
            string q2 = GenerateQueueName();

            await _channel.QueueDeclareAsync(q1, false, false, false);
            await _channel.QueueDeclareAsync(q2, false, false, false);

            AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(_channel);
            string consumerTag1 = await _channel.BasicConsumeAsync(q1, true, consumer);
            string consumerTag2 = await _channel.BasicConsumeAsync(q2, true, consumer);

            string notifiedConsumerTag = null;
            consumer.Unregistered += (sender, args) =>
            {
                notifiedConsumerTag = args.ConsumerTags.First();
                _tcs.TrySetResult(true);
                return Task.CompletedTask;
            };

            await _channel.QueueDeleteAsync(q1);
            await WaitAsync(_tcs, "ConsumerCancelled event");
            Assert.Equal(consumerTag1, notifiedConsumerTag);

            await _channel.QueueDeleteAsync(q2);
        }

        private async Task TestConsumerCancelAsync(string queue, bool eventMode)
        {
            await _channel.QueueDeclareAsync(queue, false, true, false);
            IAsyncBasicConsumer consumer = new CancelNotificationConsumer(_channel, this, eventMode);
            string actualConsumerTag = await _channel.BasicConsumeAsync(queue, false, consumer);

            await _channel.QueueDeleteAsync(queue);
            await WaitAsync(_tcs, "HandleBasicCancel / Cancelled event");
            Assert.Equal(actualConsumerTag, _consumerTag);
        }

        private class CancelNotificationConsumer : AsyncEventingBasicConsumer
        {
            private readonly TestAsyncConsumerCancelNotify _testClass;
            private readonly bool _eventMode;

            public CancelNotificationConsumer(IChannel channel, TestAsyncConsumerCancelNotify tc, bool eventMode)
                : base(channel)
            {
                _testClass = tc;
                _eventMode = eventMode;
                if (eventMode)
                {
                    Unregistered += CancelledAsync;
                }
            }

            public override Task HandleBasicCancelAsync(string consumerTag,
                CancellationToken cancellationToken = default)
            {
                if (!_eventMode)
                {
                    _testClass._consumerTag = consumerTag;
                    _testClass._tcs.SetResult(true);
                }

                return base.HandleBasicCancelAsync(consumerTag, cancellationToken);
            }

            private Task CancelledAsync(object sender, ConsumerEventArgs arg)
            {
                _testClass._consumerTag = arg.ConsumerTags[0];
                _testClass._tcs.SetResult(true);
                return Task.CompletedTask;
            }
        }
    }
}
