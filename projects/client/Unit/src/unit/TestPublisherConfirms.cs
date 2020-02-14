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
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.Threading;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestPublisherConfirms : IntegrationFixture
    {
        private const string QueueName = "RabbitMQ.Client.Unit.TestPublisherConfirms";

        [Test]
        public void TestWaitForConfirmsWithoutTimeout()
        {
            TestWaitForConfirms(200, (ch) =>
            {
                Assert.IsTrue(ch.WaitForConfirms());
            });;
        }

        [Test]
        public void TestWaitForConfirmsWithTimeout()
        {
            TestWaitForConfirms(200, (ch) =>
            {
                Assert.IsTrue(ch.WaitForConfirms(TimeSpan.FromSeconds(4)));
            }); ;
        }

        [Test]
        public void TestWaitForConfirmsWithTimeout_AllMessagesAcked_WaitingHasTimedout_ReturnTrue()
        {
            TestWaitForConfirms(200, (ch) =>
            {
                Assert.IsTrue(ch.WaitForConfirms(TimeSpan.FromMilliseconds(1)));
            });
        }

        [Test]
        public void TestWaitForConfirmsWithTimeout_MessageNacked_WaitingHasTimedout_ReturnFalse()
        {
            TestWaitForConfirms(200, (ch) =>
            {
                var message = ch.BasicGet(QueueName, false);

                var fullModel = ch as IFullModel;
                fullModel.HandleBasicNack(message.DeliveryTag, false, false);

                Assert.IsFalse(ch.WaitForConfirms(TimeSpan.FromMilliseconds(1)));
            });
        }

        [Test]
        public void TestWaitForConfirmsWithEvents()
        {
            var ch = Conn.CreateModel();
            ch.ConfirmSelect();

            ch.QueueDeclare(QueueName);
            var n = 200;
            // number of event handler invocations
            var c = 0;

            ch.BasicAcks += (_, args) =>
            {
                Interlocked.Increment(ref c);
            };
            try
            {
                for (int i = 0; i < n; i++)
                {
                    ch.BasicPublish("", QueueName, null, encoding.GetBytes("msg"));
                }
                Thread.Sleep(TimeSpan.FromSeconds(1));
                ch.WaitForConfirms(TimeSpan.FromSeconds(5));

                // Note: number of event invocations is not guaranteed
                // to be equal to N because acks can be batched,
                // so we primarily care about event handlers being invoked
                // in this test
                Assert.IsTrue(c > 20);
            }
            finally
            {
                ch.QueueDelete(QueueName);
                ch.Close();
            }
        }

        protected void TestWaitForConfirms(int numberOfMessagesToPublish, Action<IModel> fn)
        {
            var ch = Conn.CreateModel();
            ch.ConfirmSelect();

            ch.QueueDeclare(QueueName);

            for (int i = 0; i < numberOfMessagesToPublish; i++)
            {
                ch.BasicPublish("", QueueName, null, encoding.GetBytes("msg"));
            }

            try
            {
                fn(ch);
            }
            finally
            {
                ch.QueueDelete(QueueName);
                ch.Close();
            }
        }
    }
}
