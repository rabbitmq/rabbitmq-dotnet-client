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

using Xunit;
using Xunit.Abstractions;

namespace RabbitMQ.Client.Unit
{

    public class TestConsumerExceptions : IntegrationFixture
    {
        private class ConsumerFailingOnDelivery : DefaultBasicConsumer
        {
            public ConsumerFailingOnDelivery(IChannel model) : base(model)
            {
            }

            public override void HandleBasicDeliver(string consumerTag,
                ulong deliveryTag,
                bool redelivered,
                string exchange,
                string routingKey,
                in ReadOnlyBasicProperties properties,
                ReadOnlyMemory<byte> body)
            {
                throw new Exception("oops");
            }
        }

        private class ConsumerFailingOnCancel : DefaultBasicConsumer
        {
            public ConsumerFailingOnCancel(IChannel model) : base(model)
            {
            }

            public override void HandleBasicCancel(string consumerTag)
            {
                throw new Exception("oops");
            }
        }

        private class ConsumerFailingOnShutdown : DefaultBasicConsumer
        {
            public ConsumerFailingOnShutdown(IChannel model) : base(model)
            {
            }

            public override void HandleModelShutdown(object model, ShutdownEventArgs reason)
            {
                throw new Exception("oops");
            }
        }

        private class ConsumerFailingOnConsumeOk : DefaultBasicConsumer
        {
            public ConsumerFailingOnConsumeOk(IChannel model) : base(model)
            {
            }

            public override void HandleBasicConsumeOk(string consumerTag)
            {
                throw new Exception("oops");
            }
        }

        private class ConsumerFailingOnCancelOk : DefaultBasicConsumer
        {
            public ConsumerFailingOnCancelOk(IChannel model) : base(model)
            {
            }

            public override void HandleBasicCancelOk(string consumerTag)
            {
                throw new Exception("oops");
            }
        }

        protected void TestExceptionHandlingWith(IBasicConsumer consumer,
            Action<IChannel, string, IBasicConsumer, string> action)
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

            Assert.True(notified);
        }

        public TestConsumerExceptions(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void TestCancelNotificationExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnCancel(_model);
            TestExceptionHandlingWith(consumer, (m, q, c, ct) => m.QueueDelete(q));
        }

        [Fact]
        public void TestConsumerCancelOkExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnCancelOk(_model);
            TestExceptionHandlingWith(consumer, (m, q, c, ct) => m.BasicCancel(ct));
        }

        [Fact]
        public void TestConsumerConsumeOkExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnConsumeOk(_model);
            TestExceptionHandlingWith(consumer, (m, q, c, ct) => { });
        }

        [Fact]
        public void TestConsumerShutdownExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnShutdown(_model);
            TestExceptionHandlingWith(consumer, (m, q, c, ct) => m.Close());
        }

        [Fact]
        public void TestDeliveryExceptionHandling()
        {
            IBasicConsumer consumer = new ConsumerFailingOnDelivery(_model);
            TestExceptionHandlingWith(consumer, (m, q, c, ct) => m.BasicPublish("", q, _encoding.GetBytes("msg")));
        }
    }
}
