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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Xunit;
using Xunit.Abstractions;

namespace Test.AsyncIntegration
{
    public class TestQueueDeclareAsync : AsyncIntegrationFixture
    {
        public TestQueueDeclareAsync(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async void TestQueueDeclare()
        {
            string q = GenerateQueueName();

            QueueDeclareOk declareResult = await _channel.QueueDeclareAsync(q, false, false, false);
            Assert.Equal(q, declareResult.QueueName);

            QueueDeclareOk passiveDeclareResult = await _channel.QueueDeclarePassiveAsync(q);
            Assert.Equal(q, passiveDeclareResult.QueueName);
        }

        [Fact]
        public async void TestConcurrentQueueDeclareAndBindAsync()
        {
            bool sawShutdown = false;

            _conn.ConnectionShutdown += (o, ea) =>
            {
                HandleConnectionShutdown(_conn, ea, (args) =>
                {
                    if (ea.Initiator == ShutdownInitiator.Peer)
                    {
                        sawShutdown = true;
                    }
                });
            };

            _channel.ChannelShutdown += (o, ea) =>
            {
                HandleChannelShutdown(_channel, ea, (args) =>
                {
                    if (args.Initiator == ShutdownInitiator.Peer)
                    {
                        sawShutdown = true;
                    }
                });
            };

            var tasks = new List<Task>();
            var queues = new ConcurrentBag<string>();

            NotSupportedException nse = null;
            for (int i = 0; i < 256; i++)
            {
                async Task f()
                {
                    try
                    {
                        // sleep for a random amount of time to increase the chances
                        // of thread interleaving. MK.
                        await Task.Delay(S_Random.Next(5, 50));
                        QueueDeclareOk r = await _channel.QueueDeclareAsync(queue: string.Empty, false, false, false);
                        string queueName = r.QueueName;
                        await _channel.QueueBindAsync(queue: queueName, exchange: "amq.fanout", routingKey: queueName);
                        queues.Add(queueName);
                    }
                    catch (NotSupportedException e)
                    {
                        nse = e;
                    }
                }
                var t = Task.Run(f);
                tasks.Add(t);
            }

            await AssertRanToCompletion(tasks);
            Assert.Null(nse);
            tasks.Clear();

            nse = null;
            foreach (string q in queues)
            {
                async Task f()
                {
                    string qname = q;
                    try
                    {
                        await Task.Delay(S_Random.Next(5, 50));

                        QueueDeclareOk r = await _channel.QueueDeclarePassiveAsync(qname);
                        Assert.Equal(qname, r.QueueName);

                        await _channel.QueueUnbindAsync(queue: qname, exchange: "amq.fanout", routingKey: qname, null);

                        uint deletedMessageCount = await _channel.QueueDeleteAsync(qname, false, false);
                        Assert.Equal((uint)0, deletedMessageCount);
                    }
                    catch (NotSupportedException e)
                    {
                        nse = e;
                    }
                }
                var t = Task.Run(f);
                tasks.Add(t);
            }

            await AssertRanToCompletion(tasks);
            Assert.Null(nse);
            Assert.False(sawShutdown);
        }
    }
}
