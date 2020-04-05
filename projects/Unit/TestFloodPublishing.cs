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
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestFloodPublishing : IntegrationFixture
    {
        [Test, Category("LongRunning")]
        public void TestUnthrottledFloodPublishing()
        {
            var connFactory = new ConnectionFactory()
            {
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                AutomaticRecoveryEnabled = false
            };
            using var Conn = connFactory.CreateConnection();
            using var Model = Conn.CreateModel();

            Conn.ConnectionShutdown += (_, args) =>
            {
                if (args.Initiator != ShutdownInitiator.Application)
                {
                    Assert.Fail("Unexpected connection shutdown!");
                }
            };

            bool elapsed = false;
            using (Timer t = new Timer((_obj) => { elapsed = true; }, null, 1000 * 185, -1))
            {
                while (!elapsed)
                {
                    Model.BasicPublish("", "", null, new byte[2048]);
                }
                Assert.IsTrue(Conn.IsOpen);
            }
        }

        // TODO rabbitmq/rabbitmq-dotnet-client#802 FIX THIS
        [Test, Category("LongRunning")]
        public async Task TestMultithreadFloodPublishing()
        {
            string message = "test message";
            int threadCount = 1;
            int publishCount = 100;
            var receivedCount = 0;
            byte[] sendBody = Encoding.UTF8.GetBytes(message);

            var cf = new ConnectionFactory();
            using (IConnection c = cf.CreateConnection())
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
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                using (var timeoutRegistration = cts.Token.Register(() => tcs.SetCanceled()))
                {
                    for (int i = 0; i < publishCount; i++)
                    {
                        m.BasicPublish(string.Empty, q.QueueName, bp, sendBody);
                    }

                    await tcs.Task;
                }
                m.BasicCancel(tag);
                await tcs.Task;
                Assert.AreEqual(threadCount * publishCount, receivedCount);
            }
        }
    }
}
