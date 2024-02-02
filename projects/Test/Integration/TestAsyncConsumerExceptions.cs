﻿// This source code is dual-licensed under the Apache License, version
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
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestAsyncConsumerExceptions : IntegrationFixture
    {
        private static readonly Exception TestException = new Exception("oops");

        public TestAsyncConsumerExceptions(ITestOutputHelper output)
            : base(output, dispatchConsumersAsync: true, consumerDispatchConcurrency: 1)
        {
        }

        [Fact]
        public Task TestCancelNotificationExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnCancel(_channel);
            return TestExceptionHandlingWith(consumer, async (ch, q, c, ct) =>
            {
                await ch.QueueDeleteAsync(q, false, false);
            });
        }

        [Fact]
        public Task TestConsumerCancelOkExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnCancelOk(_channel);
            return TestExceptionHandlingWith(consumer, (ch, q, c, ct) => ch.BasicCancelAsync(ct));
        }

        [Fact]
        public Task TestConsumerConsumeOkExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnConsumeOk(_channel);
            return TestExceptionHandlingWith(consumer, async (ch, q, c, ct) => await Task.Yield());
        }

        [Fact]
        public Task TestConsumerShutdownExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnShutdown(_channel);
            return TestExceptionHandlingWith(consumer, (ch, q, c, ct) => ch.CloseAsync());
        }

        [Fact]
        public Task TestDeliveryExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnDelivery(_channel);
            return TestExceptionHandlingWith(consumer, (ch, q, c, ct) =>
                ch.BasicPublishAsync("", q, _encoding.GetBytes("msg")).AsTask());
        }

        protected async Task TestExceptionHandlingWith(IBasicConsumer consumer,
            Func<IChannel, string, IBasicConsumer, string, Task> action)
        {
            var waitSpan = TimeSpan.FromSeconds(5);
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cts = new CancellationTokenSource(waitSpan);
            CancellationTokenRegistration ctsr = cts.Token.Register(() => tcs.TrySetResult(false));
            try
            {
                string q = await _channel.QueueDeclareAsync(string.Empty, false, true, false);
                _channel.CallbackException += (ch, evt) =>
                {
                    if (evt.Exception == TestException)
                    {
                        tcs.SetResult(true);
                    }
                };

                string tag = await _channel.BasicConsumeAsync(q, true, string.Empty, false, false, null, consumer);
                await action(_channel, q, consumer, tag);
                Assert.True(await tcs.Task);
            }
            finally
            {
                cts.Dispose();
                ctsr.Dispose();
            }
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
                in ReadOnlyBasicProperties properties,
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

            public override Task HandleChannelShutdown(object channel, ShutdownEventArgs reason)
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
