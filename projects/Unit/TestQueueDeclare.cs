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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace RabbitMQ.Client.Unit
{
    public class TestQueueDeclare : IntegrationFixture
    {
        private readonly Random _rnd = new Random();

        public TestQueueDeclare(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async void TestQueueDeclareAsync()
        {
            string q = GenerateQueueName();

            QueueDeclareOk declareResult = await _channel.QueueDeclareAsync(q, passive: false, false, false, false, null);
            Assert.Equal(q, declareResult.QueueName);

            QueueDeclareOk passiveDeclareResult = await _channel.QueueDeclareAsync(q, passive: true, false, false, false, null);
            Assert.Equal(q, passiveDeclareResult.QueueName);
        }

        [Fact]
        public void TestConcurrentQueueDeclare()
        {
            var qs = new List<string>();
            var ts = new List<Thread>();
            NotSupportedException nse = null;
            for (int i = 0; i < 256; i++)
            {
                var t = new Thread(() =>
                        {
                            try
                            {
                                // sleep for a random amount of time to increase the chances
                                // of thread interleaving. MK.
                                Thread.Sleep(_rnd.Next(5, 50));
                                string q = GenerateQueueName();
                                _channel.QueueDeclare(q, false, false, false, null);
                                qs.Add(q);
                            }
                            catch (NotSupportedException e)
                            {
                                nse = e;
                            }
                        });
                ts.Add(t);
                t.Start();
            }

            foreach (Thread t in ts)
            {
                t.Join();
            }

            Assert.Null(nse);
            ts.Clear();

            foreach (string queueName in qs)
            {
                var t = new Thread(() =>
                        {
                            try
                            {
                                Thread.Sleep(_rnd.Next(5, 50));
                                _channel.QueueDelete(queueName);
                            }
                            catch (NotSupportedException e)
                            {
                                nse = e;
                            }
                        });
                ts.Add(t);
                t.Start();
            }

            foreach (Thread t in ts)
            {
                t.Join();
            }

            Assert.Null(nse);
        }

        [Fact]
        public async void TestConcurrentQueueDeclareAndBindAsync()
        {
            var ts = new List<Task>();
            var qs = new List<string>();

            NotSupportedException nse = null;
            for (int i = 0; i < 256; i++)
            {
                async Task f()
                {
                    try
                    {
                        // sleep for a random amount of time to increase the chances
                        // of thread interleaving. MK.
                        await Task.Delay(_rnd.Next(5, 50));
                        QueueDeclareOk r = await _channel.QueueDeclareAsync(queue: string.Empty, passive: false, false, false, false, null);
                        string queueName = r.QueueName;
                        await _channel.QueueBindAsync(queue: queueName, exchange: "amq.fanout", routingKey: queueName, null);
                        qs.Add(queueName);
                    }
                    catch (NotSupportedException e)
                    {
                        nse = e;
                    }
                }
                var t = Task.Run(f);
                ts.Add(t);
            }

            await Task.WhenAll(ts);
            Assert.Null(nse);
            ts.Clear();

            nse = null;
            foreach (string q in qs)
            {
                async Task f()
                {
                    string qname = q;
                    try
                    {
                        await Task.Delay(_rnd.Next(5, 50));

                        QueueDeclareOk r = await _channel.QueueDeclareAsync(qname, passive: true, false, false, false, null);
                        Assert.Equal(qname, r.QueueName);

                        uint deletedMessageCount = await _channel.QueueDeleteAsync(qname, false, false);
                        Assert.Equal((uint)0, deletedMessageCount);
                    }
                    catch (NotSupportedException e)
                    {
                        nse = e;
                    }
                }
                var t = Task.Run(f);
                ts.Add(t);
            }

            await Task.WhenAll(ts);
            Assert.Null(nse);
        }
    }
}
