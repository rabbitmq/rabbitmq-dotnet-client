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

            string publish1 = GetUniqueString(512);
            byte[] body = _encoding.GetBytes(publish1);
            await _channel.BasicPublishAsync("", q.QueueName, body);

            string publish2 = GetUniqueString(512);
            body = _encoding.GetBytes(publish2);
            await _channel.BasicPublishAsync("", q.QueueName, body);

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
                        MaybeSetException(args, publish1SyncSource, publish2SyncSource);
                    });
                };

                _channel.ChannelShutdown += (o, ea) =>
                {
                    HandleChannelShutdown(_channel, ea, (args) =>
                    {
                        MaybeSetException(args, publish1SyncSource, publish2SyncSource);
                    });
                };

                consumer.Received += (o, a) =>
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
                    return Task.CompletedTask;
                };

                await _channel.BasicConsumeAsync(q.QueueName, true, string.Empty, false, false, null, consumer);

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
            string queueName = GenerateQueueName();

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
                        MaybeSetException(args, publish1SyncSource, publish2SyncSource);
                    });
                };

                _channel.ChannelShutdown += (o, ea) =>
                {
                    HandleChannelShutdown(_channel, ea, (args) =>
                    {
                        MaybeSetException(args, publish1SyncSource, publish2SyncSource);
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
                                        MaybeSetException(args, publish1SyncSource, publish2SyncSource);
                                    });
                                };
                                using (IChannel publishChannel = await publishConn.CreateChannelAsync())
                                {
                                    publishChannel.ChannelShutdown += (o, ea) =>
                                    {
                                        HandleChannelShutdown(publishChannel, ea, (args) =>
                                        {
                                            MaybeSetException(args, publish1SyncSource, publish2SyncSource);
                                        });
                                    };
                                    await publishChannel.ConfirmSelectAsync();

                                    for (int i = 0; i < publish_total; i++)
                                    {
                                        await publishChannel.BasicPublishAsync(string.Empty, queueName, body1);
                                        await publishChannel.BasicPublishAsync(string.Empty, queueName, body2);
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
                                        MaybeSetException(ea, publish1SyncSource, publish2SyncSource);
                                    });
                                };
                                using (IChannel consumeChannel = await consumeConn.CreateChannelAsync())
                                {
                                    consumeChannel.ChannelShutdown += (o, ea) =>
                                    {
                                        HandleChannelShutdown(consumeChannel, ea, (args) =>
                                        {
                                            MaybeSetException(ea, publish1SyncSource, publish2SyncSource);
                                        });
                                    };

                                    var consumer = new AsyncEventingBasicConsumer(consumeChannel);

                                    int publish1_count = 0;
                                    int publish2_count = 0;

                                    consumer.Received += (o, a) =>
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
                                        return Task.CompletedTask;
                                    };

                                    await consumeChannel.BasicConsumeAsync(queueName, true, string.Empty, false, false, null, consumer);
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
            string queueName = GenerateQueueName();

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
                        MaybeSetException(args, publishSyncSource);
                    });
                };

                _channel.ChannelShutdown += (o, ea) =>
                {
                    HandleChannelShutdown(_channel, ea, (args) =>
                    {
                        MaybeSetException(args, publishSyncSource);
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
                await _channel.QueueDeleteAsync(queue: queueName);
                ctsr.Dispose();
                cancellationTokenSource.Dispose();
            }
        }

        [Fact]
        public async Task TestBasicAckAsync()
        {
            string queueName = GenerateQueueName();

            const int messageCount = 1024;
            int messagesReceived = 0;

            var publishSyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _conn.ConnectionShutdown += (o, ea) =>
            {
                HandleConnectionShutdown(_conn, ea, (args) =>
                {
                    MaybeSetException(args, publishSyncSource);
                });
            };

            _channel.ChannelShutdown += (o, ea) =>
            {
                HandleChannelShutdown(_channel, ea, (args) =>
                {
                    MaybeSetException(args, publishSyncSource);
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
                consumerTag: string.Empty, noLocal: false, exclusive: false,
                arguments: null, consumer);

            Task<bool> publishTask = Task.Run(async () =>
            {
                for (int i = 0; i < messageCount; i++)
                {
                    byte[] _body = _encoding.GetBytes(Guid.NewGuid().ToString());
                    await _channel.BasicPublishAsync(string.Empty, queueName, _body);
                    await _channel.WaitForConfirmsOrDieAsync();
                }

                return true;
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
                    MaybeSetException(ea, publishSyncSource);
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
                Assert.NotNull(c);
                await _channel.BasicCancelAsync(c.ConsumerTags[0]);
                await _channel.BasicNackAsync(args.DeliveryTag, false, true);
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

        [Fact]
        public async Task TestDeclarationOfManyAutoDeleteQueuesWithTransientConsumer()
        {
            AssertRecordedQueues((RabbitMQ.Client.Framing.Impl.AutorecoveringConnection)_conn, 0);
            var tasks = new List<Task>();
            for (int i = 0; i < 256; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    string q = GenerateQueueName();
                    await _channel.QueueDeclareAsync(q, false, false, true);
                    var dummy = new AsyncEventingBasicConsumer(_channel);
                    string tag = await _channel.BasicConsumeAsync(q, true, dummy);
                    await _channel.BasicCancelAsync(tag);
                }));
            }
            await Task.WhenAll(tasks);
            AssertRecordedQueues((RabbitMQ.Client.Framing.Impl.AutorecoveringConnection)_conn, 0);
        }

        [Fact]
        public async Task TestCreateChannelWithinAsyncConsumerCallback_GH650()
        {
            string exchangeName = GenerateExchangeName();
            string queue1Name = GenerateQueueName();
            string queue2Name = GenerateQueueName();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource(WaitSpan);
            using CancellationTokenRegistration ctr = cts.Token.Register(() =>
            {
                tcs.SetCanceled();
            });

            _conn.ConnectionShutdown += (o, ea) =>
            {
                HandleConnectionShutdown(_conn, ea, (args) =>
                {
                    MaybeSetException(ea, tcs);
                });
            };

            _channel.ChannelShutdown += (o, ea) =>
            {
                HandleChannelShutdown(_channel, ea, (args) =>
                {
                    MaybeSetException(ea, tcs);
                });
            };

            //                   queue1 -> produce click to queue2
            // click -> exchange
            //                   queue2 -> consume click from queue1
            await _channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Direct, autoDelete: true);
            await _channel.QueueDeclareAsync(queue1Name);
            await _channel.QueueBindAsync(queue1Name, exchangeName, queue1Name);
            await _channel.QueueDeclareAsync(queue2Name);
            await _channel.QueueBindAsync(queue2Name, exchangeName, queue2Name);

            var consumer1 = new AsyncEventingBasicConsumer(_channel);
            consumer1.Received += async (sender, args) =>
            {
                using (IChannel innerChannel = await _conn.CreateChannelAsync())
                {
                    await innerChannel.ConfirmSelectAsync();
                    await innerChannel.BasicPublishAsync(exchangeName, queue2Name, mandatory: true);
                    await innerChannel.WaitForConfirmsOrDieAsync();
                    await innerChannel.CloseAsync();
                }
            };
            await _channel.BasicConsumeAsync(queue1Name, autoAck: true, consumer1);

            var consumer2 = new AsyncEventingBasicConsumer(_channel);
            consumer2.Received += async (sender, args) =>
            {
                tcs.TrySetResult(true);
                await Task.Yield();
            };
            await _channel.BasicConsumeAsync(queue2Name, autoAck: true, consumer2);

            await _channel.ConfirmSelectAsync();
            await _channel.BasicPublishAsync(exchangeName, queue1Name, body: GetRandomBody(1024));
            await _channel.WaitForConfirmsOrDieAsync();

            Assert.True(await tcs.Task);
        }

        private static void SetException(Exception ex, params TaskCompletionSource<bool>[] tcsAry)
        {
            foreach (TaskCompletionSource<bool> tcs in tcsAry)
            {
                tcs.TrySetException(ex);
            }
        }

        private static void MaybeSetException(ShutdownEventArgs args, params TaskCompletionSource<bool>[] tcsAry)
        {
            if (args.Initiator != ShutdownInitiator.Application)
            {
                foreach (TaskCompletionSource<bool> tcs in tcsAry)
                {
                    MaybeSetException(args, tcs);
                }
            }
        }

        private static void MaybeSetException(ShutdownEventArgs args, TaskCompletionSource<bool> tcs)
        {
            if (args.Initiator != ShutdownInitiator.Application)
            {
                Exception ex = args.Exception ?? new Exception(args.ReplyText);
                tcs.TrySetException(ex);
            }
        }
    }
}
