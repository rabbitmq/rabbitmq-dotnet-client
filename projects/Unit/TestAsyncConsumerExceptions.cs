﻿// This source code is dual-licensed under the Apache License, version
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

using RabbitMQ.Client.Events;

using Xunit;
using Xunit.Abstractions;

namespace RabbitMQ.Client.Unit
{
    public class TestAsyncConsumerExceptions : IntegrationFixture
    {
        private static readonly Exception TestException = new Exception("oops");

        public TestAsyncConsumerExceptions(ITestOutputHelper output) : base(output)
        {
        }

        protected void TestExceptionHandlingWith(IBasicConsumer consumer,
            Action<IModel, string, IBasicConsumer, string> action)
        {
            var resetEvent = new AutoResetEvent(false);
            bool notified = false;
            string q = _model.QueueDeclare();

            _model.CallbackException += (m, evt) =>
            {
                if (evt.Exception != TestException) return;

                notified = true;
                resetEvent.Set();
            };

            string tag = _model.BasicConsume(q, true, consumer);
            action(_model, q, consumer, tag);
            resetEvent.WaitOne(2000);

            Assert.True(notified);
        }

        protected override void SetUp()
        {
            _connFactory = new ConnectionFactory
            {
                DispatchConsumersAsync = true
            };

            _conn = _connFactory.CreateConnection();
            _model = _conn.CreateModel();
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

        private class ConsumerFailingOnDelivery : AsyncEventingBasicConsumer
        {
            public ConsumerFailingOnDelivery(IModel model) : base(model)
            {
            }

            public override Task HandleBasicDeliver(string consumerTag,
                ulong deliveryTag,
                bool redelivered,
                ReadOnlyMemory<byte> exchange,
                ReadOnlyMemory<byte> routingKey,
                in ReadOnlyBasicProperties properties,
                ReadOnlyMemory<byte> body)
            {
                return Task.FromException(TestException);
            }
        }

        private class ConsumerFailingOnCancel : AsyncEventingBasicConsumer
        {
            public ConsumerFailingOnCancel(IModel model) : base(model)
            {
            }

            public override Task HandleBasicCancel(string consumerTag)
            {
                return Task.FromException(TestException);
            }
        }

        private class ConsumerFailingOnShutdown : AsyncEventingBasicConsumer
        {
            public ConsumerFailingOnShutdown(IModel model) : base(model)
            {
            }

            public override Task HandleModelShutdown(object model, ShutdownEventArgs reason)
            {
                return Task.FromException(TestException);
            }
        }

        private class ConsumerFailingOnConsumeOk : AsyncEventingBasicConsumer
        {
            public ConsumerFailingOnConsumeOk(IModel model) : base(model)
            {
            }

            public override Task HandleBasicConsumeOk(string consumerTag)
            {
                return Task.FromException(TestException);
            }
        }

        private class ConsumerFailingOnCancelOk : AsyncEventingBasicConsumer
        {
            public ConsumerFailingOnCancelOk(IModel model) : base(model)
            {
            }

            public override Task HandleBasicCancelOk(string consumerTag)
            {
                return Task.FromException(TestException);
            }
        }
    }
}
