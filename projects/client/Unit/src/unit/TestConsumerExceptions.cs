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
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Threading;
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

            public override void HandleBasicDeliver(string consumerTag,
                ulong deliveryTag,
                bool redelivered,
                string exchange,
                string routingKey,
                IBasicProperties properties,
                byte[] body)
            {
                throw new SystemException("oops");
            }
        }

        private class ConsumerFailingOnCancel : DefaultBasicConsumer
        {
            public ConsumerFailingOnCancel(IModel model) : base(model)
            {
            }

            public override void HandleBasicCancel(string consumerTag)
            {
                throw new SystemException("oops");
            }
        }

        private class ConsumerFailingOnShutdown : DefaultBasicConsumer
        {
            public ConsumerFailingOnShutdown(IModel model) : base(model)
            {
            }

            public override void HandleModelShutdown(object model, ShutdownEventArgs reason)
            {
                throw new SystemException("oops");
            }
        }

        private class ConsumerFailingOnConsumeOk : DefaultBasicConsumer
        {
            public ConsumerFailingOnConsumeOk(IModel model) : base(model)
            {
            }

            public override void HandleBasicConsumeOk(string consumerTag)
            {
                throw new SystemException("oops");
            }
        }

        private class ConsumerFailingOnCancelOk : DefaultBasicConsumer
        {
            public ConsumerFailingOnCancelOk(IModel model) : base(model)
            {
            }

            public override void HandleBasicCancelOk(string consumerTag)
            {
                throw new SystemException("oops");
            }
        }

        protected void TestExceptionHandlingWith(IBasicConsumer consumer,
            Action<IModel, string, IBasicConsumer, string> action)
        {
            var o = new object();
            bool notified = false;
            string q = Model.QueueDeclare();


            Model.CallbackException += (m, evt) =>
            {
                notified = true;
                Monitor.PulseAll(o);
            };

            string tag = Model.BasicConsume(q, true, consumer);
            action(Model, q, consumer, tag);
            WaitOn(o);

            Assert.IsTrue(notified);
        }

        [Test]
        public void TestCancelNotificationExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnCancel(Model);
            TestExceptionHandlingWith(consumer, (m, q, c, ct) => m.QueueDelete(q));
        }

        [Test]
        public void TestConsumerCancelOkExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnCancelOk(Model);
            TestExceptionHandlingWith(consumer, (m, q, c, ct) => m.BasicCancel(ct));
        }

        [Test]
        public void TestConsumerConsumeOkExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnConsumeOk(Model);
            TestExceptionHandlingWith(consumer, (m, q, c, ct) => { });
        }

        [Test]
        public void TestConsumerShutdownExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnShutdown(Model);
            TestExceptionHandlingWith(consumer, (m, q, c, ct) => m.Close());
        }

        [Test]
        public void TestDeliveryExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnDelivery(Model);
            TestExceptionHandlingWith(consumer, (m, q, c, ct) => m.BasicPublish("", q, null, encoding.GetBytes("msg")));
        }
    }
}
