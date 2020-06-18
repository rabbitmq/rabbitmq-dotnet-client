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

using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestPublisherConfirms : IntegrationFixture
    {
        private const string QueueName = "RabbitMQ.Client.Unit.TestPublisherConfirms";

        [Test]
        public async ValueTask TestWaitForConfirmsWithoutTimeout()
        {
            await TestWaitForConfirms(200, async (ch) =>
            {
                Assert.IsTrue(await ch.WaitForConfirms());
            });
        }

        [Test]
        public async ValueTask TestWaitForConfirmsWithTimeout()
        {
            await TestWaitForConfirms(200, async (ch) =>
            {
                Assert.IsTrue(await ch.WaitForConfirms(TimeSpan.FromSeconds(4)));
            });
        }

        [Test]
        public async ValueTask TestWaitForConfirmsWithTimeout_AllMessagesAcked_WaitingHasTimedout_ReturnTrue()
        {
            await TestWaitForConfirms(2000, (ch) =>
            {
                Assert.ThrowsAsync<TimeoutException>(async () => await ch.WaitForConfirms(TimeSpan.FromMilliseconds(1)));
                return default;
            });
        }

        [Test]
        public async ValueTask TestWaitForConfirmsWithTimeout_MessageNacked_WaitingHasTimedout_ReturnFalse()
        {
            await TestWaitForConfirms(200, async (ch) =>
            {
                BasicGetResult message = await ch.BasicGet(QueueName, false);

                var fullModel = ch as IFullModel;
                await fullModel.HandleBasicNack(message.DeliveryTag, false, false);

                Assert.IsFalse(await ch.WaitForConfirms(TimeSpan.FromMilliseconds(1)));
            });
        }

        [Test]
        public async ValueTask TestWaitForConfirmsWithEvents()
        {
            IModel ch = await Conn.CreateModel();
            await ch.ConfirmSelect();

            await ch.QueueDeclare(QueueName);
            int n = 200;
            // number of event handler invocations
            int c = 0;

            ch.BasicAcks += (_, args) =>
            {
                Interlocked.Increment(ref c);
            };
            try
            {
                for (int i = 0; i < n; i++)
                {
                    await ch.BasicPublish("", QueueName, null, encoding.GetBytes("msg"));
                }
                await Task.Delay(TimeSpan.FromSeconds(1));
                await ch.WaitForConfirms(TimeSpan.FromSeconds(5));

                // Note: number of event invocations is not guaranteed
                // to be equal to N because acks can be batched,
                // so we primarily care about event handlers being invoked
                // in this test
                Assert.IsTrue(c > 20);
            }
            finally
            {
                await ch.QueueDelete(QueueName);
                await ch.Close();
            }
        }

        protected async ValueTask TestWaitForConfirms(int numberOfMessagesToPublish, Func<IModel, ValueTask> fn)
        {
            IModel ch = await Conn.CreateModel();
            await ch.ConfirmSelect();

            await ch.QueueDeclare(QueueName);

            for (int i = 0; i < numberOfMessagesToPublish; i++)
            {
                await ch.BasicPublish("", QueueName, null, encoding.GetBytes("msg"));
            }

            try
            {
                await fn(ch);
            }
            finally
            {
                await ch.QueueDelete(QueueName);
                await ch.Close();
            }
        }
    }
}
