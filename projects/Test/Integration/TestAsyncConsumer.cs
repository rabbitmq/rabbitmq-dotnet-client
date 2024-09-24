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
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestAsyncConsumer : IntegrationFixture
    {
        private const ushort ConsumerDispatchConcurrency = 2;

        private readonly ShutdownEventArgs _closeArgs = new ShutdownEventArgs(ShutdownInitiator.Application, Constants.ReplySuccess, "normal shutdown");

        public TestAsyncConsumer(ITestOutputHelper output)
            : base(output, consumerDispatchConcurrency: ConsumerDispatchConcurrency)
        {
        }

        [Fact]
        public async Task TestBasicRoundtripConcurrent()
        {
            await ValidateConsumerDispatchConcurrency();

            AddCallbackExceptionHandlers();
            _channel.DefaultConsumer = new DefaultAsyncConsumer(_channel, "_channel,", _output);

            QueueDeclareOk q = await _channel.QueueDeclareAsync();

            const int length = 4096;
            (byte[] body1, byte[] body2) = GenerateTwoBodies(length);

            await _channel.BasicPublishAsync("", q.QueueName, body1);
            await _channel.BasicPublishAsync("", q.QueueName, body2);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            var publish1SyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var publish2SyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var tokenSource = new CancellationTokenSource(WaitSpan);
            CancellationTokenRegistration ctsr = tokenSource.Token.Register(() =>
            {
                _output.WriteLine("publish1SyncSource.Task Status: {0}", publish1SyncSource.Task.Status);
                _output.WriteLine("publish2SyncSource.Task Status: {0}", publish2SyncSource.Task.Status);
                publish1SyncSource.TrySetCanceled();
                publish2SyncSource.TrySetCanceled();
            });

            bool body1Received = false;
            bool body2Received = false;
            try
            {
                _conn.ConnectionShutdownAsync += (o, ea) =>
                {
                    HandleConnectionShutdown(_conn, ea, (args) =>
                    {
                        MaybeSetException(args, publish1SyncSource, publish2SyncSource);
                    });
                    return Task.CompletedTask;
                };

                _channel.ChannelShutdownAsync += (o, ea) =>
                {
                    HandleChannelShutdown(_channel, ea, (args) =>
                    {
                        MaybeSetException(args, publish1SyncSource, publish2SyncSource);
                    });
                    return Task.CompletedTask;
                };

                consumer.ReceivedAsync += (o, a) =>
                {
                    if (ByteArraysEqual(a.Body.Span, body1))
                    {
                        body1Received = true;
                        publish1SyncSource.TrySetResult(true);
                    }
                    else if (ByteArraysEqual(a.Body.Span, body2))
                    {
                        body2Received = true;
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

                try
                {
                    bool result1 = await publish1SyncSource.Task;
                    Assert.True(result1, $"1 - Non concurrent dispatch lead to deadlock after {WaitSpan}");
                    bool result2 = await publish2SyncSource.Task;
                    Assert.True(result2, $"2 - Non concurrent dispatch lead to deadlock after {WaitSpan}");
                }
                catch (Exception ex)
                {
                    _output.WriteLine("EXCEPTION: {0}", ex);
                    _output.WriteLine("body1Received: {0}, body2Received: {1}", body1Received, body2Received);
                    _output.WriteLine("publish1SyncSource.Task Status: {0}", publish1SyncSource.Task.Status);
                    _output.WriteLine("publish2SyncSource.Task Status: {0}", publish2SyncSource.Task.Status);
                    throw;
                }
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
            await ValidateConsumerDispatchConcurrency();

            AddCallbackExceptionHandlers();
            _channel.DefaultConsumer = new DefaultAsyncConsumer(_channel, "_channel,", _output);

            const int publish_total = 4096;
            const int length = 512;
            string queueName = GenerateQueueName();

            (byte[] body1, byte[] body2) = GenerateTwoBodies(length);

            var publish1SyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var publish2SyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var consumerSyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var tokenSource = new CancellationTokenSource(WaitSpan);
            CancellationTokenRegistration ctsr = tokenSource.Token.Register(() =>
            {
                _output.WriteLine("publish1SyncSource.Task Status: {0}", publish1SyncSource.Task.Status);
                _output.WriteLine("publish2SyncSource.Task Status: {0}", publish2SyncSource.Task.Status);
                _output.WriteLine("consumerSyncSource.Task Status: {0}", consumerSyncSource.Task.Status);
                publish1SyncSource.TrySetCanceled();
                publish2SyncSource.TrySetCanceled();
                consumerSyncSource.TrySetCanceled();
            });

            try
            {
                _conn.ConnectionShutdownAsync += (o, ea) =>
                {
                    HandleConnectionShutdown(_conn, ea, (args) =>
                    {
                        MaybeSetException(args, publish1SyncSource, publish2SyncSource);
                    });
                    return Task.CompletedTask;
                };

                _channel.ChannelShutdownAsync += (o, ea) =>
                {
                    HandleChannelShutdown(_channel, ea, (args) =>
                    {
                        MaybeSetException(args, publish1SyncSource, publish2SyncSource);
                    });
                    return Task.CompletedTask;
                };

                QueueDeclareOk q = await _channel.QueueDeclareAsync(queue: queueName, exclusive: false, autoDelete: true);
                Assert.Equal(queueName, q.QueueName);

                Task publishTask = Task.Run(async () =>
                {
                    await using IConnection publishConn = await _connFactory.CreateConnectionAsync();
                    publishConn.ConnectionShutdownAsync += (o, ea) =>
                    {
                        HandleConnectionShutdown(publishConn, ea, (args) =>
                        {
                            MaybeSetException(args, publish1SyncSource, publish2SyncSource);
                        });
                        return Task.CompletedTask;
                    };
                    await using (IChannel publishChannel = await publishConn.CreateChannelAsync(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true))
                    {
                        AddCallbackExceptionHandlers(publishConn, publishChannel);
                        publishChannel.DefaultConsumer = new DefaultAsyncConsumer(publishChannel,
                            "publishChannel,", _output);
                        publishChannel.ChannelShutdownAsync += (o, ea) =>
                        {
                            HandleChannelShutdown(publishChannel, ea, (args) =>
                            {
                                MaybeSetException(args, publish1SyncSource, publish2SyncSource);
                            });
                            return Task.CompletedTask;
                        };

                        var publishTasks = new List<Task>();
                        for (int i = 0; i < publish_total; i++)
                        {
                            publishTasks.Add(publishChannel.BasicPublishAsync(string.Empty, queueName, body1).AsTask());
                            publishTasks.Add(publishChannel.BasicPublishAsync(string.Empty, queueName, body2).AsTask());
                        }

                        await Task.WhenAll(publishTasks).WaitAsync(WaitSpan);
                        await publishChannel.CloseAsync();
                    }

                    await publishConn.CloseAsync();
                });


                int publish1_count = 0;
                int publish2_count = 0;

                Task consumeTask = Task.Run(async () =>
                {
                    await using IConnection consumeConn = await _connFactory.CreateConnectionAsync();
                    consumeConn.ConnectionShutdownAsync += (o, ea) =>
                    {
                        HandleConnectionShutdown(consumeConn, ea, (args) =>
                        {
                            MaybeSetException(ea, publish1SyncSource, publish2SyncSource);
                        });
                        return Task.CompletedTask;
                    };
                    await using (IChannel consumeChannel = await consumeConn.CreateChannelAsync())
                    {
                        AddCallbackExceptionHandlers(consumeConn, consumeChannel);
                        consumeChannel.DefaultConsumer = new DefaultAsyncConsumer(consumeChannel,
                            "consumeChannel,", _output);
                        consumeChannel.ChannelShutdownAsync += (o, ea) =>
                        {
                            HandleChannelShutdown(consumeChannel, ea, (args) =>
                            {
                                MaybeSetException(ea, publish1SyncSource, publish2SyncSource);
                            });
                            return Task.CompletedTask;
                        };

                        var consumer = new AsyncEventingBasicConsumer(consumeChannel);
                        consumer.ReceivedAsync += (o, a) =>
                        {
                            if (ByteArraysEqual(a.Body.ToArray(), body1))
                            {
                                if (Interlocked.Increment(ref publish1_count) >= publish_total)
                                {
                                    publish1SyncSource.TrySetResult(true);
                                }
                            }
                            else if (ByteArraysEqual(a.Body.ToArray(), body2))
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
                });

                try
                {
                    await publishTask;
                    bool result1 = await publish1SyncSource.Task;
                    Assert.True(result1, $"Non concurrent dispatch lead to deadlock after {WaitSpan}");
                    bool result2 = await publish2SyncSource.Task;
                    Assert.True(result2, $"Non concurrent dispatch lead to deadlock after {WaitSpan}");
                }
                catch (Exception ex)
                {
                    _output.WriteLine("EXCEPTION: {0}", ex);
                    _output.WriteLine("publish1_count: {0}, publish2_count: {1}", publish1_count, publish2_count);
                    _output.WriteLine("publishTask Status: {0}", publishTask.Status);
                    _output.WriteLine("publish1SyncSource.Task Status: {0}", publish1SyncSource.Task.Status);
                    _output.WriteLine("publish2SyncSource.Task Status: {0}", publish2SyncSource.Task.Status);
                    _output.WriteLine("consumerSyncSource.Task Status: {0}", consumerSyncSource.Task.Status);
                    throw;
                }
            }
            finally
            {
                consumerSyncSource.TrySetResult(true);
                ctsr.Dispose();
                tokenSource.Dispose();
            }
        }

        [Fact]
        public async Task TestBasicRejectAsync()
        {
            await ValidateConsumerDispatchConcurrency();

            string queueName = GenerateQueueName();

            var publishSyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellationTokenSource = new CancellationTokenSource(TestTimeout);
            CancellationTokenRegistration ctsr = cancellationTokenSource.Token.Register(() =>
            {
                publishSyncSource.SetCanceled();
            });

            try
            {
                _conn.ConnectionShutdownAsync += (o, ea) =>
                {
                    HandleConnectionShutdown(_conn, ea, (args) =>
                    {
                        MaybeSetException(args, publishSyncSource);
                    });
                    return Task.CompletedTask;
                };

                _channel.ChannelShutdownAsync += (o, ea) =>
                {
                    HandleChannelShutdown(_channel, ea, (args) =>
                    {
                        MaybeSetException(args, publishSyncSource);
                    });
                    return Task.CompletedTask;
                };

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.ReceivedAsync += async (object sender, BasicDeliverEventArgs args) =>
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
            await ValidateConsumerDispatchConcurrency();

            string queueName = GenerateQueueName();

            const int messageCount = 1024;
            int messagesReceived = 0;

            var publishSyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _conn.ConnectionShutdownAsync += (o, ea) =>
            {
                HandleConnectionShutdown(_conn, ea, (args) =>
                {
                    MaybeSetException(args, publishSyncSource);
                });
                return Task.CompletedTask;
            };

            _channel.ChannelShutdownAsync += (o, ea) =>
            {
                HandleChannelShutdown(_channel, ea, (args) =>
                {
                    MaybeSetException(args, publishSyncSource);
                });
                return Task.CompletedTask;
            };

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (object sender, BasicDeliverEventArgs args) =>
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
            await ValidateConsumerDispatchConcurrency();

            var publishSyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _conn.ConnectionShutdownAsync += (o, ea) =>
            {
                HandleConnectionShutdown(_conn, ea, (args) =>
                {
                    MaybeSetException(ea, publishSyncSource);
                });
                return Task.CompletedTask;
            };

            _channel.ChannelShutdownAsync += (o, ea) =>
            {
                HandleChannelShutdown(_channel, ea, (args) =>
                {
                    MaybeSetException(ea, publishSyncSource);
                });
                return Task.CompletedTask;
            };

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (object sender, BasicDeliverEventArgs args) =>
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
        public async Task TestDeclarationOfManyAutoDeleteQueuesWithTransientConsumer()
        {
            await ValidateConsumerDispatchConcurrency();

            AssertRecordedQueues((RabbitMQ.Client.Framing.AutorecoveringConnection)_conn, 0);
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
            AssertRecordedQueues((RabbitMQ.Client.Framing.AutorecoveringConnection)_conn, 0);
        }

        [Fact]
        public async Task TestCreateChannelWithinAsyncConsumerCallback_GH650()
        {
            await ValidateConsumerDispatchConcurrency();

            string exchangeName = GenerateExchangeName();
            string queue1Name = GenerateQueueName();
            string queue2Name = GenerateQueueName();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource(WaitSpan);
            using CancellationTokenRegistration ctr = cts.Token.Register(() =>
            {
                tcs.SetCanceled();
            });

            _conn.ConnectionShutdownAsync += (o, ea) =>
            {
                HandleConnectionShutdown(_conn, ea, (args) =>
                {
                    MaybeSetException(ea, tcs);
                });
                return Task.CompletedTask;
            };

            _channel.ChannelShutdownAsync += (o, ea) =>
            {
                HandleChannelShutdown(_channel, ea, (args) =>
                {
                    MaybeSetException(ea, tcs);
                });
                return Task.CompletedTask;
            };

            // queue1 -> produce click to queue2
            // click -> exchange
            // queue2 -> consume click from queue1
            await _channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Direct, autoDelete: true);
            await _channel.QueueDeclareAsync(queue1Name);
            await _channel.QueueBindAsync(queue1Name, exchangeName, queue1Name);
            await _channel.QueueDeclareAsync(queue2Name);
            await _channel.QueueBindAsync(queue2Name, exchangeName, queue2Name);

            var consumer1 = new AsyncEventingBasicConsumer(_channel);
            consumer1.ReceivedAsync += async (sender, args) =>
            {
                await using IChannel innerChannel = await _conn.CreateChannelAsync(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);
                await innerChannel.BasicPublishAsync(exchangeName, queue2Name,
                    mandatory: true,
                    body: Encoding.ASCII.GetBytes(nameof(TestCreateChannelWithinAsyncConsumerCallback_GH650)));
                await innerChannel.CloseAsync();
            };
            await _channel.BasicConsumeAsync(queue1Name, autoAck: true, consumer1);

            var consumer2 = new AsyncEventingBasicConsumer(_channel);
            consumer2.ReceivedAsync += async (sender, args) =>
            {
                tcs.TrySetResult(true);
                await Task.Yield();
            };
            await _channel.BasicConsumeAsync(queue2Name, autoAck: true, consumer2);

            await _channel.BasicPublishAsync(exchangeName, queue1Name, body: GetRandomBody(1024));

            Assert.True(await tcs.Task);
        }

        [Fact]
        public async Task TestCloseWithinEventHandler_GH1567()
        {
            await ValidateConsumerDispatchConcurrency();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            QueueDeclareOk q = await _channel.QueueDeclareAsync();
            string queueName = q.QueueName;

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, eventArgs) =>
            {
                await _channel.BasicCancelAsync(eventArgs.ConsumerTag);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                _channel.CloseAsync().ContinueWith(async (_) =>
                {
                    await _channel.DisposeAsync();
                    _channel = null;
                });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                tcs.TrySetResult(true);
            };

            await _channel.BasicConsumeAsync(consumer: consumer, queue: queueName, autoAck: true);

            var bp = new BasicProperties();

            await _channel.BasicPublishAsync(exchange: string.Empty, routingKey: queueName,
                basicProperties: bp, mandatory: true, body: GetRandomBody(64));

            Assert.True(await tcs.Task);
        }

        private async Task ValidateConsumerDispatchConcurrency()
        {
            ushort expectedConsumerDispatchConcurrency = (ushort)S_Random.Next(3, 10);
            AutorecoveringChannel autorecoveringChannel = (AutorecoveringChannel)_channel;
            Assert.Equal(ConsumerDispatchConcurrency, autorecoveringChannel.ConsumerDispatcher.Concurrency);
            Assert.Equal(_consumerDispatchConcurrency, autorecoveringChannel.ConsumerDispatcher.Concurrency);
            await using IChannel ch = await _conn.CreateChannelAsync(
                consumerDispatchConcurrency: expectedConsumerDispatchConcurrency);
            AutorecoveringChannel ach = (AutorecoveringChannel)ch;
            Assert.Equal(expectedConsumerDispatchConcurrency, ach.ConsumerDispatcher.Concurrency);
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

        private static bool ByteArraysEqual(ReadOnlySpan<byte> a1, ReadOnlySpan<byte> a2)
        {
            return a1.SequenceEqual(a2);
        }

        private static (byte[] body1, byte[] body2) GenerateTwoBodies(ushort length)
        {
            byte[] body1 = _encoding.GetBytes(new string('x', length));
            byte[] body2 = _encoding.GetBytes(new string('y', length));
            return (body1, body2);
        }

        private class DefaultAsyncConsumer : AsyncDefaultBasicConsumer
        {
            private readonly string _logPrefix;
            private readonly ITestOutputHelper _output;

            public DefaultAsyncConsumer(IChannel channel, string logPrefix, ITestOutputHelper output)
                : base(channel)
            {
                _logPrefix = logPrefix;
                _output = output;
            }

            public override Task HandleBasicCancelAsync(string consumerTag,
                CancellationToken cancellationToken = default)
            {
                _output.WriteLine("[ERROR] {0} HandleBasicCancelAsync {1}", _logPrefix, consumerTag);
                return base.HandleBasicCancelAsync(consumerTag, cancellationToken);
            }

            public override Task HandleBasicCancelOkAsync(string consumerTag,
                CancellationToken cancellationToken = default)
            {
                _output.WriteLine("[ERROR] {0} HandleBasicCancelOkAsync {1}", _logPrefix, consumerTag);
                return base.HandleBasicCancelOkAsync(consumerTag, cancellationToken);
            }

            public override Task HandleBasicConsumeOkAsync(string consumerTag,
                CancellationToken cancellationToken = default)
            {
                _output.WriteLine("[ERROR] {0} HandleBasicConsumeOkAsync {1}", _logPrefix, consumerTag);
                return base.HandleBasicConsumeOkAsync(consumerTag, cancellationToken);
            }

            public override async Task HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered,
                string exchange, string routingKey, IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body,
                CancellationToken cancellationToken = default)
            {
                _output.WriteLine("[ERROR] {0} HandleBasicDeliverAsync {1}", _logPrefix, consumerTag);
                await base.HandleBasicDeliverAsync(consumerTag, deliveryTag, redelivered,
                    exchange, routingKey, properties, body,
                    cancellationToken);
            }

            public override Task HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason)
            {
                _output.WriteLine("[ERROR] {0} HandleChannelShutdownAsync", _logPrefix);
                return base.HandleChannelShutdownAsync(channel, reason);
            }

            protected override Task OnCancelAsync(string[] consumerTags,
                CancellationToken cancellationToken = default)
            {
                _output.WriteLine("[ERROR] {0} OnCancel {1}", _logPrefix, consumerTags[0]);
                return base.OnCancelAsync(consumerTags, cancellationToken);
            }
        }
    }
}
