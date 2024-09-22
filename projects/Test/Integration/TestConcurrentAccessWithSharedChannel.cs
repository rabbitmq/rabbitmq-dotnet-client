// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;
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
            // TODO
            // Hack for rabbitmq/rabbitmq-dotnet-client#1682
            AutorecoveringChannel ach = (AutorecoveringChannel)_channel;
            await ach.ConfirmSelectAsync(trackConfirmations: false);

            int publishAckCount = 0;

            _channel.BasicAcksAsync += (object sender, BasicAckEventArgs e) =>
            {
                Interlocked.Increment(ref publishAckCount);
                return Task.CompletedTask;
            };

            _channel.BasicNacksAsync += (object sender, BasicNackEventArgs e) =>
            {
                _output.WriteLine($"channel #{_channel.ChannelNumber} saw a nack, deliveryTag: {e.DeliveryTag}, multiple: {e.Multiple}");
                return Task.CompletedTask;
            };


            await TestConcurrentOperationsAsync(async () =>
            {
                long receivedCount = 0;
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                var msgTracker = new ConcurrentDictionary<ushort, bool>();

                QueueDeclareOk q = await _channel.QueueDeclareAsync(queue: string.Empty, passive: false,
                    durable: false, exclusive: true, autoDelete: true, arguments: null);

                var consumer = new AsyncEventingBasicConsumer(_channel);

                consumer.ReceivedAsync += async (object sender, BasicDeliverEventArgs ea) =>
                {
                    try
                    {
                        System.Diagnostics.Debug.Assert(object.ReferenceEquals(sender, consumer));
                        ushort idx = ushort.Parse(_encoding.GetString(ea.Body.ToArray()));
                        Assert.False(msgTracker[idx]);
                        msgTracker[idx] = true;

                        var cons = (AsyncEventingBasicConsumer)sender;
                        IChannel ch = cons.Channel;
                        await ch.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);

                        if (Interlocked.Increment(ref receivedCount) == _messageCount)
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
                    msgTracker[i] = false;
                    byte[] body = _encoding.GetBytes(i.ToString());
                    publishTasks.Add(_channel.BasicPublishAsync("", q.QueueName, mandatory: true, body: body));
                }

                foreach (ValueTask pt in publishTasks)
                {
                    await pt;
                }

                Assert.True(await tcs.Task);
            }, Iterations);
        }
    }
}
