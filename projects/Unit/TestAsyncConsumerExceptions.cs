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
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RabbitMQ.Client.client.impl.Channel;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestAsyncConsumerExceptions : IntegrationFixture
    {
        private static readonly Exception TestException = new Exception("oops");

        protected async Task TestExceptionHandlingWith(IBasicConsumer consumer, Func<IChannel, string, IBasicConsumer, string, Task> action)
        {
            var resetEvent = new AutoResetEvent(false);
            bool notified = false;
            (string q, _, _) = await _channel.DeclareQueueAsync().ConfigureAwait(false);

            _channel.UnhandledExceptionOccurred += (ex, _) =>
            {
                if (ex != TestException)
                {
                    return;
                }

                notified = true;
                resetEvent.Set();
            };

            string tag = await _channel.ActivateConsumerAsync(consumer, q, true);
            await action(_channel, q, consumer, tag).ConfigureAwait(false);
            resetEvent.WaitOne();

            Assert.IsTrue(notified);
        }

        [SetUp]
        public override async Task Init()
        {
            _connFactory = new ConnectionFactory
            {
                DispatchConsumersAsync = true
            };

            _conn = _connFactory.CreateConnection();
            _channel = await _conn.CreateChannelAsync().ConfigureAwait(false);
        }

        [Test]
        public Task TestCancelNotificationExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnCancel(_channel);
            return TestExceptionHandlingWith(consumer, (ch, q, c, ct) => ch.DeleteQueueAsync(q).AsTask());
        }

        [Test]
        public Task TestConsumerCancelOkExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnCancelOk(_channel);
            return TestExceptionHandlingWith(consumer, (ch, q, c, ct) => ch.CancelConsumerAsync(ct).AsTask());
        }

        [Test]
        public Task TestConsumerConsumeOkExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnConsumeOk(_channel);
            return TestExceptionHandlingWith(consumer, (ch, q, c, ct) => Task.CompletedTask);
        }

        [Test]
        public Task TestConsumerShutdownExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnShutdown(_channel);
            return TestExceptionHandlingWith(consumer, (ch, q, c, ct) => ch.CloseAsync().AsTask());
        }

        [Test]
        public Task TestDeliveryExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnDelivery(_channel);
            return TestExceptionHandlingWith(consumer, (ch, q, c, ct) => ch.PublishMessageAsync("", q, null, _encoding.GetBytes("msg")).AsTask());
        }

        private class ConsumerFailingOnDelivery : AsyncEventingBasicConsumer
        {
            public ConsumerFailingOnDelivery(IChannel channel) : base(channel)
            {
            }

            public override Task HandleBasicDeliver(string consumerTag,
                ulong deliveryTag,
                bool redelivered,
                string exchange,
                string routingKey,
                IBasicProperties properties,
                ReadOnlyMemory<byte> body)
            {
                return Task.FromException(TestException);
            }
        }

        private class ConsumerFailingOnCancel : AsyncEventingBasicConsumer
        {
            public ConsumerFailingOnCancel(IChannel channel) : base(channel)
            {
            }

            public override Task HandleBasicCancel(string consumerTag)
            {
                return Task.FromException(TestException);
            }
        }

        private class ConsumerFailingOnShutdown : AsyncEventingBasicConsumer
        {
            public ConsumerFailingOnShutdown(IChannel channel) : base(channel)
            {
            }

            public override Task HandleModelShutdown(object channel, ShutdownEventArgs reason)
            {
                return Task.FromException(TestException);
            }
        }

        private class ConsumerFailingOnConsumeOk : AsyncEventingBasicConsumer
        {
            public ConsumerFailingOnConsumeOk(IChannel channel) : base(channel)
            {
            }

            public override Task HandleBasicConsumeOk(string consumerTag)
            {
                return Task.FromException(TestException);
            }
        }

        private class ConsumerFailingOnCancelOk : AsyncEventingBasicConsumer
        {
            public ConsumerFailingOnCancelOk(IChannel channel) : base(channel)
            {
            }

            public override Task HandleBasicCancelOk(string consumerTag)
            {
                return Task.FromException(TestException);
            }
        }
    }
}
