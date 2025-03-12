// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Integration;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Toxiproxy.Net.Toxics;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestToxiproxy : IntegrationFixture
    {
        private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromSeconds(1);
        private ToxiproxyManager _toxiproxyManager;
        private int _proxyPort;

        public TestToxiproxy(ITestOutputHelper output) : base(output)
        {
        }

        public override Task InitializeAsync()
        {
            // NB: nothing to do here since each test creates its own factory,
            // connections and channels
            Assert.Null(_connFactory);
            Assert.Null(_conn);
            Assert.Null(_channel);

            if (AreToxiproxyTestsEnabled)
            {
                _toxiproxyManager = new ToxiproxyManager(_testDisplayName, IsRunningInCI, IsWindows);
                _proxyPort = ToxiproxyManager.ProxyPort;
                return _toxiproxyManager.InitializeAsync();
            }
            else
            {
                return Task.CompletedTask;
            }
        }

        public override async Task DisposeAsync()
        {
            if (AreToxiproxyTestsEnabled)
            {
                await _toxiproxyManager.DisposeAsync();
            }

            await base.DisposeAsync();
        }

        [SkippableFact]
        [Trait("Category", "Toxiproxy")]
        public async Task TestCloseConnection()
        {
            Skip.IfNot(AreToxiproxyTestsEnabled, "RABBITMQ_TOXIPROXY_TESTS is not set, skipping test");

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Port = _proxyPort;
            cf.AutomaticRecoveryEnabled = true;
            cf.NetworkRecoveryInterval = TimeSpan.FromSeconds(1);
            cf.RequestedHeartbeat = TimeSpan.FromSeconds(1);

            var messagePublishedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var connectionShutdownTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var recoverySucceededTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var testSucceededTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            Task pubTask = Task.Run(async () =>
            {
                await using IConnection conn = await cf.CreateConnectionAsync();
                conn.CallbackExceptionAsync += (s, ea) =>
                {
                    _output.WriteLine($"[ERROR] unexpected callback exception {ea.Detail} {ea.Exception}");
                    recoverySucceededTcs.SetResult(false);
                    return Task.CompletedTask;
                };

                conn.ConnectionRecoveryErrorAsync += (s, ea) =>
                {
                    _output.WriteLine($"[ERROR] connection recovery error {ea.Exception}");
                    recoverySucceededTcs.SetResult(false);
                    return Task.CompletedTask;
                };

                conn.ConnectionShutdownAsync += (s, ea) =>
                {
                    if (IsVerbose)
                    {
                        _output.WriteLine($"[INFO] connection shutdown");
                    }

                    /*
                     * Note: using TrySetResult because this callback will be called when the
                     * test exits, and connectionShutdownTcs will have already been set
                     */
                    connectionShutdownTcs.TrySetResult(true);
                    return Task.CompletedTask;
                };

                conn.RecoverySucceededAsync += (s, ea) =>
                {
                    if (IsVerbose)
                    {
                        _output.WriteLine($"[INFO] connection recovery succeeded");
                    }

                    recoverySucceededTcs.SetResult(true);
                    return Task.CompletedTask;
                };

                async Task PublishLoop()
                {
                    await using IChannel ch = await conn.CreateChannelAsync(_createChannelOptions);
                    QueueDeclareOk q = await ch.QueueDeclareAsync();
                    while (conn.IsOpen)
                    {
                        /*
                         * Note:
                         * In this test, it is possible that the connection
                         * will be closed before the ack is returned,
                         * and this await will throw an exception
                         */
                        try
                        {
                            await ch.BasicPublishAsync("", q.QueueName, GetRandomBody());
                            messagePublishedTcs.TrySetResult(true);
                        }
                        catch (AlreadyClosedException ex)
                        {
                            if (IsVerbose)
                            {
                                _output.WriteLine($"[WARNING] BasicPublishAsync ex: {ex}");
                            }
                        }
                    }

                    await ch.CloseAsync();
                }

                try
                {
                    await PublishLoop();
                }
                catch (Exception ex)
                {
                    if (IsVerbose)
                    {
                        _output.WriteLine($"[WARNING] PublishLoop ex: {ex}");
                    }
                }

                Assert.True(await testSucceededTcs.Task);
                await conn.CloseAsync();
            });

            Assert.True(await messagePublishedTcs.Task);

            Task disableProxyTask = _toxiproxyManager.DisableAsync();

            await Task.WhenAll(disableProxyTask, connectionShutdownTcs.Task);

            Task enableProxyTask = _toxiproxyManager.EnableAsync();

            Task whenAllTask = Task.WhenAll(enableProxyTask, recoverySucceededTcs.Task);
            await whenAllTask.WaitAsync(TimeSpan.FromSeconds(15));

            Assert.True(await recoverySucceededTcs.Task);

            testSucceededTcs.SetResult(true);
            await pubTask;
        }

        [SkippableFact]
        [Trait("Category", "Toxiproxy")]
        public async Task TestThatStoppedSocketResultsInHeartbeatTimeout()
        {
            Skip.IfNot(AreToxiproxyTestsEnabled, "RABBITMQ_TOXIPROXY_TESTS is not set, skipping test");

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Port = _proxyPort;
            cf.RequestedHeartbeat = _heartbeatTimeout;
            cf.AutomaticRecoveryEnabled = false;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            Task pubTask = Task.Run(async () =>
            {
                await using IConnection conn = await cf.CreateConnectionAsync();
                await using IChannel ch = await conn.CreateChannelAsync(_createChannelOptions);
                QueueDeclareOk q = await ch.QueueDeclareAsync();
                while (conn.IsOpen)
                {
                    await ch.BasicPublishAsync("", q.QueueName, GetRandomBody());
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    tcs.TrySetResult(true);
                }

                await ch.CloseAsync();
                await conn.CloseAsync();
            });

            Assert.True(await tcs.Task);

            string toxicName = $"rmq-localhost-timeout-{Now}-{GenerateShortUuid()}";
            var timeoutToxic = new TimeoutToxic
            {
                Name = toxicName
            };
            timeoutToxic.Attributes.Timeout = 0;
            timeoutToxic.Toxicity = 1.0;

            Task<TimeoutToxic> addToxicTask = _toxiproxyManager.AddToxicAsync(timeoutToxic);

            await Assert.ThrowsAsync<AlreadyClosedException>(() =>
            {
                return Task.WhenAll(addToxicTask, pubTask);
            });
        }

        [SkippableFact]
        [Trait("Category", "Toxiproxy")]
        public async Task TestTcpReset_GH1464()
        {
            Skip.IfNot(AreToxiproxyTestsEnabled, "RABBITMQ_TOXIPROXY_TESTS is not set, skipping test");

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Endpoint = new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), _proxyPort);
            cf.RequestedHeartbeat = TimeSpan.FromSeconds(5);
            cf.AutomaticRecoveryEnabled = true;

            var channelCreatedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var connectionShutdownTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            Task recoveryTask = Task.Run(async () =>
            {
                await using IConnection conn = await cf.CreateConnectionAsync();
                conn.ConnectionShutdownAsync += (o, ea) =>
                {
                    connectionShutdownTcs.SetResult(true);
                    return Task.CompletedTask;
                };

                await using (IChannel ch = await conn.CreateChannelAsync())
                {
                    channelCreatedTcs.SetResult(true);
                    await WaitForRecoveryAsync(conn);
                    await ch.CloseAsync();
                }

                await conn.CloseAsync();
            });

            Assert.True(await channelCreatedTcs.Task);

            string toxicName = $"rmq-localhost-reset_peer-{Now}-{GenerateShortUuid()}";
            var resetPeerToxic = new ResetPeerToxic
            {
                Name = toxicName
            };
            resetPeerToxic.Attributes.Timeout = 500;
            resetPeerToxic.Toxicity = 1.0;

            Task<ResetPeerToxic> addToxicTask = _toxiproxyManager.AddToxicAsync(resetPeerToxic);

            await Task.WhenAll(addToxicTask, connectionShutdownTcs.Task);

            await _toxiproxyManager.RemoveToxicAsync(toxicName);

            await recoveryTask;
        }

        [SkippableFact]
        [Trait("Category", "Toxiproxy")]
        public async Task TestPublisherConfirmationThrottling()
        {
            Skip.IfNot(AreToxiproxyTestsEnabled, "RABBITMQ_TOXIPROXY_TESTS is not set, skipping test");

            const int TotalMessageCount = 64;
            const int MaxOutstandingConfirms = 8;
            const int BatchSize = MaxOutstandingConfirms * 2;

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Endpoint = new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), _proxyPort);
            cf.RequestedHeartbeat = TimeSpan.FromSeconds(5);
            cf.AutomaticRecoveryEnabled = true;

            var channelOpts = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true,
                outstandingPublisherConfirmationsRateLimiter: new ThrottlingRateLimiter(MaxOutstandingConfirms)
            );

            var channelCreatedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var messagesPublishedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            long publishCount = 0;
            Task publishTask = Task.Run(async () =>
            {
                await using (IConnection conn = await cf.CreateConnectionAsync())
                {
                    await using (IChannel ch = await conn.CreateChannelAsync(channelOpts))
                    {
                        QueueDeclareOk q = await ch.QueueDeclareAsync();

                        channelCreatedTcs.SetResult(true);

                        try
                        {
                            var publishBatch = new List<ValueTask>();
                            while (publishCount < TotalMessageCount)
                            {
                                for (int i = 0; i < BatchSize; i++)
                                {
                                    publishBatch.Add(ch.BasicPublishAsync("", q.QueueName, GetRandomBody()));
                                }

                                foreach (ValueTask pt in publishBatch)
                                {
                                    await pt;
                                    Interlocked.Increment(ref publishCount);
                                }

                                publishBatch.Clear();
                            }

                            messagesPublishedTcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            messagesPublishedTcs.SetException(ex);
                        }
                    }
                }
            });

            await channelCreatedTcs.Task;

            string toxicName = $"rmq-localhost-bandwidth-{Now}-{GenerateShortUuid()}";
            var bandwidthToxic = new BandwidthToxic
            {
                Name = toxicName
            };
            bandwidthToxic.Attributes.Rate = 0;
            bandwidthToxic.Toxicity = 1.0;
            bandwidthToxic.Stream = ToxicDirection.DownStream;

            await Task.Delay(TimeSpan.FromSeconds(1));

            Task<BandwidthToxic> addToxicTask = _toxiproxyManager.AddToxicAsync(bandwidthToxic);

            while (true)
            {
                long publishCount0 = Interlocked.Read(ref publishCount);
                await Task.Delay(TimeSpan.FromSeconds(5));
                long publishCount1 = Interlocked.Read(ref publishCount);

                if (publishCount0 == publishCount1)
                {
                    // Publishing has "settled" due to being blocked
                    break;
                }
            }

            await addToxicTask.WaitAsync(WaitSpan);
            await _toxiproxyManager.RemoveToxicAsync(toxicName).WaitAsync(WaitSpan);

            await messagesPublishedTcs.Task.WaitAsync(WaitSpan);
            await publishTask.WaitAsync(WaitSpan);

            Assert.Equal(TotalMessageCount, publishCount);
        }

        [SkippableFact]
        [Trait("Category", "Toxiproxy")]
        public async Task TestRpcContinuationTimeout_GH1802()
        {
            Skip.IfNot(AreToxiproxyTestsEnabled, "RABBITMQ_TOXIPROXY_TESTS is not set, skipping test");

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Endpoint = new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), _proxyPort);
            cf.ContinuationTimeout = TimeSpan.FromSeconds(1);
            cf.AutomaticRecoveryEnabled = false;
            cf.TopologyRecoveryEnabled = false;

            await using IConnection conn = await cf.CreateConnectionAsync();
            await using IChannel ch = await conn.CreateChannelAsync();

            string toxicName = $"rmq-localhost-bandwidth-{Now}-{GenerateShortUuid()}";
            var bandwidthToxic = new BandwidthToxic
            {
                Name = toxicName
            };
            bandwidthToxic.Attributes.Rate = 0;
            bandwidthToxic.Toxicity = 1.0;
            bandwidthToxic.Stream = ToxicDirection.DownStream;

            Task<BandwidthToxic> addToxicTask = _toxiproxyManager.AddToxicAsync(bandwidthToxic);

            await Task.Delay(TimeSpan.FromSeconds(1));

            bool sawContinuationTimeout = false;
            try
            {
                ch.ContinuationTimeout = TimeSpan.FromMilliseconds(5);
                QueueDeclareOk q = await ch.QueueDeclareAsync();
            }
            catch (OperationCanceledException)
            {
                sawContinuationTimeout = true;
            }

            await _toxiproxyManager.RemoveToxicAsync(toxicName);

            await ch.CloseAsync();

            Assert.True(sawContinuationTimeout);
        }

        private bool AreToxiproxyTestsEnabled
        {
            get
            {
                // TODO rabbitmq/rabbitmq-dotnet-client#1802
                return true;
                /*
                string s = Environment.GetEnvironmentVariable("RABBITMQ_TOXIPROXY_TESTS");

                if (string.IsNullOrEmpty(s))
                {
                    return false;
                }

                if (bool.TryParse(s, out bool enabled))
                {
                    return enabled;
                }

                return false;
                */
            }
        }
    }
}
