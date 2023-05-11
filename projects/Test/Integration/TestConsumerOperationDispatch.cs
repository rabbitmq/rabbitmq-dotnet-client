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
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestConsumerOperationDispatch : IntegrationFixture
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


        public TestConsumerOperationDispatch(ITestOutputHelper output) : base(output)
        {
        }

        protected override void TearDown()
        {
            foreach (IChannel ch in _channels)
            {
                if (ch.IsOpen)
                {
                    ch.Close();
                }
            }

            s_counter.Reset();
        }

        private class CollectingConsumer : DefaultBasicConsumer
        {
            public List<ulong> DeliveryTags { get; }

            public CollectingConsumer(IChannel channel)
                : base(channel)
            {
                DeliveryTags = new List<ulong>();
            }

            public override void HandleBasicDeliver(string consumerTag,
                ulong deliveryTag, bool redelivered, string exchange, string routingKey,
                in ReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body)
            {
                // we test concurrent dispatch from the moment basic.delivery is returned.
                // delivery tags have guaranteed ordering and we verify that it is preserved
                // (per-channel) by the concurrent dispatcher.
                DeliveryTags.Add(deliveryTag);

                if (deliveryTag == N)
                {
                    s_counter.Signal();
                }

                Channel.BasicAck(deliveryTag: deliveryTag, multiple: false);
            }
        }

        [SkippableFact]
        public void TestDeliveryOrderingWithSingleChannel()
        {
            Skip.If(IntegrationFixture.IsRunningInCI && IntegrationFixtureBase.IsWindows, "TODO - test is slow in CI on Windows");

            _channel.ExchangeDeclare(_x, "fanout", durable: false);

            for (int i = 0; i < Y; i++)
            {
                IChannel ch = _conn.CreateChannel();
                QueueDeclareOk q = ch.QueueDeclare("", durable: false, exclusive: true, autoDelete: true, arguments: null);
                ch.QueueBind(queue: q, exchange: _x, routingKey: "");
                _channels.Add(ch);
                _queues.Add(q);
                var cons = new CollectingConsumer(ch);
                _consumers.Add(cons);
                ch.BasicConsume(queue: q, autoAck: false, consumer: cons);
            }

            for (int i = 0; i < N; i++)
            {
                _channel.BasicPublish(_x, "", _encoding.GetBytes("msg"));
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
        public void TestChannelShutdownDoesNotShutDownDispatcher()
        {
            _channel.ExchangeDeclare(_x, "fanout", durable: false);

            IChannel ch1 = _conn.CreateChannel();
            IChannel ch2 = _conn.CreateChannel();

            string q1 = ch1.QueueDeclare().QueueName;
            string q2 = ch2.QueueDeclare().QueueName;
            ch2.QueueBind(queue: q2, exchange: _x, routingKey: "");

            var latch = new ManualResetEventSlim(false);
            ch1.BasicConsume(q1, true, new EventingBasicConsumer(ch1));
            var c2 = new EventingBasicConsumer(ch2);
            c2.Received += (object sender, BasicDeliverEventArgs e) =>
            {
                latch.Set();
            };
            ch2.BasicConsume(q2, true, c2);
            // closing this channel must not affect ch2
            ch1.Close();

            ch2.BasicPublish(_x, "", _encoding.GetBytes("msg"));
            Wait(latch, "received event");
        }

        private class ShutdownLatchConsumer : DefaultBasicConsumer
        {
            public ShutdownLatchConsumer()
            {
                Latch = new();
                DuplicateLatch = new();
            }

            public readonly ManualResetEventSlim Latch;
            public readonly ManualResetEventSlim DuplicateLatch;

            public override void HandleChannelShutdown(object channel, ShutdownEventArgs reason)
            {
                // keep track of duplicates
                if (Latch.Wait(0))
                {
                    DuplicateLatch.Set();
                }
                else
                {
                    Latch.Set();
                }
            }
        }

        [Fact]
        public void TestChannelShutdownHandler()
        {
            string q = _channel.QueueDeclare();
            var c = new ShutdownLatchConsumer();

            _channel.BasicConsume(queue: q, autoAck: true, consumer: c);
            _channel.Close();

            Wait(c.Latch, TimeSpan.FromSeconds(5), "channel shutdown");
            Assert.False(c.DuplicateLatch.Wait(TimeSpan.FromSeconds(5)), "event handler fired more than once");
        }
    }
}
