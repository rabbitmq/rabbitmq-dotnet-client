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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestFloodPublishing : IntegrationFixture
    {
        private static readonly TimeSpan TenSeconds = TimeSpan.FromSeconds(10);
        private readonly byte[] _body = GetRandomBody(2048);

        public TestFloodPublishing(ITestOutputHelper output) : base(output)
        {
        }

        public override Task InitializeAsync()
        {
            // NB: each test sets itself up
            return Task.CompletedTask;
        }

        [Fact]
        public async Task TestUnthrottledFloodPublishing()
        {
            bool sawUnexpectedShutdown = false;
            _connFactory = CreateConnectionFactory();
            _connFactory.RequestedHeartbeat = TimeSpan.FromSeconds(60);
            _connFactory.AutomaticRecoveryEnabled = false;
            _conn = await _connFactory.CreateConnectionAsync();
            Assert.IsNotType<RabbitMQ.Client.Framing.AutorecoveringConnection>(_conn);
            _channel = await _conn.CreateChannelAsync();

            _conn.ConnectionShutdownAsync += (_, ea) =>
            {
                HandleConnectionShutdown(_conn, ea, (args) =>
                {
                    if (args.Initiator != ShutdownInitiator.Application)
                    {
                        sawUnexpectedShutdown = true;
                    }
                });
                return Task.CompletedTask;
            };

            _channel.ChannelShutdownAsync += (o, ea) =>
            {
                HandleChannelShutdown(_channel, ea, (args) =>
                {
                    if (args.Initiator != ShutdownInitiator.Application)
                    {
                        sawUnexpectedShutdown = true;
                    }
                });

                return Task.CompletedTask;
            };

            var stopwatch = Stopwatch.StartNew();
            int i = 0;
            try
            {
                for (i = 0; i < 65535 * 64; i++)
                {
                    if (i % 65536 == 0)
                    {
                        if (stopwatch.Elapsed > TenSeconds)
                        {
                            break;
                        }
                    }

                    await _channel.BasicPublishAsync(CachedString.Empty, CachedString.Empty, _body);
                }
            }
            finally
            {
                stopwatch.Stop();
            }

            Assert.True(_conn.IsOpen);
            Assert.False(sawUnexpectedShutdown);
        }

        [Fact]
        public async Task TestMultithreadFloodPublishing()
        {
            _connFactory = CreateConnectionFactory();
            _connFactory.AutomaticRecoveryEnabled = false;

            _conn = await _connFactory.CreateConnectionAsync();
            Assert.IsNotType<RabbitMQ.Client.Framing.AutorecoveringConnection>(_conn);
            _channel = await _conn.CreateChannelAsync();

            string message = "Hello from test TestMultithreadFloodPublishing";
            byte[] sendBody = _encoding.GetBytes(message);
            int publishCount = 4096;
            int receivedCount = 0;

            var allMessagesSeenTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _conn.ConnectionShutdownAsync += (o, ea) =>
            {
                HandleConnectionShutdown(_conn, ea, (args) =>
                {
                    if (args.Initiator != ShutdownInitiator.Application)
                    {
                        receivedCount = -1;
                        allMessagesSeenTcs.TrySetException(args.Exception);
                    }
                });
                return Task.CompletedTask;
            };

            _channel.ChannelShutdownAsync += (o, ea) =>
            {
                HandleChannelShutdown(_channel, ea, (args) =>
                {
                    if (args.Initiator != ShutdownInitiator.Application)
                    {
                        receivedCount = -1;
                        allMessagesSeenTcs.TrySetException(args.Exception);
                    }
                });
                return Task.CompletedTask;
            };

            QueueDeclareOk q = await _channel.QueueDeclareAsync(queue: string.Empty,
                    passive: false, durable: false, exclusive: false, autoDelete: true, arguments: null);
            string queueName = q.QueueName;

            Task pub = Task.Run(async () =>
            {
                bool stop = false;
                using (IConnection publishConnection = await _connFactory.CreateConnectionAsync())
                {
                    publishConnection.ConnectionShutdownAsync += (o, ea) =>
                    {
                        HandleConnectionShutdown(_conn, ea, (args) =>
                        {
                            if (args.Initiator != ShutdownInitiator.Application)
                            {
                                receivedCount = -1;
                                allMessagesSeenTcs.TrySetException(args.Exception);
                            }
                        });
                        return Task.CompletedTask;
                    };

                    using (IChannel publishChannel = await publishConnection.CreateChannelAsync())
                    {
                        await publishChannel.ConfirmSelectAsync();

                        publishChannel.ChannelShutdownAsync += (o, ea) =>
                        {
                            HandleChannelShutdown(publishChannel, ea, (args) =>
                            {
                                if (args.Initiator != ShutdownInitiator.Application)
                                {
                                    stop = true;
                                    allMessagesSeenTcs.TrySetException(args.Exception);
                                }
                            });
                            return Task.CompletedTask;
                        };

                        for (int i = 0; i < publishCount && false == stop; i++)
                        {
                            await publishChannel.BasicPublishAsync(string.Empty, queueName, true, sendBody);
                        }

                        await publishChannel.WaitForConfirmsOrDieAsync();
                        await publishChannel.CloseAsync();
                    }

                    await publishConnection.CloseAsync();
                }
            });

            var cts = new CancellationTokenSource(WaitSpan);
            CancellationTokenRegistration ctsr = cts.Token.Register(() =>
            {
                allMessagesSeenTcs.TrySetCanceled();
            });

            try
            {
                using (IConnection consumeConnection = await _connFactory.CreateConnectionAsync())
                {
                    consumeConnection.ConnectionShutdownAsync += (o, ea) =>
                    {
                        HandleConnectionShutdown(_conn, ea, (args) =>
                        {
                            if (args.Initiator != ShutdownInitiator.Application)
                            {
                                receivedCount = -1;
                                allMessagesSeenTcs.TrySetException(args.Exception);
                            }
                        });
                        return Task.CompletedTask;
                    };

                    using (IChannel consumeChannel = await consumeConnection.CreateChannelAsync())
                    {
                        consumeChannel.ChannelShutdownAsync += (o, ea) =>
                        {
                            HandleChannelShutdown(consumeChannel, ea, (args) =>
                            {
                                if (args.Initiator != ShutdownInitiator.Application)
                                {
                                    allMessagesSeenTcs.TrySetException(args.Exception);
                                }
                            });
                            return Task.CompletedTask;
                        };

                        var consumer = new AsyncEventingBasicConsumer(consumeChannel);
                        consumer.ReceivedAsync += async (o, a) =>
                        {
                            string receivedMessage = _encoding.GetString(a.Body.ToArray());
                            Assert.Equal(message, receivedMessage);
                            if (Interlocked.Increment(ref receivedCount) == publishCount)
                            {
                                allMessagesSeenTcs.SetResult(true);
                            }
                            await Task.Yield();
                        };

                        await consumeChannel.BasicConsumeAsync(queue: queueName, autoAck: true,
                            consumerTag: string.Empty, noLocal: false, exclusive: false,
                            arguments: null, consumer: consumer);

                        Assert.True(await allMessagesSeenTcs.Task);
                        await consumeChannel.CloseAsync();
                    }

                    await consumeConnection.CloseAsync();
                }

                await pub;
                Assert.Equal(publishCount, receivedCount);
            }
            finally
            {
                cts.Dispose();
                ctsr.Dispose();
            }
        }
    }
}
