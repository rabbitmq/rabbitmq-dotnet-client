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
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestConcurrentAccessWithSharedConnectionAsync : IntegrationFixture
    {
        private const ushort _messageCount = 200;

        public TestConcurrentAccessWithSharedConnectionAsync(ITestOutputHelper output)
            : base(output, openChannel: false)
        {
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            _conn.ConnectionShutdown += HandleConnectionShutdown;
        }

        [Fact]
        public Task TestConcurrentChannelOpenAndPublishingWithBlankMessagesAsync()
        {
            return TestConcurrentChannelOpenAndPublishingWithBodyAsync(Array.Empty<byte>(), 30);
        }

        [Fact]
        public Task TestConcurrentChannelOpenAndPublishingSize64Async()
        {
            return TestConcurrentChannelOpenAndPublishingWithBodyOfSizeAsync(64);
        }

        [Fact]
        public Task TestConcurrentChannelOpenAndPublishingSize256Async()
        {
            return TestConcurrentChannelOpenAndPublishingWithBodyOfSizeAsync(256);
        }

        [Fact]
        public Task TestConcurrentChannelOpenAndPublishingSize1024Async()
        {
            return TestConcurrentChannelOpenAndPublishingWithBodyOfSizeAsync(1024);
        }

        private Task TestConcurrentChannelOpenAndPublishingWithBodyOfSizeAsync(ushort length, int iterations = 30)
        {
            byte[] body = GetRandomBody(length);
            return TestConcurrentChannelOpenAndPublishingWithBodyAsync(body, iterations);
        }

        private Task TestConcurrentChannelOpenAndPublishingWithBodyAsync(byte[] body, int iterations)
        {
            return TestConcurrentChannelOperationsAsync(async (conn) =>
            {
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var tokenSource = new CancellationTokenSource(LongWaitSpan);
                CancellationTokenRegistration ctsr = tokenSource.Token.Register(() =>
                {
                    tcs.TrySetResult(false);
                });

                try
                {
                    using (IChannel ch = await _conn.CreateChannelAsync())
                    {
                        ch.ChannelShutdown += (o, ea) =>
                        {
                            HandleChannelShutdown(ch, ea, (args) =>
                            {
                                if (args.Initiator != ShutdownInitiator.Application)
                                {
                                    tcs.TrySetException(args.Exception);
                                }
                            });
                        };

                        await ch.ConfirmSelectAsync();

                        ch.BasicAcks += (object sender, BasicAckEventArgs e) =>
                        {
                            if (e.DeliveryTag >= _messageCount)
                            {
                                tcs.SetResult(true);
                            }
                        };

                        ch.BasicNacks += (object sender, BasicNackEventArgs e) =>
                        {
                            tcs.SetResult(false);
                            _output.WriteLine($"channel #{ch.ChannelNumber} saw a nack, deliveryTag: {e.DeliveryTag}, multiple: {e.Multiple}");
                        };

                        QueueDeclareOk q = await ch.QueueDeclareAsync(queue: string.Empty, passive: false, durable: false, exclusive: true, autoDelete: true, arguments: null);
                        for (ushort j = 0; j < _messageCount; j++)
                        {
                            await ch.BasicPublishAsync("", q.QueueName, body, mandatory: true);
                        }

                        Assert.True(await tcs.Task);
                        await ch.CloseAsync();
                    }
                }
                finally
                {
                    tokenSource.Dispose();
                    ctsr.Dispose();
                }
            }, iterations);
        }

        private Task TestConcurrentChannelOperationsAsync(Func<IConnection, Task> actions, int iterations)
        {
            return TestConcurrentChannelOperationsAsync(actions, iterations, LongWaitSpan);
        }

        private async Task TestConcurrentChannelOperationsAsync(Func<IConnection, Task> action, int iterations, TimeSpan timeout)
        {
            var tasks = new List<Task>();
            for (int i = 0; i < _processorCount; i++)
            {
                for (int j = 0; j < iterations; j++)
                {
                    tasks.Add(action(_conn));
                }
            }
            await AssertRanToCompletion(tasks);

            // incorrect frame interleaving in these tests will result
            // in an unrecoverable connection-level exception, thus
            // closing the connection
            Assert.True(_conn.IsOpen);
        }
    }
}
