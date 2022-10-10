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
using System.Threading.Tasks;
using RabbitMQ.Client;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestConsumerExceptions : IntegrationFixture
    {
        private class ConsumerFailingOnDelivery : DefaultBasicConsumer
        {
            public ConsumerFailingOnDelivery(IChannel channel) : base(channel)
            {
            }

            public override Task HandleBasicDeliverAsync(string consumerTag,
                ulong deliveryTag,
                bool redelivered,
                string exchange,
                string routingKey,
                ReadOnlyBasicProperties properties,
                ReadOnlyMemory<byte> body)
            {
                throw new Exception("oops");
            }
        }

        private class ConsumerFailingOnCancel : DefaultBasicConsumer
        {
            public ConsumerFailingOnCancel(IChannel channel) : base(channel)
            {
            }

            public override void HandleBasicCancel(string consumerTag)
            {
                throw new Exception("oops");
            }
        }

        private class ConsumerFailingOnShutdown : DefaultBasicConsumer
        {
            public ConsumerFailingOnShutdown(IChannel channel) : base(channel)
            {
            }

            public override void HandleChannelShutdown(object channel, ShutdownEventArgs reason)
            {
                throw new Exception("oops");
            }
        }

        private class ConsumerFailingOnConsumeOk : DefaultBasicConsumer
        {
            public ConsumerFailingOnConsumeOk(IChannel channel) : base(channel)
            {
            }

            public override void HandleBasicConsumeOk(string consumerTag)
            {
                throw new Exception("oops");
            }
        }

        private class ConsumerFailingOnCancelOk : DefaultBasicConsumer
        {
            public ConsumerFailingOnCancelOk(IChannel channel) : base(channel)
            {
            }

            public override void HandleBasicCancelOk(string consumerTag)
            {
                throw new Exception("oops");
            }
        }

        protected async Task TestExceptionHandlingWithAsync(IBasicConsumer consumer,
            Func<IChannel, string, IBasicConsumer, string, Task> action)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool notified = false;
            string q = await _channel.QueueDeclareAsync();

            _channel.CallbackException += (m, evt) =>
            {
                notified = true;
                tcs.SetResult(true);
            };

            string tag = await _channel.BasicConsumeAsync(q, true, consumer);
            await action(_channel, q, consumer, tag);
            await WaitAsync(tcs, "callback exception");

            Assert.True(notified);
        }

        public TestConsumerExceptions(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public Task TestCancelNotificationExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnCancel(_channel);
            return TestExceptionHandlingWithAsync(consumer, (ch, q, c, ct) =>
            {
                return ch.QueueDeleteAsync(q);
            });
        }

        [Fact]
        public Task TestConsumerCancelOkExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnCancelOk(_channel);
            return TestExceptionHandlingWithAsync(consumer, (ch, q, c, ct) => ch.BasicCancelAsync(ct));
        }

        [Fact]
        public Task TestConsumerConsumeOkExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnConsumeOk(_channel);
            return TestExceptionHandlingWithAsync(consumer, (ch, q, c, ct) => Task.CompletedTask);
        }

        [Fact]
        public Task TestConsumerShutdownExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnShutdown(_channel);
            return TestExceptionHandlingWithAsync(consumer, (ch, q, c, ct) => ch.CloseAsync());
        }

        [Fact]
        public Task TestDeliveryExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnDelivery(_channel);
            return TestExceptionHandlingWithAsync(consumer, (ch, q, c, ct) =>
                ch.BasicPublishAsync("", q, _encoding.GetBytes("msg")).AsTask());
        }
    }
}
