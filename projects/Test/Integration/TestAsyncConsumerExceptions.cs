// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
            : base(output,
                  consumerDispatchConcurrency: 1)
        {
        }

        protected override void DisposeAssertions()
        {
            /*
             * Note: don't do anything since these tests expect callback
             * exceptions
             */
        }

        [Fact]
        public Task TestCancelNotificationExceptionHandling()
        {
            IAsyncBasicConsumer consumer = new ConsumerFailingOnCancel(_channel);
            return TestExceptionHandlingWith(consumer, (ch, q, c, ct) => ch.QueueDeleteAsync(q, false, false));
        }

        [Fact]
        public Task TestConsumerCancelOkExceptionHandling()
        {
            IAsyncBasicConsumer consumer = new ConsumerFailingOnCancelOk(_channel);
            return TestExceptionHandlingWith(consumer, (ch, q, c, ct) => ch.BasicCancelAsync(ct));
        }

        [Fact]
        public Task TestConsumerConsumeOkExceptionHandling()
        {
            IAsyncBasicConsumer consumer = new ConsumerFailingOnConsumeOk(_channel);
            return TestExceptionHandlingWith(consumer, async (ch, q, c, ct) => await Task.Yield());
        }

        [Fact]
        public Task TestConsumerShutdownExceptionHandling()
        {
            IAsyncBasicConsumer consumer = new ConsumerFailingOnShutdown(_channel);
            return TestExceptionHandlingWith(consumer, (ch, q, c, ct) => ch.CloseAsync());
        }

        [Fact]
        public Task TestDeliveryExceptionHandling()
        {
            IAsyncBasicConsumer consumer = new ConsumerFailingOnDelivery(_channel);
            return TestExceptionHandlingWith(consumer, (ch, q, c, ct) =>
                ch.BasicPublishAsync("", q, _encoding.GetBytes("msg")).AsTask());
        }

        protected async Task TestExceptionHandlingWith(IAsyncBasicConsumer consumer,
            Func<IChannel, string, IAsyncBasicConsumer, string, Task> action)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cts = new CancellationTokenSource(ShortSpan);
            CancellationTokenRegistration ctsr = cts.Token.Register(() => tcs.TrySetCanceled());
            try
            {
                string q = await _channel.QueueDeclareAsync(string.Empty, false, true, false);
                _channel.CallbackExceptionAsync += (ch, evt) =>
                {
                    // _output.WriteLine($"[INFO] _channel.CallbackException: {evt.Exception}");
                    if (evt.Exception == TestException)
                    {
                        tcs.SetResult(true);
                    }

                    return Task.CompletedTask;
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

            public override Task HandleBasicDeliverAsync(string consumerTag,
                ulong deliveryTag,
                bool redelivered,
                string exchange,
                string routingKey,
                IReadOnlyBasicProperties properties,
                ReadOnlyMemory<byte> body,
                CancellationToken cancellationToken = default)
            {
                return Task.FromException(TestException);
            }
        }

        private class ConsumerFailingOnCancel : AsyncEventingBasicConsumer
        {
            public ConsumerFailingOnCancel(IChannel channel) : base(channel)
            {
            }

            public override Task HandleBasicCancelAsync(string consumerTag,
                CancellationToken cancellationToken = default)
            {
                return Task.FromException(TestException);
            }
        }

        private class ConsumerFailingOnShutdown : AsyncEventingBasicConsumer
        {
            public ConsumerFailingOnShutdown(IChannel channel) : base(channel)
            {
            }

            public override Task HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason)
            {
                return Task.FromException(TestException);
            }
        }

        private class ConsumerFailingOnConsumeOk : AsyncEventingBasicConsumer
        {
            public ConsumerFailingOnConsumeOk(IChannel channel) : base(channel)
            {
            }

            public override Task HandleBasicConsumeOkAsync(string consumerTag,
                CancellationToken cancellationToken = default)
            {
                return Task.FromException(TestException);
            }
        }

        private class ConsumerFailingOnCancelOk : AsyncEventingBasicConsumer
        {
            public ConsumerFailingOnCancelOk(IChannel channel) : base(channel)
            {
            }

            public override Task HandleBasicCancelOkAsync(string consumerTag,
                CancellationToken cancellationToken = default)
            {
                return Task.FromException(TestException);
            }
        }
    }
}
