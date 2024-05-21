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

namespace Test.Integration
{
    public class TestQueueDeclare : IntegrationFixture
    {
        public TestQueueDeclare(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async void TestQueueDeclareAsync()
        {
            QueueName q = GenerateQueueName();

            QueueDeclareOk declareResult = await _channel.QueueDeclareAsync(q, false, false, false);
            Assert.Equal(q, declareResult.QueueName);

            QueueDeclareOk passiveDeclareResult = await _channel.QueueDeclarePassiveAsync(q);
            Assert.Equal(q, passiveDeclareResult.QueueName);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async void TestConcurrentQueueDeclareAndBindAsync(bool useDedicatedChannelPerTask)
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
            var queueNames = new ConcurrentDictionary<QueueName, bool>();

            ExchangeName exchangeName = GenerateExchangeName();
            await _channel.ExchangeDeclareAsync(exchange: exchangeName, ExchangeType.Fanout,
                durable: false, autoDelete: true);

            NotSupportedException nse = null;
            for (int i = 0; i < 256; i++)
            {
                async Task f()
                {
                    IChannel ch = _channel;
                    if (useDedicatedChannelPerTask)
                    {
                        ch = await _conn.CreateChannelAsync();
                    }

                    try
                    {
                        // sleep for a random amount of time to increase the chances
                        // of thread interleaving. MK.
                        await Task.Delay(S_Random.Next(5, 50));
                        QueueName queueName = GenerateQueueName();
                        QueueDeclareOk r = await ch.QueueDeclareAsync(queue: queueName,
                            durable: false, exclusive: true, autoDelete: false);
                        Assert.Equal(queueName, r.QueueName);
                        await ch.QueueBindAsync(queue: queueName,
                            exchange: exchangeName, routingKey: (RoutingKey)queueName);
                        if (false == queueNames.TryAdd(queueName, true))
                        {
                            throw new InvalidOperationException($"queue with name {queueName} already added!");
                        }
                    }
                    catch (NotSupportedException e)
                    {
                        nse = e;
                    }
                    finally
                    {
                        if (useDedicatedChannelPerTask)
                        {
                            await ch.CloseAsync();
                            ch.Dispose();
                        }
                    }
                }
                var t = Task.Run(f);
                tasks.Add(t);
            }

            await AssertRanToCompletion(tasks);
            Assert.Null(nse);
            tasks.Clear();

            nse = null;
            foreach (QueueName q in queueNames.Keys)
            {
                async Task f()
                {
                    QueueName qname = q;
                    IChannel ch = _channel;
                    if (useDedicatedChannelPerTask)
                    {
                        ch = await _conn.CreateChannelAsync();
                    }

                    try
                    {
                        await Task.Delay(S_Random.Next(5, 50));

                        QueueDeclareOk r = await ch.QueueDeclarePassiveAsync(qname);
                        Assert.Equal(qname, r.QueueName);
                        Assert.Equal((uint)0, r.MessageCount);

                        await ch.QueueUnbindAsync(queue: qname,
                            exchange: exchangeName, routingKey: (RoutingKey)qname, null);

                        uint deletedMessageCount = await ch.QueueDeleteAsync(qname, false, false);
                        Assert.Equal((uint)0, deletedMessageCount);
                    }
                    catch (NotSupportedException e)
                    {
                        nse = e;
                    }
                    finally
                    {
                        if (useDedicatedChannelPerTask)
                        {
                            await ch.CloseAsync();
                            ch.Dispose();
                        }
                    }
                }
                var t = Task.Run(f);
                tasks.Add(t);
            }

            await AssertRanToCompletion(tasks);
            Assert.Null(nse);
            Assert.False(sawShutdown);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TestConcurrentQueueDeclare(bool useDedicatedChannelPerTask)
        {
            var queueNames = new ConcurrentBag<QueueName>();
            var tasks = new List<Task>();
            NotSupportedException nse = null;
            for (int i = 0; i < 256; i++)
            {
                var t = Task.Run(async () =>
                        {
                            IChannel ch = _channel;
                            if (useDedicatedChannelPerTask)
                            {
                                ch = await _conn.CreateChannelAsync();
                            }

                            try
                            {
                                // sleep for a random amount of time to increase the chances
                                // of thread interleaving. MK.
                                await Task.Delay(S_Random.Next(5, 50));
                                QueueName q = GenerateQueueName();
                                await ch.QueueDeclareAsync(q, false, false, false);
                                queueNames.Add(q);
                            }
                            catch (NotSupportedException e)
                            {
                                nse = e;
                            }
                            finally
                            {
                                if (useDedicatedChannelPerTask)
                                {
                                    await ch.CloseAsync();
                                    ch.Dispose();
                                }
                            }
                        });
                tasks.Add(t);
            }

            await Task.WhenAll(tasks);

            Assert.Null(nse);
            tasks.Clear();

            foreach (QueueName queueName in queueNames)
            {
                QueueName q = queueName;
                var t = Task.Run(async () =>
                        {
                            IChannel ch = _channel;
                            if (useDedicatedChannelPerTask)
                            {
                                ch = await _conn.CreateChannelAsync();
                            }

                            try
                            {
                                await Task.Delay(S_Random.Next(5, 50));
                                await ch.QueueDeleteAsync(queueName);
                            }
                            catch (NotSupportedException e)
                            {
                                nse = e;
                            }
                            finally
                            {
                                if (useDedicatedChannelPerTask)
                                {
                                    await ch.CloseAsync();
                                    ch.Dispose();
                                }
                            }
                        });
                tasks.Add(t);
            }

            await Task.WhenAll(tasks);

            Assert.Null(nse);
        }
    }
}
