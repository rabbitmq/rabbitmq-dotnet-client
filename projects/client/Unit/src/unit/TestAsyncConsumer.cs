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

using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestAsyncConsumer
    {

        [Test]
        public void TestBasicRoundtrip()
        {
            var cf = new ConnectionFactory{ DispatchConsumersAsync = true };
            using(IConnection c = cf.CreateConnection())
            using(IModel m = c.CreateModel())
            {
                QueueDeclareOk q = m.QueueDeclare();
                IBasicProperties bp = m.CreateBasicProperties();
                byte[] body = System.Text.Encoding.UTF8.GetBytes("async-hi");
                m.BasicPublish("", q.QueueName, bp, body);
                var consumer = new AsyncEventingBasicConsumer(m);
                var are = new AutoResetEvent(false);
                consumer.Received += async (o, a) =>
                    {
                        are.Set();
                        await Task.Yield();
                    };
                string tag = m.BasicConsume(q.QueueName, true, consumer);
                // ensure we get a delivery
                bool waitRes = are.WaitOne(2000);
                Assert.IsTrue(waitRes);
                // unsubscribe and ensure no further deliveries
                m.BasicCancel(tag);
                m.BasicPublish("", q.QueueName, bp, body);
                bool waitResFalse = are.WaitOne(2000);
                Assert.IsFalse(waitResFalse);
            }
        }

        [Test]
        public void NonAsyncConsumerShouldThrowInvalidOperationException()
        {
            var cf = new ConnectionFactory{ DispatchConsumersAsync = true };
            using(IConnection c = cf.CreateConnection())
            using(IModel m = c.CreateModel())
            {
                QueueDeclareOk q = m.QueueDeclare();
                IBasicProperties bp = m.CreateBasicProperties();
                byte[] body = System.Text.Encoding.UTF8.GetBytes("async-hi");
                m.BasicPublish("", q.QueueName, bp, body);
                var consumer = new EventingBasicConsumer(m);
                Assert.Throws<InvalidOperationException>(() => m.BasicConsume(q.QueueName, false, consumer));
            }
        }
    }
}