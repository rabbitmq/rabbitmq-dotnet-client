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
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.AsyncIntegration
{
    public class TestAsyncConsumer : AsyncIntegrationFixture
    {
        private readonly ShutdownEventArgs _closeArgs = new ShutdownEventArgs(ShutdownInitiator.Application, Constants.ReplySuccess, "normal shutdown");

        public TestAsyncConsumer(ITestOutputHelper output)
            : base(output, dispatchConsumersAsync: true, consumerDispatchConcurrency: 2)
        {
        }

        [Fact]
        public async Task TestBasicRoundtripConcurrent()
        {
            QueueDeclareOk q = await _channel.QueueDeclareAsync();
            string publish1 = GetUniqueString(1024);
            byte[] body = _encoding.GetBytes(publish1);
            await _channel.BasicPublishAsync("", q.QueueName, body);

            string publish2 = GetUniqueString(1024);
            body = _encoding.GetBytes(publish2);
            await _channel.BasicPublishAsync("", q.QueueName, body);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            var publish1SyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var publish2SyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var maximumWaitTime = TimeSpan.FromSeconds(10);
            var tokenSource = new CancellationTokenSource(maximumWaitTime);
            tokenSource.Token.Register(() =>
            {
                publish1SyncSource.TrySetResult(false);
                publish2SyncSource.TrySetResult(false);
            });

            _conn.ConnectionShutdown += (o, ea) =>
            {
                HandleConnectionShutdown(_conn, ea, (args) =>
                {
                    if (args.Initiator == ShutdownInitiator.Peer)
                    {
                        publish1SyncSource.TrySetResult(false);
                        publish2SyncSource.TrySetResult(false);
                    }
                });
            };

            _channel.ChannelShutdown += (o, ea) =>
            {
                HandleChannelShutdown(_channel, ea, (args) =>
                {
                    if (args.Initiator == ShutdownInitiator.Peer)
                    {
                        publish1SyncSource.TrySetResult(false);
                        publish2SyncSource.TrySetResult(false);
                    }
                });
            };

            consumer.Received += async (o, a) =>
            {
                string decoded = _encoding.GetString(a.Body.ToArray());
                if (decoded == publish1)
                {
                    publish1SyncSource.TrySetResult(true);
                    await publish2SyncSource.Task;
                }
                else if (decoded == publish2)
                {
                    publish2SyncSource.TrySetResult(true);
                    await publish1SyncSource.Task;
                }
            };

            await _channel.BasicConsumeAsync(q.QueueName, true, string.Empty, false, false, null, consumer);

            // ensure we get a delivery
            await AssertRanToCompletion(publish1SyncSource.Task, publish2SyncSource.Task);

            bool result1 = await publish1SyncSource.Task;
            Assert.True(result1, $"1 - Non concurrent dispatch lead to deadlock after {maximumWaitTime}");

            bool result2 = await publish2SyncSource.Task;
            Assert.True(result2, $"2 - Non concurrent dispatch lead to deadlock after {maximumWaitTime}");
        }

        [Fact]
        public async Task TestBasicRoundtripConcurrentManyMessages()
        {
            const int publish_total = 4096;
            string queueName = $"{nameof(TestBasicRoundtripConcurrentManyMessages)}-{Guid.NewGuid()}";

            string publish1 = GetUniqueString(32768);
            byte[] body1 = _encoding.GetBytes(publish1);
            string publish2 = GetUniqueString(32768);
            byte[] body2 = _encoding.GetBytes(publish2);

            var publish1SyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var publish2SyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var maximumWaitTime = TimeSpan.FromSeconds(30);
            var tokenSource = new CancellationTokenSource(maximumWaitTime);
            tokenSource.Token.Register(() =>
            {
                publish1SyncSource.TrySetResult(false);
                publish2SyncSource.TrySetResult(false);
            });

            _conn.ConnectionShutdown += (o, ea) =>
            {
                HandleConnectionShutdown(_conn, ea, (args) =>
                {
                    if (args.Initiator == ShutdownInitiator.Peer)
                    {
                        publish1SyncSource.TrySetResult(false);
                        publish2SyncSource.TrySetResult(false);
                    }
                });
            };

            _channel.ChannelShutdown += (o, ea) =>
            {
                HandleChannelShutdown(_channel, ea, (args) =>
                {
                    if (args.Initiator == ShutdownInitiator.Peer)
                    {
                        publish1SyncSource.TrySetResult(false);
                        publish2SyncSource.TrySetResult(false);
                    }
                });
            };

            QueueDeclareOk q = await _channel.QueueDeclareAsync(queue: queueName, exclusive: false, durable: true);
            Assert.Equal(q, queueName);

            Task publishTask = Task.Run(async () =>
                    {
                        using (IChannel m = await _conn.CreateChannelAsync())
                        {
                            QueueDeclareOk q = _channel.QueueDeclare(queue: queueName, exclusive: false, durable: true);
                            for (int i = 0; i < publish_total; i++)
                            {
                                await _channel.BasicPublishAsync(string.Empty, queueName, body1);
                                await _channel.BasicPublishAsync(string.Empty, queueName, body2);
                            }
                        }
                    });

            Task consumeTask = Task.Run(async () =>
                    {
                        using (IChannel m = await _conn.CreateChannelAsync())
                        {
                            var consumer = new AsyncEventingBasicConsumer(m);

                            int publish1_count = 0;
                            int publish2_count = 0;

                            consumer.Received += async (o, a) =>
                            {
                                string decoded = _encoding.GetString(a.Body.ToArray());
                                if (decoded == publish1)
                                {
                                    if (Interlocked.Increment(ref publish1_count) >= publish_total)
                                    {
                                        publish1SyncSource.TrySetResult(true);
                                        await publish2SyncSource.Task;
                                    }
                                }
                                else if (decoded == publish2)
                                {
                                    if (Interlocked.Increment(ref publish2_count) >= publish_total)
                                    {
                                        publish2SyncSource.TrySetResult(true);
                                        await publish1SyncSource.Task;
                                    }
                                }
                            };

                            await _channel.BasicConsumeAsync(queueName, true, string.Empty, false, false, null, consumer);

                            // ensure we get a delivery
                            await AssertRanToCompletion(publish1SyncSource.Task, publish2SyncSource.Task);

                            bool result1 = await publish1SyncSource.Task;
                            Assert.True(result1, $"Non concurrent dispatch lead to deadlock after {maximumWaitTime}");

                            bool result2 = await publish2SyncSource.Task;
                            Assert.True(result2, $"Non concurrent dispatch lead to deadlock after {maximumWaitTime}");
                        }
                    });

            await AssertRanToCompletion(publishTask, consumeTask);
        }

        [Fact]
        public async Task TestBasicRejectAsync()
        {
            var publishSyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var cancellationTokenSource = new CancellationTokenSource(TestTimeout);

            cancellationTokenSource.Token.Register(() =>
            {
                publishSyncSource.SetCanceled();
            });

            _conn.ConnectionShutdown += (o, ea) =>
            {
                HandleConnectionShutdown(_conn, ea, (args) =>
                {
                    if (args.Initiator == ShutdownInitiator.Peer)
                    {
                        publishSyncSource.TrySetResult(false);
                    }
                });
            };

            _channel.ChannelShutdown += (o, ea) =>
            {
                HandleChannelShutdown(_channel, ea, (args) =>
                {
                    if (args.Initiator == ShutdownInitiator.Peer)
                    {
                        publishSyncSource.TrySetResult(false);
                    }
                });
            };

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (object sender, BasicDeliverEventArgs args) =>
            {
                var c = sender as AsyncEventingBasicConsumer;
                Assert.Same(c, consumer);
                await _channel.BasicCancelAsync(c.ConsumerTags[0]);
                /*
                 * https://github.com/rabbitmq/rabbitmq-dotnet-client/actions/runs/7450578332/attempts/1
                 * That job failed with a bizarre error where the delivery tag ack timed out:
                 *
                 * AI.TestAsyncConsumer.TestBasicRejectAsync channel 1 shut down:
                 *     AMQP close-reason, initiated by Peer, code=406, text=
                 *         'PRECONDITION_FAILED - delivery acknowledgement on channel 1 timed out. Timeout value used: 1800000 ms ...', classId=0, methodId=0
                 *  
                 * Added Task.Yield() to see if it ever happens again.
                 */
                await Task.Yield();
                await _channel.BasicRejectAsync(args.DeliveryTag, true);
                publishSyncSource.TrySetResult(true);
            };

            QueueDeclareOk q = await _channel.QueueDeclareAsync(string.Empty, false, false, true, false, null);
            string queueName = q.QueueName;
            const string publish1 = "sync-hi-1";
            byte[] _body = _encoding.GetBytes(publish1);
            await _channel.BasicPublishAsync(string.Empty, queueName, _body);

            await _channel.BasicConsumeAsync(queue: queueName, autoAck: false,
                consumerTag: string.Empty, noLocal: false, exclusive: false,
                arguments: null, consumer);

            Assert.True(await publishSyncSource.Task);

            uint messageCount, consumerCount = 0;
            ushort tries = 5;
            do
            {
                QueueDeclareOk result = await _channel.QueueDeclareAsync(queue: queueName, passive: true, false, false, false, null);
                consumerCount = result.ConsumerCount;
                messageCount = result.MessageCount;
                if (consumerCount == 0 && messageCount > 0)
                {
                    break;
                }
                else
                {
                    await Task.Delay(500);
                }
            } while (tries-- > 0);

            if (tries == 0)
            {
                Assert.Fail("[ERROR] failed waiting for MessageCount > 0 && ConsumerCount == 0");
            }
            else
            {
                Assert.Equal((uint)1, messageCount);
                Assert.Equal((uint)0, consumerCount);
            }
        }

        [Fact]
        public async Task TestBasicAckAsync()
        {
            const int messageCount = 1024;
            int messagesReceived = 0;

            var publishSyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var cf = CreateConnectionFactory();
            cf.DispatchConsumersAsync = true;

            using IConnection connection = await cf.CreateConnectionAsync();
            using IChannel channel = await connection.CreateChannelAsync();

            connection.ConnectionShutdown += (o, ea) =>
            {
                HandleConnectionShutdown(connection, ea, (args) =>
                {
                    if (args.Initiator == ShutdownInitiator.Peer)
                    {
                        publishSyncSource.TrySetResult(false);
                    }
                });
            };

            channel.ChannelShutdown += (o, ea) =>
            {
                HandleChannelShutdown(channel, ea, (args) =>
                {
                    if (args.Initiator == ShutdownInitiator.Peer)
                    {
                        publishSyncSource.TrySetResult(false);
                    }
                });
            };

            await channel.ConfirmSelectAsync();

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += async (object sender, BasicDeliverEventArgs args) =>
            {
                var c = sender as AsyncEventingBasicConsumer;
                Assert.NotNull(c);
                await channel.BasicAckAsync(args.DeliveryTag, false);
                messagesReceived++;
                if (messagesReceived == messageCount)
                {
                    publishSyncSource.SetResult(true);
                }
            };

            QueueDeclareOk q = await channel.QueueDeclareAsync(string.Empty, false, false, true, false, null);
            string queueName = q.QueueName;

            await channel.BasicQosAsync(0, 1, false);
            await channel.BasicConsumeAsync(queue: queueName, autoAck: false,
                consumerTag: string.Empty, noLocal: false, exclusive: false,
                arguments: null, consumer);

            var publishTask = Task.Run(async () =>
            {
                for (int i = 0; i < messageCount; i++)
                {
                    byte[] _body = _encoding.GetBytes(Guid.NewGuid().ToString());
                    await channel.BasicPublishAsync(string.Empty, queueName, _body);
                }
            });

            await channel.WaitForConfirmsOrDieAsync();
            Assert.True(await publishSyncSource.Task);

            Assert.Equal(messageCount, messagesReceived);

            // Note: closing channel explicitly just to test it.
            await channel.CloseAsync(_closeArgs, false);
        }

        [Fact]
        public async Task TestBasicNackAsync()
        {
            var publishSyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var cf = CreateConnectionFactory();
            cf.DispatchConsumersAsync = true;

            using IConnection connection = await cf.CreateConnectionAsync();
            using IChannel channel = await connection.CreateChannelAsync();

            connection.ConnectionShutdown += (o, ea) =>
            {
                HandleConnectionShutdown(connection, ea, (args) =>
                {
                    if (args.Initiator == ShutdownInitiator.Peer)
                    {
                        publishSyncSource.TrySetResult(false);
                    }
                });
            };

            channel.ChannelShutdown += (o, ea) =>
            {
                HandleChannelShutdown(channel, ea, (args) =>
                {
                    if (args.Initiator == ShutdownInitiator.Peer)
                    {
                        publishSyncSource.TrySetResult(false);
                    }
                });
            };

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += async (object sender, BasicDeliverEventArgs args) =>
            {
                var c = sender as AsyncEventingBasicConsumer;
                Assert.NotNull(c);
                await channel.BasicCancelAsync(c.ConsumerTags[0]);
                await channel.BasicNackAsync(args.DeliveryTag, false, true);
                publishSyncSource.SetResult(true);
            };

            QueueDeclareOk q = await channel.QueueDeclareAsync(string.Empty, false, false, false, false, null);
            string queueName = q.QueueName;
            const string publish1 = "sync-hi-1";
            byte[] _body = _encoding.GetBytes(publish1);
            await channel.BasicPublishAsync(string.Empty, queueName, _body);

            await channel.BasicConsumeAsync(queue: queueName, autoAck: false,
                consumerTag: string.Empty, noLocal: false, exclusive: false,
                arguments: null, consumer);

            Assert.True(await publishSyncSource.Task);

            uint messageCount, consumerCount = 0;
            ushort tries = 5;
            do
            {
                QueueDeclareOk result = await channel.QueueDeclareAsync(queue: queueName, passive: true, false, false, false, null);
                consumerCount = result.ConsumerCount;
                messageCount = result.MessageCount;
                if (consumerCount == 0 && messageCount > 0)
                {
                    break;
                }
                else
                {
                    await Task.Delay(500);
                }
            } while (tries-- > 0);

            if (tries == 0)
            {
                Assert.Fail("[ERROR] failed waiting for MessageCount > 0 && ConsumerCount == 0");
            }
            else
            {
                Assert.Equal((uint)1, messageCount);
                Assert.Equal((uint)0, consumerCount);
            }

            // Note: closing channel explicitly just to test it.
            await channel.CloseAsync(_closeArgs, false);
        }

        [Fact]
        public async Task NonAsyncConsumerShouldThrowInvalidOperationException()
        {
            bool sawException = false;
            QueueDeclareOk q = await _channel.QueueDeclareAsync(string.Empty, false, false, false, false, null);
            await _channel.BasicPublishAsync(string.Empty, q.QueueName, GetRandomBody(1024));
            var consumer = new EventingBasicConsumer(_channel);
            try
            {
                string consumerTag = await _channel.BasicConsumeAsync(q.QueueName, false, string.Empty, false, false, null, consumer);
            }
            catch (InvalidOperationException)
            {
                sawException = true;
            }
            Assert.True(sawException, "did not see expected InvalidOperationException");
        }
    }
}
