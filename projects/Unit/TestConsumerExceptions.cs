// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
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
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at https://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestConsumerExceptions : IntegrationFixture
    {
        private class ConsumerFailingOnDelivery : DefaultBasicConsumer
        {
            public ConsumerFailingOnDelivery(IModel model) : base(model)
            {
            }

            public override ValueTask HandleBasicDeliver(string consumerTag,
                ulong deliveryTag,
                bool redelivered,
                string exchange,
                string routingKey,
                IBasicProperties properties,
                ReadOnlyMemory<byte> body)
            {
                throw new Exception("oops");
            }
        }

        private class ConsumerFailingOnCancel : DefaultBasicConsumer
        {
            public ConsumerFailingOnCancel(IModel model) : base(model)
            {
            }

            public override ValueTask HandleBasicCancel(string consumerTag)
            {
                throw new Exception("oops");
            }
        }

        private class ConsumerFailingOnShutdown : DefaultBasicConsumer
        {
            public ConsumerFailingOnShutdown(IModel model) : base(model)
            {
            }

            public override ValueTask HandleModelShutdown(object model, ShutdownEventArgs reason)
            {
                throw new Exception("oops");
            }
        }

        private class ConsumerFailingOnConsumeOk : DefaultBasicConsumer
        {
            public ConsumerFailingOnConsumeOk(IModel model) : base(model)
            {
            }

            public override ValueTask HandleBasicConsumeOk(string consumerTag)
            {
                throw new Exception("oops");
            }
        }

        private class ConsumerFailingOnCancelOk : DefaultBasicConsumer
        {
            public ConsumerFailingOnCancelOk(IModel model) : base(model)
            {
            }

            public override ValueTask HandleBasicCancelOk(string consumerTag)
            {
                throw new Exception("oops");
            }
        }

        protected async ValueTask TestExceptionHandlingWith(IAsyncBasicConsumer consumer,
            Func<IModel, string, IAsyncBasicConsumer, string, ValueTask> action)
        {
            object o = new object();
            bool notified = false;
            string q = await Model.QueueDeclare();


            Model.CallbackException += (m, evt) =>
            {
                notified = true;
                Monitor.PulseAll(o);
            };

            string tag = await Model.BasicConsume(q, true, consumer);
            await action(Model, q, consumer, tag);
            WaitOn(o);

            Assert.IsTrue(notified);
        }

        [Test]
        public async ValueTask TestCancelNotificationExceptionHandling()
        {
            IAsyncBasicConsumer consumer = new ConsumerFailingOnCancel(Model);
            await TestExceptionHandlingWith(consumer, async (m, q, c, ct) => await m.QueueDelete(q));
        }

        [Test]
        public async ValueTask TestConsumerCancelOkExceptionHandling()
        {
            IAsyncBasicConsumer consumer = new ConsumerFailingOnCancelOk(Model);
            await TestExceptionHandlingWith(consumer, async (m, q, c, ct) => await m.BasicCancel(ct));
        }

        [Test]
        public async ValueTask TestConsumerConsumeOkExceptionHandling()
        {
            IAsyncBasicConsumer consumer = new ConsumerFailingOnConsumeOk(Model);
            await TestExceptionHandlingWith(consumer, (m, q, c, ct) => { return default; });
        }

        [Test]
        public async ValueTask TestConsumerShutdownExceptionHandling()
        {
            IAsyncBasicConsumer consumer = new ConsumerFailingOnShutdown(Model);
            await TestExceptionHandlingWith(consumer, async (m, q, c, ct) => { await m.Close(); });
        }

        [Test]
        public async ValueTask TestDeliveryExceptionHandling()
        {
            IAsyncBasicConsumer consumer = new ConsumerFailingOnDelivery(Model);
            await TestExceptionHandlingWith(consumer, async (m, q, c, ct) => { await m.BasicPublish("", q, null, encoding.GetBytes("msg")); });
        }
    }
}
