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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RabbitMQ.Client.client.impl.Channel;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    internal class TestConsumerOperationDispatch : IntegrationFixture
    {
        private readonly string _x = "dotnet.tests.consumer-operation-dispatch.fanout";
        private readonly List<IChannel> _channels = new List<IChannel>();
        private readonly List<string> _queues = new List<string>();
        private readonly List<CollectingConsumer> _consumers = new List<CollectingConsumer>();

        // number of channels (and consumers)
        private const int Y = 100;

        // number of messages to be published
        private const int N = 100;

        public static CountdownEvent counter = new CountdownEvent(Y);

        [TearDown]
        protected override void ReleaseResources()
        {
            foreach (IChannel ch in _channels)
            {
                if (ch.IsOpen)
                {
                    ch.CloseAsync().GetAwaiter().GetResult();
                }
            }
            _queues.Clear();
            _consumers.Clear();
            counter.Reset();
            base.ReleaseResources();
        }

        private class CollectingConsumer : DefaultBasicConsumer
        {
            public List<ulong> DeliveryTags { get; }

            public CollectingConsumer(IChannel channel)
                : base(channel)
            {
                DeliveryTags = new List<ulong>();
            }

            public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
            {
                // we test concurrent dispatch from the moment basic.delivery is returned.
                // delivery tags have guaranteed ordering and we verify that it is preserved
                // (per-channel) by the concurrent dispatcher.
                DeliveryTags.Add(deliveryTag);

                if (deliveryTag == N)
                {
                    counter.Signal();
                }

                Channel.AckMessageAsync(deliveryTag, false).GetAwaiter().GetResult();
            }
        }

        [Test]
        public async Task TestDeliveryOrderingWithSingleChannel()
        {
            IChannel Ch = await _conn.CreateChannelAsync().ConfigureAwait(false);
            await Ch.DeclareExchangeAsync(_x, "fanout", false, false).ConfigureAwait(false);

            for (int i = 0; i < Y; i++)
            {
                IChannel ch = await _conn.CreateChannelAsync().ConfigureAwait(false);
                (string q, _, _) = await ch.DeclareQueueAsync("", false, true, true).ConfigureAwait(false);
                await ch.BindQueueAsync(q, _x, "").ConfigureAwait(false);
                _channels.Add(ch);
                _queues.Add(q);
                var cons = new CollectingConsumer(ch);
                _consumers.Add(cons);
                await ch.ActivateConsumerAsync(cons, q, false).ConfigureAwait(false);
            }

            byte[] body = _encoding.GetBytes("msg");
            for (int i = 0; i < N; i++)
            {
                await Ch.PublishMessageAsync(_x, "", null, body).ConfigureAwait(false);
            }
            counter.Wait(TimeSpan.FromSeconds(120));

            foreach (CollectingConsumer cons in _consumers)
            {
                Assert.That(cons.DeliveryTags, Has.Count.EqualTo(N));
                ulong[] ary = cons.DeliveryTags.ToArray();
                Assert.AreEqual(ary[0], 1);
                Assert.AreEqual(ary[N - 1], N);
                for (int i = 0; i < (N - 1); i++)
                {
                    ulong a = ary[i];
                    ulong b = ary[i + 1];

                    Assert.IsTrue(a < b);
                }
            }
        }

        // see rabbitmq/rabbitmq-dotnet-client#61
        [Test]
        public async Task TestChannelShutdownDoesNotShutDownDispatcher()
        {
            IChannel ch1 = await _conn.CreateChannelAsync().ConfigureAwait(false);
            IChannel ch2 = await _conn.CreateChannelAsync().ConfigureAwait(false);
            await _channel.DeclareExchangeAsync(_x, "fanout").ConfigureAwait(false);

            (string q1, _, _) = await ch1.DeclareQueueAsync().ConfigureAwait(false);
            (string q2, _, _) = await ch2.DeclareQueueAsync().ConfigureAwait(false);
            await ch2.BindQueueAsync(q2, _x, "").ConfigureAwait(false);

            var latch = new ManualResetEventSlim(false);
            await ch1.ActivateConsumerAsync(new EventingBasicConsumer(ch1), q1, true);
            var c2 = new EventingBasicConsumer(ch2);
            c2.Received += (object sender, BasicDeliverEventArgs e) =>
            {
                latch.Set();
            };
            await ch2.ActivateConsumerAsync(c2, q2, true).ConfigureAwait(false);
            // closing this channel must not affect ch2
            await ch1.CloseAsync().ConfigureAwait(false);

            await ch2.PublishMessageAsync(_x, "", null, _encoding.GetBytes("msg"));
            Wait(latch);
        }

        private class ShutdownLatchConsumer : DefaultBasicConsumer
        {
            public ManualResetEventSlim Latch { get; }
            public ManualResetEventSlim DuplicateLatch { get; }

            public ShutdownLatchConsumer(ManualResetEventSlim latch, ManualResetEventSlim duplicateLatch)
            {
                Latch = latch;
                DuplicateLatch = duplicateLatch;
            }

            public override void HandleModelShutdown(object model, ShutdownEventArgs reason)
            {
                // keep track of duplicates
                if (Latch.Wait(0)){
                    DuplicateLatch.Set();
                } else {
                    Latch.Set();
                }
            }
        }

        [Test]
        public async Task TestModelShutdownHandler()
        {
            var latch = new ManualResetEventSlim(false);
            var duplicateLatch = new ManualResetEventSlim(false);
            (string q, _, _) = await _channel.DeclareQueueAsync().ConfigureAwait(false);
            var c = new ShutdownLatchConsumer(latch, duplicateLatch);

            await _channel.ActivateConsumerAsync(c, q, true).ConfigureAwait(false);
            await _channel.CloseAsync().ConfigureAwait(false);
            Wait(latch, TimeSpan.FromSeconds(5));
            Assert.IsFalse(duplicateLatch.Wait(TimeSpan.FromSeconds(5)), "event handler fired more than once");
        }
    }
}
