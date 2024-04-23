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

namespace Test.Integration
{
    public class TestAsyncConsumer : IntegrationFixture
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
            CancellationTokenRegistration ctsr = tokenSource.Token.Register(() =>
            {
                publish1SyncSource.TrySetResult(false);
                publish2SyncSource.TrySetResult(false);
            });

            try
            {
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

                consumer.Received += async (o, a, ct) =>
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
            finally
            {
                tokenSource.Dispose();
                ctsr.Dispose();
            }
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
            CancellationTokenRegistration ctsr = tokenSource.Token.Register(() =>
            {
                publish1SyncSource.TrySetResult(false);
                publish2SyncSource.TrySetResult(false);
            });

            try
            {
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
                            using (IChannel publishChannel = await _conn.CreateChannelAsync())
                            {
                                QueueDeclareOk pubQ = await publishChannel.QueueDeclareAsync(queue: queueName, exclusive: false, durable: true);
                                Assert.Equal(queueName, pubQ.QueueName);
                                for (int i = 0; i < publish_total; i++)
                                {
                                    await publishChannel.BasicPublishAsync(string.Empty, queueName, body1);
                                    await publishChannel.BasicPublishAsync(string.Empty, queueName, body2);
                                }

                                await publishChannel.CloseAsync();
                            }
                        });

                Task consumeTask = Task.Run(async () =>
                        {
                            using (IChannel consumeChannel = await _conn.CreateChannelAsync())
                            {
                                var consumer = new AsyncEventingBasicConsumer(consumeChannel);

                                int publish1_count = 0;
                                int publish2_count = 0;

                                consumer.Received += async (o, a, ct) =>
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

                                await consumeChannel.BasicConsumeAsync(queueName, true, string.Empty, false, false, null, consumer);

                                // ensure we get a delivery
                                await AssertRanToCompletion(publish1SyncSource.Task, publish2SyncSource.Task);

                                bool result1 = await publish1SyncSource.Task;
                                Assert.True(result1, $"Non concurrent dispatch lead to deadlock after {maximumWaitTime}");

                                bool result2 = await publish2SyncSource.Task;
                                Assert.True(result2, $"Non concurrent dispatch lead to deadlock after {maximumWaitTime}");

                                await consumeChannel.CloseAsync();
                            }
                        });

                await AssertRanToCompletion(publishTask, consumeTask);
            }
            finally
            {
                tokenSource.Dispose();
                ctsr.Dispose();
            }
        }

        [Fact]
        public async Task TestBasicRejectAsync()
        {
            var publishSyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellationTokenSource = new CancellationTokenSource(TestTimeout);
            CancellationTokenRegistration ctsr = cancellationTokenSource.Token.Register(() =>
            {
                publishSyncSource.SetCanceled();
            });

            try
            {
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
                consumer.Received += async (object sender, BasicDeliverEventArgs args,
                    CancellationToken cancellationToken) =>
                {
                    var c = sender as AsyncEventingBasicConsumer;
                    Assert.Same(c, consumer);
                    await _channel.BasicCancelAsync(c.ConsumerTags[0], false, cancellationToken);
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
                    await _channel.BasicRejectAsync(args.DeliveryTag, true, cancellationToken);
                    publishSyncSource.TrySetResult(true);
                };

                QueueDeclareOk q = await _channel.QueueDeclareAsync(queue: string.Empty,
                    durable: false, exclusive: true, autoDelete: false);
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
                    QueueDeclareOk result = await _channel.QueueDeclarePassiveAsync(queue: queueName);
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
            finally
            {
                cancellationTokenSource.Dispose();
                ctsr.Dispose();
            }
        }

        [Fact]
        public async Task TestBasicAckAsync()
        {
            const int messageCount = 1024;
            int messagesReceived = 0;

            var publishSyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

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

            await _channel.ConfirmSelectAsync();

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (object sender, BasicDeliverEventArgs args,
                CancellationToken cancellationToken) =>
            {
                var c = sender as AsyncEventingBasicConsumer;
                Assert.NotNull(c);
                await _channel.BasicAckAsync(args.DeliveryTag, false, cancellationToken);
                Interlocked.Increment(ref messagesReceived);
                if (messagesReceived == messageCount)
                {
                    publishSyncSource.SetResult(true);
                }
            };

            QueueDeclareOk q = await _channel.QueueDeclareAsync(string.Empty, false, false, true);
            string queueName = q.QueueName;

            await _channel.BasicQosAsync(0, 1, false);
            await _channel.BasicConsumeAsync(queue: queueName, autoAck: false,
                consumerTag: string.Empty, noLocal: false, exclusive: false,
                arguments: null, consumer);

            var publishTask = Task.Run(async () =>
            {
                for (int i = 0; i < messageCount; i++)
                {
                    byte[] _body = _encoding.GetBytes(Guid.NewGuid().ToString());
                    await _channel.BasicPublishAsync(string.Empty, queueName, _body);
                    await _channel.WaitForConfirmsOrDieAsync();
                }
            });

            Assert.True(await publishSyncSource.Task);
            Assert.Equal(messageCount, messagesReceived);
            await _channel.CloseAsync(_closeArgs, false, CancellationToken.None);
        }

        [Fact]
        public async Task TestBasicNackAsync()
        {
            var publishSyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

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
            consumer.Received += async (object sender, BasicDeliverEventArgs args,
                CancellationToken cancellationToken) =>
            {
                var c = sender as AsyncEventingBasicConsumer;
                Assert.NotNull(c);
                await _channel.BasicCancelAsync(c.ConsumerTags[0], false, cancellationToken);
                await _channel.BasicNackAsync(args.DeliveryTag, false, true, cancellationToken);
                publishSyncSource.SetResult(true);
            };

            QueueDeclareOk q = await _channel.QueueDeclareAsync(string.Empty, false, false, false);
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
                QueueDeclareOk result = await _channel.QueueDeclarePassiveAsync(queue: queueName);
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

            await _channel.CloseAsync(_closeArgs, false, CancellationToken.None);
        }

        [Fact]
        public async Task NonAsyncConsumerShouldThrowInvalidOperationException()
        {
            bool sawException = false;
            QueueDeclareOk q = await _channel.QueueDeclareAsync(string.Empty, false, false, false);
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
