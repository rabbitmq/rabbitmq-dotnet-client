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
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestAsyncConsumerExceptions : IntegrationFixture
    {
        protected void TestExceptionHandlingWith(IBasicConsumer consumer,
            Action<IModel, string, IBasicConsumer, string> action)
        {
            object o = new object();
            bool notified = false;
            string q = _model.QueueDeclare();


            _model.CallbackException += (m, evt) =>
            {
                notified = true;
                Monitor.PulseAll(o);
            };

            string tag = _model.BasicConsume(q, true, consumer);
            action(_model, q, consumer, tag);
            WaitOn(o);

            Assert.IsTrue(notified);
        }

        [SetUp]
        public override void Init()
        {
            _connFactory = new ConnectionFactory
            {
                DispatchConsumersAsync = true
            };

            _conn = _connFactory.CreateConnection();
            _model = _conn.CreateModel();
        }

        [Test]
        public void TestCancelNotificationExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnCancel(_model);
            TestExceptionHandlingWith(consumer, (m, q, c, ct) => m.QueueDelete(q));
        }

        [Test]
        public void TestConsumerCancelOkExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnCancelOk(_model);
            TestExceptionHandlingWith(consumer, (m, q, c, ct) => m.BasicCancel(ct));
        }

        [Test]
        public void TestConsumerConsumeOkExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnConsumeOk(_model);
            TestExceptionHandlingWith(consumer, (m, q, c, ct) => { });
        }

        [Test]
        public void TestConsumerShutdownExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnShutdown(_model);
            TestExceptionHandlingWith(consumer, (m, q, c, ct) => m.Close());
        }

        [Test]
        public void TestDeliveryExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnDelivery(_model);
            TestExceptionHandlingWith(consumer, (m, q, c, ct) => m.BasicPublish("", q, null, _encoding.GetBytes("msg")));
        }

        private class ConsumerFailingOnDelivery : AsyncEventingBasicConsumer
        {
            public ConsumerFailingOnDelivery(IModel model) : base(model)
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
                return Task.FromException(new Exception("oops"));
            }
        }

        private class ConsumerFailingOnCancel : AsyncEventingBasicConsumer
        {
            public ConsumerFailingOnCancel(IModel model) : base(model)
            {
            }

            public override Task HandleBasicCancel(string consumerTag)
            {
                return Task.FromException(new Exception("oops"));
            }
        }

        private class ConsumerFailingOnShutdown : AsyncEventingBasicConsumer
        {
            public ConsumerFailingOnShutdown(IModel model) : base(model)
            {
            }

            public override Task HandleModelShutdown(object model, ShutdownEventArgs reason)
            {
                return Task.FromException(new Exception("oops"));
            }
        }

        private class ConsumerFailingOnConsumeOk : AsyncEventingBasicConsumer
        {
            public ConsumerFailingOnConsumeOk(IModel model) : base(model)
            {
            }

            public override Task HandleBasicConsumeOk(string consumerTag)
            {
                return Task.FromException(new Exception("oops"));
            }
        }

        private class ConsumerFailingOnCancelOk : AsyncEventingBasicConsumer
        {
            public ConsumerFailingOnCancelOk(IModel model) : base(model)
            {
            }

            public override Task HandleBasicCancelOk(string consumerTag)
            {
                return Task.FromException(new Exception("oops"));
            }
        }
    }
}
