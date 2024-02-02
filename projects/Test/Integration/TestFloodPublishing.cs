﻿// This source code is dual-licensed under the Apache License, version
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
            Assert.IsNotType<RabbitMQ.Client.Framing.Impl.AutorecoveringConnection>(_conn);
            _channel = await _conn.CreateChannelAsync();

            _conn.ConnectionShutdown += (_, ea) =>
            {
                HandleConnectionShutdown(_conn, ea, (args) =>
                {
                    if (args.Initiator == ShutdownInitiator.Peer)
                    {
                        sawUnexpectedShutdown = true;
                    }
                });
            };

            _channel.ChannelShutdown += (o, ea) =>
            {
                HandleChannelShutdown(_channel, ea, (args) =>
                {
                    if (args.Initiator == ShutdownInitiator.Peer)
                    {
                        sawUnexpectedShutdown = true;
                    }
                });
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
            _connFactory.DispatchConsumersAsync = true;
            _connFactory.AutomaticRecoveryEnabled = false;

            _conn = await _connFactory.CreateConnectionAsync();
            Assert.IsNotType<RabbitMQ.Client.Framing.Impl.AutorecoveringConnection>(_conn);
            _channel = await _conn.CreateChannelAsync();

            string message = "Hello from test TestMultithreadFloodPublishing";
            byte[] sendBody = _encoding.GetBytes(message);
            int publishCount = 4096;
            int receivedCount = 0;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _conn.ConnectionShutdown += (o, ea) =>
            {
                HandleConnectionShutdown(_conn, ea, (args) =>
                {
                    if (args.Initiator == ShutdownInitiator.Peer)
                    {
                        receivedCount = -1;
                        tcs.SetResult(false);
                    }
                });
            };

            _channel.ChannelShutdown += (o, ea) =>
            {
                HandleChannelShutdown(_channel, ea, (args) =>
                {
                    if (args.Initiator == ShutdownInitiator.Peer)
                    {
                        receivedCount = -1;
                        tcs.SetResult(false);
                    }
                });
            };

            QueueDeclareOk q = await _channel.QueueDeclareAsync(queue: string.Empty,
                    passive: false, durable: false, exclusive: true, autoDelete: false, arguments: null);
            string queueName = q.QueueName;

            Task pub = Task.Run(async () =>
            {
                bool stop = false;
                using (IChannel pubCh = await _conn.CreateChannelAsync())
                {
                    await pubCh.ConfirmSelectAsync();

                    pubCh.ChannelShutdown += (o, ea) =>
                    {
                        HandleChannelShutdown(pubCh, ea, (args) =>
                        {
                            if (args.Initiator == ShutdownInitiator.Peer)
                            {
                                stop = true;
                                tcs.TrySetResult(false);
                            }
                        });
                    };

                    for (int i = 0; i < publishCount && false == stop; i++)
                    {
                        await pubCh.BasicPublishAsync(string.Empty, queueName, sendBody, true);
                    }

                    await pubCh.WaitForConfirmsOrDieAsync();
                    await pubCh.CloseAsync();
                }
            });

            var cts = new CancellationTokenSource(WaitSpan);
            CancellationTokenRegistration ctsr = cts.Token.Register(() =>
            {
                tcs.TrySetResult(false);
            });

            try
            {
                using (IChannel consumeCh = await _conn.CreateChannelAsync())
                {
                    consumeCh.ChannelShutdown += (o, ea) =>
                    {
                        HandleChannelShutdown(consumeCh, ea, (args) =>
                        {
                            if (args.Initiator == ShutdownInitiator.Peer)
                            {
                                tcs.TrySetResult(false);
                            }
                        });
                    };

                    var consumer = new AsyncEventingBasicConsumer(consumeCh);
                    consumer.Received += async (o, a) =>
                    {
                        string receivedMessage = _encoding.GetString(a.Body.ToArray());
                        Assert.Equal(message, receivedMessage);
                        if (Interlocked.Increment(ref receivedCount) == publishCount)
                        {
                            tcs.SetResult(true);
                        }
                        await Task.Yield();
                    };

                    await consumeCh.BasicConsumeAsync(queue: queueName, autoAck: true,
                        consumerTag: string.Empty, noLocal: false, exclusive: false,
                        arguments: null, consumer: consumer);

                    Assert.True(await tcs.Task);
                    await consumeCh.CloseAsync();
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
