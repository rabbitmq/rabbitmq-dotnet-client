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
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestFloodPublishing : IntegrationFixture
    {
        private readonly byte[] _body = new byte[2048];
        private readonly TimeSpan _tenSeconds = TimeSpan.FromSeconds(10);

        public TestFloodPublishing(ITestOutputHelper output) : base(output)
        {
        }

        protected override void SetUp()
        {
            _connFactory = CreateConnectionFactory();
            _connFactory.RequestedHeartbeat = TimeSpan.FromSeconds(60);
            _connFactory.AutomaticRecoveryEnabled = false;

            _conn = _connFactory.CreateConnection();
            _channel = _conn.CreateChannel();
        }

        [Fact]
        public async Task TestUnthrottledFloodPublishingAsync()
        {
            _conn.ConnectionShutdown += (_, args) =>
            {
                if (args.Initiator != ShutdownInitiator.Application)
                {
                    Assert.Fail("Unexpected connection shutdown!");
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

                    await _channel.BasicPublishAsync(CachedString.Empty, CachedString.Empty, _body);
                }
            }
            finally
            {
                stopwatch.Stop();
            }

            Assert.True(_conn.IsOpen);
        }

        [Fact]
        public async Task TestMultithreadFloodPublishingAsync()
        {
            string message = "Hello from test TestMultithreadFloodPublishing";
            byte[] sendBody = _encoding.GetBytes(message);
            int publishCount = 4096;
            int receivedCount = 0;
            var autoResetEvent = new AutoResetEvent(false);

            var cf = CreateConnectionFactory();
            cf.RequestedHeartbeat = TimeSpan.FromSeconds(60);
            cf.AutomaticRecoveryEnabled = false;

            string queueName = null;
            QueueDeclareOk q = _channel.QueueDeclare();
            queueName = q.QueueName;

            Task pub = Task.Run(async () =>
            {
                using (IChannel pubCh = _conn.CreateChannel())
                {
                    for (int i = 0; i < publishCount; i++)
                    {
                        await pubCh.BasicPublishAsync(string.Empty, queueName, sendBody);
                    }
                }
            });

            using (IChannel consumeCh = _conn.CreateChannel())
            {
                var consumer = new EventingBasicConsumer(consumeCh);
                consumer.Received += (o, a) =>
                {
                    string receivedMessage = _encoding.GetString(a.Body.ToArray());
                    Assert.Equal(message, receivedMessage);
                    Interlocked.Increment(ref receivedCount);
                    if (receivedCount == publishCount)
                    {
                        autoResetEvent.Set();
                    }
                };
                consumeCh.BasicConsume(queueName, true, consumer);

                Assert.True(autoResetEvent.WaitOne(_tenSeconds));
            }

            await pub;
            Assert.Equal(publishCount, receivedCount);
        }
    }
}
