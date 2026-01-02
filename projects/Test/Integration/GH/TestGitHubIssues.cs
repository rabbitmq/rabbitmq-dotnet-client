// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Xunit;
using Xunit.Abstractions;

#nullable enable

namespace Test.Integration.GH
{
    public class TestGitHubIssues : IntegrationFixture
    {
        public TestGitHubIssues(ITestOutputHelper output) : base(output)
        {
        }

        public override Task InitializeAsync()
        {
            // NB: nothing to do here since each test creates its own factory,
            // connections and channels
            Assert.Null(_connFactory);
            Assert.Null(_conn);
            Assert.Null(_channel);
            return Task.CompletedTask;
        }

        [Fact]
        public async Task TestBasicConsumeCancellation_GH1750()
        {
            /*
             * Note:
             * Testing that the task is actually canceled requires a hacked RabbitMQ server.
             * Modify deps/rabbit/src/rabbit_channel.erl, handle_cast for basic.consume_ok
             * Before send/2, add timer:sleep(1000), then `make run-broker`
             *
             * The _output line at the end of the test will print TaskCanceledException
             */
            Assert.Null(_connFactory);
            Assert.Null(_conn);
            Assert.Null(_channel);

            _connFactory = CreateConnectionFactory();
            _connFactory.NetworkRecoveryInterval = TimeSpan.FromMilliseconds(250);
            _connFactory.AutomaticRecoveryEnabled = true;
            _connFactory.TopologyRecoveryEnabled = true;

            _conn = await _connFactory.CreateConnectionAsync();
            _channel = await _conn.CreateChannelAsync();

            QueueDeclareOk q = await _channel.QueueDeclareAsync();

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += (o, a) =>
            {
                return Task.CompletedTask;
            };

            bool sawConnectionShutdown = false;
            _conn.ConnectionShutdownAsync += (o, ea) =>
            {
                sawConnectionShutdown = true;
                return Task.CompletedTask;
            };

            try
            {
                // Note: use this to test timeout via the passed-in RPC token
                /*
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(5));
                await _channel.BasicConsumeAsync(q.QueueName, true, consumer, cts.Token);
                */

                // Note: use these to test timeout of the continuation RPC operation
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                _channel.ContinuationTimeout = TimeSpan.FromMilliseconds(5);
                await _channel.BasicConsumeAsync(q.QueueName, true, consumer, cts.Token);
            }
            catch (Exception ex)
            {
                _output.WriteLine("ex: {0}", ex);
            }

            await Task.Delay(500);

            Assert.False(sawConnectionShutdown);
        }

        [Fact]
        public async Task TestHeartbeatTimeoutValue_GH1756()
        {
            var connectionFactory = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                RequestedHeartbeat = TimeSpan.Zero,
            };

            _conn = await connectionFactory.CreateConnectionAsync("some-name");

            Assert.True(_conn.Heartbeat != default);
        }

        [Fact]
        public async Task DisposeWhileCatchingTimeoutDeadlocksRepro_GH1759()
        {
            _connFactory = new ConnectionFactory();
            _conn = await _connFactory.CreateConnectionAsync();
            try
            {
                await _conn.CloseAsync(TimeSpan.Zero);
            }
            catch (Exception)
            {
            }

            await _conn.DisposeAsync();
        }

        [Fact]
        public async Task InvalidCredentialsShouldThrow_GH1777()
        {
            string userPass = Guid.NewGuid().ToString();

            _connFactory = new ConnectionFactory
            {
                UserName = userPass,
                Password = userPass
            };

            BrokerUnreachableException ex =
                await Assert.ThrowsAnyAsync<BrokerUnreachableException>(
                    async () => await _connFactory.CreateConnectionAsync());
            Assert.IsAssignableFrom<PossibleAuthenticationFailureException>(ex.InnerException);
        }

        [Fact]
        public async Task SendInvalidPublishMaybeClosesConnection_GH13387()
        {
            const int messageCount = 200;

            _connFactory = new ConnectionFactory();
            _conn = await _connFactory.CreateConnectionAsync();

            var opts = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true);
            _channel = await _conn.CreateChannelAsync(opts);

            await _channel.BasicQosAsync(0, 10, false);

            string queueName = GenerateQueueName();
            QueueDeclareOk q = await _channel.QueueDeclareAsync(queueName);
            Assert.Equal(queueName, q.QueueName);

