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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestFloodPublishing
    {
        private readonly byte[] _body = new byte[2048];
        private readonly TimeSpan _tenSeconds = TimeSpan.FromSeconds(10);

        [Test]
        public async ValueTask TestUnthrottledFloodPublishing()
        {
            var connFactory = new ConnectionFactory()
            {
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                AutomaticRecoveryEnabled = false
            };

            using (IConnection conn = await connFactory.CreateConnection())
            {
                using (IModel model = await conn.CreateModel())
                {
                    conn.ConnectionShutdown += (_, args) =>
                    {
                        if (args.Initiator != ShutdownInitiator.Application)
                        {
                            Assert.Fail("Unexpected connection shutdown!");
                        }

                        return default;
                    };

                    bool shouldStop = false;
                    DateTime now = DateTime.Now;
                    DateTime stopTime = DateTime.Now.Add(_tenSeconds);
                    for (int i = 0; i < 65535 * 64; i++)
                    {
                        if (i % 65536 == 0)
                        {
                            now = DateTime.Now;
                            shouldStop = DateTime.Now > stopTime;
                            if (shouldStop)
                            {
                                break;
                            }
                        }
                        await model.BasicPublish("", "", null, _body);
                    }
                    Assert.IsTrue(conn.IsOpen);
                }
            }
        }

        [Test]
        public async ValueTask TestMultithreadFloodPublishing()
        {
            string testName = TestContext.CurrentContext.Test.FullName;
            string message = string.Format("Hello from test {0}", testName);
            byte[] sendBody = Encoding.UTF8.GetBytes(message);
            int publishCount = 4096;
            int receivedCount = 0;
            AutoResetEvent autoResetEvent = new AutoResetEvent(false);

            var cf = new ConnectionFactory()
            {
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                AutomaticRecoveryEnabled = false
            };

            using (IConnection c = await cf.CreateConnection())
            {
                string queueName = null;
                using (IModel m = await c.CreateModel())
                {
                    QueueDeclareOk q = await m.QueueDeclare();
                    queueName = q.QueueName;
                }

                Task pub = Task.Run(async () =>
                {
                    using (IModel m = await c.CreateModel())
                    {
                        IBasicProperties bp = m.CreateBasicProperties();
                        for (int i = 0; i < publishCount; i++)
                        {
                            await m.BasicPublish(string.Empty, queueName, bp, sendBody);
                        }
                    }
                });

                using (IModel consumerModel = await c.CreateModel())
                {
                    var consumer = new EventingBasicConsumer(consumerModel);
                    consumer.Received += (o, a) =>
                    {
                        string receivedMessage = Encoding.UTF8.GetString(a.Body.ToArray());
                        Assert.AreEqual(message, receivedMessage);
                        Interlocked.Increment(ref receivedCount);
                        if (receivedCount == publishCount)
                        {
                            autoResetEvent.Set();
                        }
                    };
                    await consumerModel.BasicConsume(queueName, true, consumer);
                    Assert.IsTrue(pub.Wait(_tenSeconds));
                    Assert.IsTrue(autoResetEvent.WaitOne(_tenSeconds));
                }

                Assert.AreEqual(publishCount, receivedCount);
            }
        }
    }
}
