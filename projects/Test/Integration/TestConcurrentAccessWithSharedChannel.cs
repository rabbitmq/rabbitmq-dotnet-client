// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestConcurrentAccessWithSharedChannel : TestConcurrentAccessBase
    {
        private const int Iterations = 10;

        public TestConcurrentAccessWithSharedChannel(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task ConcurrentPublishSingleChannel()
        {
            int expectedTotalMessageCount = 0;
            int expectedTotalReturnCount = 0;
            int totalNackCount = 0;
            int totalReturnCount = 0;
            int totalReceivedCount = 0;

            _channel.BasicAcksAsync += (object sender, BasicAckEventArgs e) =>
            {
                return Task.CompletedTask;
            };

            await TestConcurrentOperationsAsync(async () =>
            {
                long thisBatchReceivedCount = 0;
                long thisBatchReturnedCount = 0;
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                var msgTracker = new ConcurrentDictionary<ushort, bool>();

                QueueDeclareOk q = await _channel.QueueDeclareAsync(queue: string.Empty, passive: false,
                    durable: false, exclusive: true, autoDelete: true, arguments: null);

                var consumer = new AsyncEventingBasicConsumer(_channel);

                consumer.ReceivedAsync += async (object sender, BasicDeliverEventArgs ea) =>
                {
                    Interlocked.Increment(ref totalReceivedCount);
                    try
                    {
                        System.Diagnostics.Debug.Assert(object.ReferenceEquals(sender, consumer));
                        ushort idx = ushort.Parse(_encoding.GetString(ea.Body.ToArray()));
                        Assert.False(msgTracker[idx]);
                        msgTracker[idx] = true;

                        var cons = (AsyncEventingBasicConsumer)sender;
                        IChannel ch = cons.Channel;
                        await ch.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);

                        long receivedCountSoFar = Interlocked.Increment(ref thisBatchReceivedCount);

                        if ((receivedCountSoFar + thisBatchReturnedCount) == _messageCount)
                        {
                            if (msgTracker.Values.Any(v => v == false))
                            {
                                tcs.SetResult(false);
                            }
                            else
                            {
                                tcs.SetResult(true);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                };

                await _channel.BasicConsumeAsync(queue: q.QueueName, autoAck: false, consumer);

                var publishTasks = new List<ValueTask>();
                for (ushort i = 0; i < _messageCount; i++)
                {
                    Interlocked.Increment(ref expectedTotalMessageCount);

                    byte[] body = _encoding.GetBytes(i.ToString());

                    string routingKey = q.QueueName;
                    if (i % 5 == 0)
                    {
                        routingKey = Guid.NewGuid().ToString();
                        Interlocked.Increment(ref thisBatchReturnedCount);
                        Interlocked.Increment(ref expectedTotalReturnCount);
                    }
                    else
                    {
                        msgTracker[i] = false;
                    }

                    publishTasks.Add(_channel.BasicPublishAsync("", routingKey, mandatory: true, body: body));
                }

                foreach (ValueTask pt in publishTasks)
                {
                    try
                    {
                        await pt;
                    }
                    catch (PublishException ex)
                    {
                        if (ex is PublishReturnException prex)
                        {
                            Assert.True(prex.IsReturn);
                            Assert.NotNull(prex.Exchange);
                            Assert.NotNull(prex.RoutingKey);
                            Assert.NotEqual(0, prex.ReplyCode);
                            Assert.NotNull(prex.ReplyText);
                            Interlocked.Increment(ref totalReturnCount);
                        }
                        else
                        {
                            Interlocked.Increment(ref totalNackCount);
                        }
                    }
                }

                Assert.True(await tcs.Task);
            }, Iterations);

            if (IsVerbose)
            {
                _output.WriteLine("expectedTotalMessageCount: {0}", expectedTotalMessageCount);
                _output.WriteLine("expectedTotalReturnCount: {0}", expectedTotalReturnCount);
                _output.WriteLine("totalReceivedCount: {0}", totalReceivedCount);
                _output.WriteLine("totalReturnCount: {0}", totalReturnCount);
                _output.WriteLine("totalNackCount: {0}", totalNackCount);
            }

            Assert.Equal(expectedTotalReturnCount, totalReturnCount);
            Assert.Equal(expectedTotalMessageCount, (totalReceivedCount + totalReturnCount));
            Assert.Equal(0, totalNackCount);
        }
    }
}