            byte[] body = Encoding.ASCII.GetBytes("incoming message");
            var publishTasks = new List<ValueTask>();
            for (int i = 0; i < messageCount; i++)
            {
                ValueTask pt = _channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: queueName,
                    body: body);
                publishTasks.Add(pt);
                if (i % 20 == 0)
                {
                    foreach (ValueTask t in publishTasks)
                    {
                        await t;
                    }
                    publishTasks.Clear();
                }
            }

            foreach (ValueTask t in publishTasks)
            {
                await t;
            }
            publishTasks.Clear();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _conn.CallbackExceptionAsync += (object sender, CallbackExceptionEventArgs args) =>
            {
                if (IsVerbose)
                {
                    _output.WriteLine("_conn.CallbackExceptionAsync: {0}", args.Exception);
                }
                tcs.TrySetException(args.Exception);
                return Task.CompletedTask;
            };

            _conn.ConnectionShutdownAsync += (object sender, ShutdownEventArgs args) =>
            {
                if (args.Exception is not null)
                {
                    if (IsVerbose)
                    {
                        _output.WriteLine("_conn.ConnectionShutdownAsync: {0}", args.Exception);
                    }
                    tcs.TrySetException(args.Exception);
                }
                else
                {
                    if (IsVerbose)
                    {
                        _output.WriteLine("_conn.ConnectionShutdownAsync");
                    }
                    tcs.TrySetResult(false);
                }
                return Task.CompletedTask;
            };

            _channel.CallbackExceptionAsync += (object sender, CallbackExceptionEventArgs args) =>
            {
                if (IsVerbose)
                {
                    _output.WriteLine("_channel.CallbackExceptionAsync: {0}", args.Exception);
                }
                tcs.TrySetException(args.Exception);
                return Task.CompletedTask;
            };

            _channel.ChannelShutdownAsync += (object sender, ShutdownEventArgs args) =>
            {
                if (args.Exception is not null)
                {
                    if (IsVerbose)
                    {
                        _output.WriteLine("_channel.ChannelShutdownAsync: {0}", args.Exception);
                    }
                    tcs.TrySetException(args.Exception);
                }
                else
                {
                    if (IsVerbose)
                    {
                        _output.WriteLine("_channel.ChannelShutdownAsync");
                    }
                    tcs.TrySetResult(false);
                }
                return Task.CompletedTask;
            };

            var ackExceptions = new List<Exception>();
            var publishExceptions = new List<Exception>();
            var props = new BasicProperties { Expiration = "-1" };
            int receivedCounter = 0;
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (object sender, BasicDeliverEventArgs args) =>
            {
                var c = (AsyncEventingBasicConsumer)sender;
                IChannel ch = c.Channel;
                try
                {
                    await ch.BasicAckAsync(args.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    ackExceptions.Add(ex);
                }

                try
                {
                    await ch.BasicPublishAsync(exchange: string.Empty, routingKey: queueName,
                        mandatory: true, basicProperties: props, body: body);
                }
                catch (Exception ex)
                {
                    publishExceptions.Add(ex);
                }

                if (Interlocked.Increment(ref receivedCounter) >= messageCount)
                {
                    tcs.SetResult(true);
                }
            };

            consumer.ShutdownAsync += (object sender, ShutdownEventArgs args) =>
            {
                if (IsVerbose)
                {
                    _output.WriteLine("consumer.ShutdownAsync");
                }
                return Task.CompletedTask;
            };

            await _channel.BasicConsumeAsync(queueName, false, consumer);

            await tcs.Task;

            if (IsVerbose)
            {
                _output.WriteLine("saw {0} ackExceptions", ackExceptions.Count);
                _output.WriteLine("saw {0} publishExceptions", publishExceptions.Count);
            }
        }

        [Fact]
        public async Task MaybeSomethingUpWithRateLimiter_GH1793()
        {
            const int messageCount = 16;

            _connFactory = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true
            };

            _conn = await _connFactory.CreateConnectionAsync();

            var channelOpts = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true,
                outstandingPublisherConfirmationsRateLimiter: new NeverAcquiredRateLimiter()
            );

            _channel = await _conn.CreateChannelAsync(channelOpts);

            var properties = new BasicProperties
            {
                DeliveryMode = DeliveryModes.Persistent
            };

            for (int i = 0; i < messageCount; i++)
            {
                int retryCount = 0;
                const int maxRetries = 3;
                while (retryCount <= maxRetries)
                {
                    try
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes("message");
                        await Assert.ThrowsAnyAsync<InvalidOperationException>(async () =>
                        {
                            await _channel.BasicPublishAsync(string.Empty, string.Empty, true, properties, bytes);
                        });
                        break;
                    }
                    catch (SemaphoreFullException ex0)
                    {
                        _output.WriteLine("{0} ex: {1}", _testDisplayName, ex0);
                        retryCount++;
                    }
                    catch (PublishException)
                    {
                        retryCount++;
                    }
                }
            }
        }
    }
}
