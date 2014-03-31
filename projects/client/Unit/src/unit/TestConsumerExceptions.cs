// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2013 GoPivotal, Inc.
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
//  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;

using System;
using System.Threading;

using RabbitMQ.Client.Events;


namespace RabbitMQ.Client.Unit {
    [TestFixture]
    public class TestConsumerExceptions : IntegrationFixture {

        [Test]
        public void TestDeliveryExceptionHandling()
        {
            Object o = new Object();
            bool notified = false;
            string q = Model.QueueDeclare();
            IBasicConsumer consumer = new ConsumerFailingOnDelivery(Model);

            Model.CallbackException += (m, evt) => {
                notified = true;
                Monitor.PulseAll(o);
            };

            Model.BasicConsume(q, true, consumer);
            Model.BasicPublish("", q, null, enc.GetBytes("msg"));
            WaitOn(o);

            Assert.IsTrue(notified);
        }

        private class ConsumerFailingOnDelivery : DefaultBasicConsumer
        {
            public ConsumerFailingOnDelivery(IModel model) : base(model) {}

            public override void HandleBasicDeliver(string consumerTag,
                                               ulong deliveryTag,
                                               bool redelivered,
                                               string exchange,
                                               string routingKey,
                                               IBasicProperties properties,
                                               byte[] body) {
                throw new SystemException("oops");
            }
        }


        [Test]
        public void TestCancelNotificationExceptionHandling()
        {
            Object o = new Object();
            bool notified = false;
            string q = Model.QueueDeclare();
            IBasicConsumer consumer = new ConsumerFailingOnCancel(Model);

            Model.CallbackException += (m, evt) => {
                notified = true;
                Monitor.PulseAll(o);
            };

            Model.BasicConsume(q, true, consumer);
            Model.QueueDelete(q);
            WaitOn(o);

            Assert.IsTrue(notified);
        }

        private class ConsumerFailingOnCancel : DefaultBasicConsumer
        {
            public ConsumerFailingOnCancel(IModel model) : base(model) {}

            public override void HandleBasicCancel(string consumerTag) {
                throw new SystemException("oops");
            }
        }
    }
}

