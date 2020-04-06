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

using NUnit.Framework;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestFloodPublishing
    {
        private readonly byte[] _body = new byte[2048];
        private readonly TimeSpan _tenSeconds = TimeSpan.FromSeconds(10);

        /*
        [Test]
        public void TestUnthrottledFloodPublishing()
        {
            var connFactory = new ConnectionFactory()
            {
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                AutomaticRecoveryEnabled = false
            };

            using (var conn = connFactory.CreateConnection())
            {
                using (var model = conn.CreateModel())
                {
                    conn.ConnectionShutdown += (_, args) =>
                    {
                        if (args.Initiator != ShutdownInitiator.Application)
                        {
                            Assert.Fail("Unexpected connection shutdown!");
                        }
                    };

                    bool shouldStop = false;
                    DateTime now = DateTime.Now;
                    DateTime stopTime = DateTime.Now.Add(_tenSeconds);
                    for (int i = 0; i < 65535*64; i++)
                    {
                        if (i % 65536 == 0)
                        {
                            now = DateTime.Now;
                            shouldStop = DateTime.Now > stopTime;
                            TestContext.Out.WriteLine("@@@@@@@@ NUNIT Checking now {0} stopTime {1} shouldStop {2}", now, stopTime, shouldStop);
                            Console.Error.WriteLine("@@@@@@@@ STDERR Checking now {0} stopTime {1} shouldStop {2}", now, stopTime, shouldStop);
                            if (shouldStop)
                            {
                                break;
                            }
                        }
                        model.BasicPublish("", "", null, _body);
                    }
                    Assert.IsTrue(conn.IsOpen);
                }
            }
        }
        */

        [Test]
        public async Task TestMultithreadFloodPublishing()
        {
            string message = "test message";
            int threadCount = 1;
            int publishCount = 100;
            int receivedCount = 0;
            byte[] sendBody = Encoding.UTF8.GetBytes(message);

            var cf = new ConnectionFactory()
            {
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                AutomaticRecoveryEnabled = false
            };

            using (IConnection c = cf.CreateConnection())
            {
                using (IModel m = c.CreateModel())
                {
                    QueueDeclareOk q = m.QueueDeclare();
                    IBasicProperties bp = m.CreateBasicProperties();

                    var consumer = new EventingBasicConsumer(m);
                    var tcs = new TaskCompletionSource<bool>();
                    consumer.Received += (o, a) =>
                    {
                        var receivedMessage = Encoding.UTF8.GetString(a.Body.ToArray());
                        Assert.AreEqual(message, receivedMessage);

                        var result = Interlocked.Increment(ref receivedCount);
                        if (result == threadCount * publishCount)
                        {
                            tcs.SetResult(true);
                        }
                    };

                    string tag = m.BasicConsume(q.QueueName, true, consumer);
                    var cts = new CancellationTokenSource(_tenSeconds);

                    using (var timeoutRegistration = cts.Token.Register(() => tcs.SetCanceled()))
                    {
                        for (int i = 0; i < publishCount; i++)
                        {
                            m.BasicPublish(string.Empty, q.QueueName, bp, sendBody);
                        }
                        bool allMessagesReceived = await tcs.Task;
                        Assert.IsTrue(allMessagesReceived);
                    }
                    m.BasicCancel(tag);
                    Assert.AreEqual(threadCount * publishCount, receivedCount);
                }
            }
        }
    }
}
