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

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    internal class TestConsumerOperationDispatch : IntegrationFixture
    {
        private readonly string _x = "dotnet.tests.consumer-operation-dispatch.fanout";
        private readonly List<IModel> _channels = new List<IModel>();
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
            foreach (IModel ch in _channels)
            {
                if (ch.IsOpen)
                {
                    ch.Close();
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

            public CollectingConsumer(IModel model)
                : base(model)
            {
                DeliveryTags = new List<ulong>();
            }

            public override void HandleBasicDeliver(string consumerTag,
                ulong deliveryTag, bool redelivered, string exchange, string routingKey,
                IBasicProperties properties, ReadOnlyMemory<byte> body)
            {
                // we test concurrent dispatch from the moment basic.delivery is returned.
                // delivery tags have guaranteed ordering and we verify that it is preserved
                // (per-channel) by the concurrent dispatcher.
                DeliveryTags.Add(deliveryTag);

                if (deliveryTag == N)
                {
                    counter.Signal();
                }

                Model.BasicAck(deliveryTag: deliveryTag, multiple: false);
            }
        }

        [Test]
        public void TestDeliveryOrderingWithSingleChannel()
        {
            IModel Ch = _conn.CreateModel();
            Ch.ExchangeDeclare(_x, "fanout", durable: false);

            for (int i = 0; i < Y; i++)
            {
                IModel ch = _conn.CreateModel();
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
                Ch.BasicPublish(exchange: _x, routingKey: "",
                    basicProperties: new BasicProperties(),
                    body: _encoding.GetBytes("msg"));
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
        public void TestChannelShutdownDoesNotShutDownDispatcher()
        {
            IModel ch1 = _conn.CreateModel();
            IModel ch2 = _conn.CreateModel();
            _model.ExchangeDeclare(_x, "fanout", durable: false);

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

            ch2.BasicPublish(exchange: _x, basicProperties: null, body: _encoding.GetBytes("msg"), routingKey: "");
            Wait(latch);
        }

        [Test]
        public async Task TestConsumerExceptionRaisesEventInModel()
        {
            var tcs = new TaskCompletionSource<CallbackExceptionEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (var ch1 = _conn.CreateModel())
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            using (cts.Token.Register(() => tcs.SetCanceled()))
            {
                ch1.CallbackException += (sender, args) => tcs.TrySetResult(args);

                string q1 = ch1.QueueDeclare().QueueName;

                var errorMsg = "boom";
                var c1 = new EventingBasicConsumer(ch1);
                c1.Received += (object sender, BasicDeliverEventArgs e) =>
                {
                    throw new Exception(errorMsg);
                };
                ch1.BasicConsume(q1, true, c1);
                ch1.BasicPublish("", q1, body: _encoding.GetBytes("msg"));

                var error = await tcs.Task;

                Assert.AreEqual(errorMsg, error.Exception.Message);
                Assert.AreEqual(c1, error.Detail[CallbackExceptionEventArgs.Consumer]);
                Assert.AreEqual(nameof(Impl.IConsumerDispatcher.HandleBasicDeliver), error.Detail[CallbackExceptionEventArgs.Context]);
            }
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
        public void TestModelShutdownHandler()
        {
            var latch = new ManualResetEventSlim(false);
            var duplicateLatch = new ManualResetEventSlim(false);
            string q = _model.QueueDeclare().QueueName;
            var c = new ShutdownLatchConsumer(latch, duplicateLatch);

            _model.BasicConsume(queue: q, autoAck: true, consumer: c);
            _model.Close();
            Wait(latch, TimeSpan.FromSeconds(5));
            Assert.IsFalse(duplicateLatch.Wait(TimeSpan.FromSeconds(5)),
                           "event handler fired more than once");
        }
    }
}
