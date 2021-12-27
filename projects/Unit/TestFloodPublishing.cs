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
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using RabbitMQ.Client.Events;

using Xunit;

namespace RabbitMQ.Client.Unit
{

    public class TestFloodPublishing
    {
        private readonly byte[] _body = new byte[2048];
        private readonly TimeSpan _tenSeconds = TimeSpan.FromSeconds(10);

        [Fact]
        public void TestUnthrottledFloodPublishing()
        {
            var connFactory = new ConnectionFactory()
            {
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                AutomaticRecoveryEnabled = false
            };

            var closeWatch = new Stopwatch();
            using (IConnection conn = connFactory.CreateConnection())
            {
                using (IModel model = conn.CreateModel())
                {
                    conn.ConnectionShutdown += (_, args) =>
                    {
                        if (args.Initiator != ShutdownInitiator.Application)
                        {
                            Assert.True(false, "Unexpected connection shutdown!");
                        }
                    };

                    var stopwatch = Stopwatch.StartNew();
                    int i = 0;
                    try
                    {
                        for (i = 0; i < 65535 * 64; i++)
                        {
                            if (i % 65536 == 0)
                            {
                                if (stopwatch.Elapsed > _tenSeconds)
                                {
                                    break;
                                }
                            }

                            model.BasicPublish(CachedString.Empty, CachedString.Empty, _body);
                        }
                    }
                    finally
                    {
                        stopwatch.Stop();
                        Console.WriteLine($"sent {i}, done in {stopwatch.Elapsed.TotalMilliseconds} ms");
                    }

                    Assert.True(conn.IsOpen);
                    closeWatch.Start();
                }
            }
            closeWatch.Stop();
            Console.WriteLine($"Closing took {closeWatch.Elapsed.TotalMilliseconds} ms");
        }

        [Fact]
        public void TestMultithreadFloodPublishing()
        {
            string message = "Hello from test TestMultithreadFloodPublishing";
            byte[] sendBody = Encoding.UTF8.GetBytes(message);
            int publishCount = 4096;
            int receivedCount = 0;
            AutoResetEvent autoResetEvent = new AutoResetEvent(false);

            var cf = new ConnectionFactory()
            {
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                AutomaticRecoveryEnabled = false
            };

            using (IConnection c = cf.CreateConnection())
            {
                string queueName = null;
                using (IModel m = c.CreateModel())
                {
                    QueueDeclareOk q = m.QueueDeclare();
                    queueName = q.QueueName;
                }

                Task pub = Task.Run(() =>
                {
                    using (IModel m = c.CreateModel())
                    {
                        for (int i = 0; i < publishCount; i++)
                        {
                            m.BasicPublish(string.Empty, queueName, sendBody);
                        }
                    }
                });

                using (IModel consumerModel = c.CreateModel())
                {
                    var consumer = new EventingBasicConsumer(consumerModel);
                    consumer.Received += (o, a) =>
                    {
                        string receivedMessage = Encoding.UTF8.GetString(a.Body.ToArray());
                        Assert.Equal(message, receivedMessage);
                        Interlocked.Increment(ref receivedCount);
                        if (receivedCount == publishCount)
                        {
                            autoResetEvent.Set();
                        }
                    };
                    consumerModel.BasicConsume(queueName, true, consumer);
                    Assert.True(pub.Wait(_tenSeconds));
                    Assert.True(autoResetEvent.WaitOne(_tenSeconds));
                }

                Assert.Equal(publishCount, receivedCount);
            }
        }
    }
}
