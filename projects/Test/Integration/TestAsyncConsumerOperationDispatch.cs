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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestAsyncConsumerOperationDispatch : IntegrationFixture
    {
        // number of channels (and consumers)
        private const int Y = 100;
        // number of messages to be published
        private const int N = 100;

        private static readonly CountdownEvent s_counter = new CountdownEvent(Y);

        private const string _x = "dotnet.tests.consumer-operation-dispatch.fanout";
        private readonly List<IChannel> _channels = new List<IChannel>();
        private readonly List<string> _queues = new List<string>();
        private readonly List<CollectingConsumer> _consumers = new List<CollectingConsumer>();


        public TestAsyncConsumerOperationDispatch(ITestOutputHelper output) : base(output)
        {
        }

        public override async Task DisposeAsync()
        {
            foreach (IChannel ch in _channels)
            {
                if (ch.IsOpen)
                {
                    await ch.CloseAsync();
                }
            }

            s_counter.Reset();

            await base.DisposeAsync();
        }

        private class CollectingConsumer : AsyncDefaultBasicConsumer
        {
            public List<ulong> DeliveryTags { get; }

            public CollectingConsumer(IChannel channel)
                : base(channel)
            {
                DeliveryTags = new List<ulong>();
            }

            public override Task HandleBasicDeliverAsync(string consumerTag,
                ulong deliveryTag, bool redelivered, string exchange, string routingKey,
                IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body,
                CancellationToken cancellationToken = default)
            {
                // we test concurrent dispatch from the moment basic.delivery is returned.
                // delivery tags have guaranteed ordering and we verify that it is preserved
                // (per-channel) by the concurrent dispatcher.
                DeliveryTags.Add(deliveryTag);

                if (deliveryTag == N)
                {
                    s_counter.Signal();
                }

                return Channel.BasicAckAsync(deliveryTag: deliveryTag, multiple: false,
                    cancellationToken: cancellationToken).AsTask();
            }
        }

        [SkippableFact]
        public async Task TestDeliveryOrderingWithSingleChannel()
        {
            Skip.If(IntegrationFixture.IsRunningInCI && IntegrationFixture.IsWindows, "TODO - test is slow in CI on Windows");

            await _channel.ExchangeDeclareAsync(_x, "fanout", durable: false);

            for (int i = 0; i < Y; i++)
            {
                IChannel ch = await _conn.CreateChannelAsync();
                QueueDeclareOk q = await ch.QueueDeclareAsync("", durable: false, exclusive: true, autoDelete: true, arguments: null);
                await ch.QueueBindAsync(queue: q, exchange: _x, routingKey: "");
                _channels.Add(ch);
                _queues.Add(q);
                var cons = new CollectingConsumer(ch);
                _consumers.Add(cons);
                await ch.BasicConsumeAsync(q, false, cons);
            }

            for (int i = 0; i < N; i++)
            {
                await _channel.BasicPublishAsync(_x, "", _encoding.GetBytes("msg"));
            }

            if (IntegrationFixture.IsRunningInCI)
            {
                s_counter.Wait(TimeSpan.FromMinutes(5));
            }
            else
            {
                s_counter.Wait(TimeSpan.FromMinutes(2));
            }

            foreach (CollectingConsumer cons in _consumers)
            {
                Assert.Equal(N, cons.DeliveryTags.Count);
                ulong[] ary = cons.DeliveryTags.ToArray();
                Assert.Equal(1u, ary[0]);
                Assert.Equal((ulong)N, ary[N - 1]);
                for (int i = 0; i < (N - 1); i++)
                {
                    ulong a = ary[i];
                    ulong b = ary[i + 1];

                    Assert.True(a < b);
                }
            }
        }

        // see rabbitmq/rabbitmq-dotnet-client#61
        [Fact]
        public async Task TestChannelShutdownDoesNotShutDownDispatcher()
        {
            await _channel.ExchangeDeclareAsync(_x, "fanout", durable: false);

            IChannel ch1 = await _conn.CreateChannelAsync();
            IChannel ch2 = await _conn.CreateChannelAsync();

            string q1 = (await ch1.QueueDeclareAsync()).QueueName;
            string q2 = (await ch2.QueueDeclareAsync()).QueueName;
            await ch2.QueueBindAsync(queue: q2, exchange: _x, routingKey: "");

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await ch1.BasicConsumeAsync(q1, true, new AsyncEventingBasicConsumer(ch1));
            var c2 = new AsyncEventingBasicConsumer(ch2);
            c2.ReceivedAsync += (object sender, BasicDeliverEventArgs e) =>
            {
                tcs.SetResult(true);
                return Task.CompletedTask;
            };
            await ch2.BasicConsumeAsync(q2, true, c2);
            // closing this channel must not affect ch2
            await ch1.CloseAsync();

            await ch2.BasicPublishAsync(_x, "", _encoding.GetBytes("msg"));
            await WaitAsync(tcs, "received event");
        }

        private class ShutdownLatchConsumer : AsyncDefaultBasicConsumer
        {
            public ShutdownLatchConsumer(IChannel channel) : base(channel)
            {
                Latch = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                DuplicateLatch = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public readonly TaskCompletionSource<bool> Latch;
            public readonly TaskCompletionSource<bool> DuplicateLatch;

            public override Task HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason)
            {
                // keep track of duplicates
                if (Latch.Task.IsCompletedSuccessfully())
                {
                    DuplicateLatch.SetResult(true);
                }
                else
                {
                    Latch.SetResult(true);
                }

                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task TestChannelShutdownHandler()
        {
            string q = await _channel.QueueDeclareAsync();
            var consumer = new ShutdownLatchConsumer(_channel);

            await _channel.BasicConsumeAsync(q, true, consumer);
            await _channel.CloseAsync();

            await consumer.Latch.Task.WaitAsync(ShortSpan);
            Assert.True(consumer.Latch.Task.IsCompletedSuccessfully());

            await Assert.ThrowsAsync<TimeoutException>(() =>
            {
                return consumer.DuplicateLatch.Task.WaitAsync(ShortSpan);
            });

            Assert.False(consumer.DuplicateLatch.Task.IsCompletedSuccessfully());
        }
    }
}
