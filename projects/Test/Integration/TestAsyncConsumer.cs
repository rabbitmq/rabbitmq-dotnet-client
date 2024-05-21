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
            QueueName qname = (QueueName)q;

            string publish1 = GetUniqueString(512);
            byte[] body = _encoding.GetBytes(publish1);
            await _channel.BasicPublishAsync(ExchangeName.Empty, (RoutingKey)qname, body);

            string publish2 = GetUniqueString(512);
            body = _encoding.GetBytes(publish2);
            await _channel.BasicPublishAsync(ExchangeName.Empty, (RoutingKey)qname, body);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            var publish1SyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var publish2SyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var tokenSource = new CancellationTokenSource(WaitSpan);
            CancellationTokenRegistration ctsr = tokenSource.Token.Register(() =>
            {
                publish1SyncSource.TrySetCanceled();
                publish2SyncSource.TrySetCanceled();
            });

            try
            {
                _conn.ConnectionShutdown += (o, ea) =>
                {
                    HandleConnectionShutdown(_conn, ea, (args) =>
                    {
                        if (args.Initiator == ShutdownInitiator.Peer)
                        {
                            MaybeSetException(ea, publish1SyncSource, publish2SyncSource);
                        }
                    });
                };

                _channel.ChannelShutdown += (o, ea) =>
                {
                    HandleChannelShutdown(_channel, ea, (args) =>
                    {
                        if (args.Initiator == ShutdownInitiator.Peer)
                        {
                            MaybeSetException(ea, publish1SyncSource, publish2SyncSource);
                        }
                    });
                };

                consumer.Received += async (o, a) =>
                {
                    string decoded = _encoding.GetString(a.Body.ToArray());
                    if (decoded == publish1)
                    {
                        publish1SyncSource.TrySetResult(true);
                    }
                    else if (decoded == publish2)
                    {
                        publish2SyncSource.TrySetResult(true);
                    }
                    else
                    {
                        var ex = new InvalidOperationException("incorrect message - should never happen!");
                        SetException(ex, publish1SyncSource, publish2SyncSource);
                    }

                    AsyncEventingBasicConsumer cons = (AsyncEventingBasicConsumer)o;
                    await cons.Channel.BasicAckAsync(a.DeliveryTag, false);
                };

                await _channel.BasicQosAsync(0, 1, false);
                await _channel.BasicConsumeAsync(qname, autoAck: false, ConsumerTag.Empty, false, false, null, consumer);

                // ensure we get a delivery
                await AssertRanToCompletion(publish1SyncSource.Task, publish2SyncSource.Task);

                bool result1 = await publish1SyncSource.Task;
                Assert.True(result1, $"1 - Non concurrent dispatch lead to deadlock after {WaitSpan}");

                bool result2 = await publish2SyncSource.Task;
                Assert.True(result2, $"2 - Non concurrent dispatch lead to deadlock after {WaitSpan}");
            }
            finally
            {
                ctsr.Dispose();
                tokenSource.Dispose();
            }
        }

        [Fact]
        public async Task TestBasicRoundtripConcurrentManyMessages()
        {
            const int publish_total = 4096;
            QueueName queueName = GenerateQueueName();

            string publish1 = GetUniqueString(512);
            byte[] body1 = _encoding.GetBytes(publish1);
            string publish2 = GetUniqueString(512);
            byte[] body2 = _encoding.GetBytes(publish2);

            var publish1SyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var publish2SyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var consumerSyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var tokenSource = new CancellationTokenSource(WaitSpan);
            CancellationTokenRegistration ctsr = tokenSource.Token.Register(() =>
            {
                publish1SyncSource.TrySetCanceled();
                publish2SyncSource.TrySetCanceled();
                consumerSyncSource.TrySetCanceled();
            });

            try
            {
                _conn.ConnectionShutdown += (o, ea) =>
                {
                    HandleConnectionShutdown(_conn, ea, (args) =>
                    {
                        if (args.Initiator == ShutdownInitiator.Peer)
                        {
                            MaybeSetException(ea, publish1SyncSource, publish2SyncSource);
                        }
                    });
                };

                _channel.ChannelShutdown += (o, ea) =>
                {
                    HandleChannelShutdown(_channel, ea, (args) =>
                    {
                        if (args.Initiator == ShutdownInitiator.Peer)
                        {
                            MaybeSetException(ea, publish1SyncSource, publish2SyncSource);
                        }
                    });
                };

                QueueDeclareOk q = await _channel.QueueDeclareAsync(queue: queueName, exclusive: false, autoDelete: true);
                Assert.Equal(queueName, q.QueueName);

                Task publishTask = Task.Run(async () =>
                        {
                            using (IConnection publishConn = await _connFactory.CreateConnectionAsync())
                            {
                                publishConn.ConnectionShutdown += (o, ea) =>
                                {
                                    HandleConnectionShutdown(publishConn, ea, (args) =>
                                    {
                                        if (args.Initiator == ShutdownInitiator.Peer)
                                        {
                                            MaybeSetException(ea, publish1SyncSource, publish2SyncSource);
                                        }
                                    });
                                };
                                using (IChannel publishChannel = await publishConn.CreateChannelAsync())
                                {
                                    publishChannel.ChannelShutdown += (o, ea) =>
                                    {
                                        HandleChannelShutdown(publishChannel, ea, (args) =>
                                        {
                                            if (args.Initiator == ShutdownInitiator.Peer)
                                            {
                                                MaybeSetException(ea, publish1SyncSource, publish2SyncSource);
                                            }
                                        });
                                    };
                                    await publishChannel.ConfirmSelectAsync();

                                    for (int i = 0; i < publish_total; i++)
                                    {
                                        await publishChannel.BasicPublishAsync(ExchangeName.Empty, (RoutingKey)queueName, body1, mandatory: true);
                                        await publishChannel.BasicPublishAsync(ExchangeName.Empty, (RoutingKey)queueName, body2, mandatory: true);
                                        await publishChannel.WaitForConfirmsOrDieAsync();
                                    }

                                    await publishChannel.CloseAsync();
                                }

                                await publishConn.CloseAsync();
                            }
                        });

                Task consumeTask = Task.Run(async () =>
                        {
                            using (IConnection consumeConn = await _connFactory.CreateConnectionAsync())
                            {
                                consumeConn.ConnectionShutdown += (o, ea) =>
                                {
                                    HandleConnectionShutdown(consumeConn, ea, (args) =>
                                    {
                                        if (args.Initiator == ShutdownInitiator.Peer)
                                        {
                                            MaybeSetException(ea, publish1SyncSource, publish2SyncSource);
                                        }
                                    });
                                };
                                using (IChannel consumeChannel = await consumeConn.CreateChannelAsync())
                                {
                                    consumeChannel.ChannelShutdown += (o, ea) =>
                                    {
                                        HandleChannelShutdown(consumeChannel, ea, (args) =>
                                        {
                                            if (args.Initiator == ShutdownInitiator.Peer)
                                            {
                                                MaybeSetException(ea, publish1SyncSource, publish2SyncSource);
                                            }
                                        });
                                    };

                                    var consumer = new AsyncEventingBasicConsumer(consumeChannel);

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
                                            }
                                        }
                                        else if (decoded == publish2)
                                        {
                                            if (Interlocked.Increment(ref publish2_count) >= publish_total)
                                            {
                                                publish2SyncSource.TrySetResult(true);
                                            }
                                        }
                                        else
                                        {
                                            var ex = new InvalidOperationException("incorrect message - should never happen!");
                                            SetException(ex, publish1SyncSource, publish2SyncSource);
                                        }

                                        AsyncEventingBasicConsumer cons = (AsyncEventingBasicConsumer)o;
                                        await cons.Channel.BasicAckAsync(a.DeliveryTag, false);
                                    };

                                    await consumeChannel.BasicQosAsync(0, 1, false);
                                    await consumeChannel.BasicConsumeAsync(queueName, autoAck: false, ConsumerTag.Empty, false, false, null, consumer);
                                    await consumerSyncSource.Task;

                                    await consumeChannel.CloseAsync();
                                }

                                await consumeConn.CloseAsync();
                            }
                        });

                await AssertRanToCompletion(publishTask);

                await AssertRanToCompletion(publish1SyncSource.Task, publish2SyncSource.Task);
                consumerSyncSource.TrySetResult(true);

                bool result1 = await publish1SyncSource.Task;
                Assert.True(result1, $"Non concurrent dispatch lead to deadlock after {WaitSpan}");

                bool result2 = await publish2SyncSource.Task;
                Assert.True(result2, $"Non concurrent dispatch lead to deadlock after {WaitSpan}");
            }
            finally
            {
                ctsr.Dispose();
                tokenSource.Dispose();
            }
        }

        [Fact]
        public async Task TestBasicRejectAsync()
        {
            QueueName queueName = GenerateQueueName();

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
                            MaybeSetException(ea, publishSyncSource);
                        }
                    });
                };

                _channel.ChannelShutdown += (o, ea) =>
                {
                    HandleChannelShutdown(_channel, ea, (args) =>
                    {
                        MaybeSetException(ea, publishSyncSource);
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

                QueueDeclareOk q = await _channel.QueueDeclareAsync(queueName, false, false, false);

                const string publish1 = "sync-hi-1";
                byte[] _body = _encoding.GetBytes(publish1);
                await _channel.BasicPublishAsync(ExchangeName.Empty, (RoutingKey)queueName, _body);

                await _channel.BasicConsumeAsync(queue: queueName, autoAck: false,
                    consumerTag: ConsumerTag.Empty, noLocal: false, exclusive: false,
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
                await _channel.QueueDeleteAsync(queue: queueName);
                ctsr.Dispose();
                cancellationTokenSource.Dispose();
            }
        }

        [Fact]
        public async Task TestBasicAckAsync()
        {
            QueueName queueName = GenerateQueueName();

            const int messageCount = 1024;
            int messagesReceived = 0;

            var publishSyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _conn.ConnectionShutdown += (o, ea) =>
            {
                HandleConnectionShutdown(_conn, ea, (args) =>
                {
                    if (args.Initiator == ShutdownInitiator.Peer)
                    {
                        MaybeSetException(ea, publishSyncSource);
                    }
                });
            };

            _channel.ChannelShutdown += (o, ea) =>
            {
                HandleChannelShutdown(_channel, ea, (args) =>
                {
                    if (args.Initiator == ShutdownInitiator.Peer)
                    {
                        MaybeSetException(ea, publishSyncSource);
                    }
                });
            };

            await _channel.ConfirmSelectAsync();

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (object sender, BasicDeliverEventArgs args) =>
            {
                var c = sender as AsyncEventingBasicConsumer;
                Assert.NotNull(c);
                await _channel.BasicAckAsync(args.DeliveryTag, false);
                Interlocked.Increment(ref messagesReceived);
                if (messagesReceived == messageCount)
                {
                    publishSyncSource.SetResult(true);
                }
            };

            QueueDeclareOk q = await _channel.QueueDeclareAsync(queueName, false, false, true);

            await _channel.BasicQosAsync(0, 1, false);
            await _channel.BasicConsumeAsync(queue: queueName, autoAck: false,
                consumerTag: ConsumerTag.Empty, noLocal: false, exclusive: false,
                arguments: null, consumer);

            Task<bool> publishTask = Task.Run(async () =>
            {
                using (IChannel publishChannel = await _conn.CreateChannelAsync())
                {
                    for (int i = 0; i < messageCount; i++)
                    {
                        byte[] _body = _encoding.GetBytes(Guid.NewGuid().ToString());
                        await publishChannel.BasicPublishAsync(ExchangeName.Empty, (RoutingKey)queueName, _body);
                        await publishChannel.WaitForConfirmsOrDieAsync();
                    }

                    return true;
                }
            });

            Assert.True(await publishTask);
            Assert.True(await publishSyncSource.Task);
            Assert.Equal(messageCount, messagesReceived);
            await _channel.QueueDeleteAsync(queue: queueName);
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
                        MaybeSetException(ea, publishSyncSource);
                    }
                });
            };

            _channel.ChannelShutdown += (o, ea) =>
            {
                HandleChannelShutdown(_channel, ea, (args) =>
                {
                    if (args.Initiator == ShutdownInitiator.Peer)
                    {
                        MaybeSetException(ea, publishSyncSource);
                    }
                });
            };

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (object sender, BasicDeliverEventArgs args) =>
            {
                var c = sender as AsyncEventingBasicConsumer;
                Assert.NotNull(c);
                await _channel.BasicCancelAsync(c.ConsumerTags[0]);
                await _channel.BasicNackAsync(args.DeliveryTag, false, true);
                publishSyncSource.SetResult(true);
            };

            QueueDeclareOk q = await _channel.QueueDeclareAsync(QueueName.Empty, false, false, false);
            QueueName queueName = (QueueName)q;
            const string publish1 = "sync-hi-1";
            byte[] _body = _encoding.GetBytes(publish1);
            await _channel.BasicPublishAsync(ExchangeName.Empty, (RoutingKey)queueName, _body);

            await _channel.BasicConsumeAsync(queue: queueName, autoAck: false,
                consumerTag: ConsumerTag.Empty, noLocal: false, exclusive: false,
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
            QueueDeclareOk q = await _channel.QueueDeclareAsync(QueueName.Empty, false, false, false);
            QueueName queueName = (QueueName)q;
            await _channel.BasicPublishAsync(ExchangeName.Empty, (RoutingKey)queueName, GetRandomBody(1024));
            var consumer = new EventingBasicConsumer(_channel);
            try
            {
                ConsumerTag consumerTag = await _channel.BasicConsumeAsync(queueName, false, ConsumerTag.Empty, false, false, null, consumer);
            }
            catch (InvalidOperationException)
            {
                sawException = true;
            }
            Assert.True(sawException, "did not see expected InvalidOperationException");
        }

        [Fact]
        public async Task TestDeclarationOfManyAutoDeleteQueuesWithTransientConsumer()
        {
            AssertRecordedQueues((RabbitMQ.Client.Framing.Impl.AutorecoveringConnection)_conn, 0);
            var tasks = new List<Task>();
            for (int i = 0; i < 256; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    using (IChannel ch = await _conn.CreateChannelAsync())
                    {
                        QueueName q = GenerateQueueName();
                        await ch.QueueDeclareAsync(q, false, false, true);
                        var dummy = new AsyncEventingBasicConsumer(ch);
                        ConsumerTag tag = await ch.BasicConsumeAsync(q, true, dummy);
                        await ch.BasicCancelAsync(tag);
                    }
                }));
            }
            await Task.WhenAll(tasks);
            AssertRecordedQueues((RabbitMQ.Client.Framing.Impl.AutorecoveringConnection)_conn, 0);
        }

        private static void SetException(Exception ex, params TaskCompletionSource<bool>[] tcsAry)
        {
            foreach (TaskCompletionSource<bool> tcs in tcsAry)
            {
                tcs.TrySetException(ex);
            }
        }

        private static void MaybeSetException(ShutdownEventArgs ea, params TaskCompletionSource<bool>[] tcsAry)
        {
            foreach (TaskCompletionSource<bool> tcs in tcsAry)
            {
                MaybeSetException(ea, tcs);
            }
        }

        private static void MaybeSetException(ShutdownEventArgs ea, TaskCompletionSource<bool> tcs)
        {
            if (ea.Initiator == ShutdownInitiator.Peer)
            {
                Exception ex = ea.Exception ?? new Exception(ea.ReplyText);
                tcs.TrySetException(ex);
            }
        }
    }
}
