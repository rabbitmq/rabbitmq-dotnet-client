// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2015 Pivotal Software, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at http://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    internal class TestConsumerOperationDispatch : IntegrationFixture
    {
        private string x = "dotnet.tests.consumer-operation-dispatch.fanout";
        private List<IModel> channels = new List<IModel>();
        private List<string> queues = new List<string>();
        private List<CollectingConsumer> consumers = new List<CollectingConsumer>();

        // number of channels (and consumers)
        private const int y = 200;

        // number of messages to be published
        private const int n = 250;

        public static CountdownEvent counter = new CountdownEvent(y);

        [TearDown]
        protected override void ReleaseResources()
        {
            base.ReleaseResources();
            foreach (var ch in channels)
            {
                if (ch.IsOpen)
                {
                    ch.Close();
                }
            }
            queues.Clear();
            consumers.Clear();
            counter.Reset();
        }

        private class CollectingConsumer : DefaultBasicConsumer
        {
            public List<ulong> DeliveryTags { get; private set; }

            public CollectingConsumer(IModel model)
                : base(model)
            {
                this.DeliveryTags = new List<ulong>();
            }

            public override void HandleBasicDeliver(string consumerTag,
                ulong deliveryTag, bool redelivered, string exchange, string routingKey,
                IBasicProperties properties, byte[] body)
            {
                // we test concurrent dispatch from the moment basic.delivery is returned.
                // delivery tags have guaranteed ordering and we verify that it is preserved
                // (per-channel) by the concurrent dispatcher.
                this.DeliveryTags.Add(deliveryTag);

                if (deliveryTag == n)
                {
                    counter.Signal();
                }

                this.Model.BasicAck(deliveryTag: deliveryTag, multiple: false);
            }
        }

        [Test]
        public void TestDeliveryOrderingWithSingleChannel()
        {
            var Ch = Conn.CreateModel();
            Ch.ExchangeDeclare(x, "fanout", durable: false);

            for (int i = 0; i < y; i++)
            {
                var ch = Conn.CreateModel();
                var q = ch.QueueDeclare("", durable: false, exclusive: true, autoDelete: true, arguments: null);
                ch.QueueBind(queue: q, exchange: x, routingKey: "");
                channels.Add(ch);
                queues.Add(q);
                var cons = new CollectingConsumer(ch);
                consumers.Add(cons);
                ch.BasicConsume(queue: q, noAck: false, consumer: cons);
            }

            for (int i = 0; i < n; i++)
            {
                Ch.BasicPublish(exchange: x, routingKey: "",
                    basicProperties: new BasicProperties(),
                    body: encoding.GetBytes("msg"));
            }
            counter.Wait(TimeSpan.FromSeconds(30));

            foreach (var cons in consumers)
            {
                Assert.That(cons.DeliveryTags, Has.Count.EqualTo(n));
                var ary = cons.DeliveryTags.ToArray();
                Assert.AreEqual(ary[0], 1);
                Assert.AreEqual(ary[n - 1], n);
                for (int i = 0; i < (n - 1); i++)
                {
                    var a = ary[i];
                    var b = ary[i + 1];

                    Assert.IsTrue(a < b);
                }
            }
        }

        // see rabbitmq/rabbitmq-dotnet-client#61
        [Test]
        public void TestChannelShutdownDoesNotShutDownDispatcher()
        {
            var ch1 = Conn.CreateModel();
            var ch2 = Conn.CreateModel();
            Model.ExchangeDeclare(x, "fanout", durable: false);

            var q1 = ch1.QueueDeclare().QueueName;
            var q2 = ch2.QueueDeclare().QueueName;
            ch2.QueueBind(queue: q2, exchange: x, routingKey: "");

            var latch = new ManualResetEvent(false);
            ch1.BasicConsume(q1, true, new EventingBasicConsumer(ch1));
            var c2 = new EventingBasicConsumer(ch2);
            c2.Received += (object sender, BasicDeliverEventArgs e) =>
            {
                latch.Set();
            };
            ch2.BasicConsume(q2, true, c2);
            // closing this channel must not affect ch2
            ch1.Close();

            ch2.BasicPublish(exchange: x, basicProperties: null, body: encoding.GetBytes("msg"), routingKey: "");
            Wait(latch);
        }

        private class ShutdownLatchConsumer : DefaultBasicConsumer
        {
            public ManualResetEvent Latch { get; private set; }

            public ShutdownLatchConsumer(ManualResetEvent latch)
            {
                this.Latch = latch;
            }

            public override void HandleModelShutdown(object model, ShutdownEventArgs reason)
            {
                this.Latch.Set();
            }
        }

        [Test]
        public void TestModelShutdownHandler()
        {
            var latch = new ManualResetEvent(false);
            var q = this.Model.QueueDeclare().QueueName;
            var c = new ShutdownLatchConsumer(latch);

            this.Model.BasicConsume(queue: q, noAck: true, consumer: c);
            this.Model.Close();
            Wait(latch, TimeSpan.FromSeconds(5));
        }
    }
}
