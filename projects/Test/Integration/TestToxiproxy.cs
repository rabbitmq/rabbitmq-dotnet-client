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
using System.Net;
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

            return Task.CompletedTask;
        }

        [SkippableFact]
        [Trait("Category", "Toxiproxy")]
        public async Task TestCloseConnection()
        {
            Skip.IfNot(AreToxiproxyTestsEnabled, "RABBITMQ_TOXIPROXY_TESTS is not set, skipping test");

            using (var pm = new ToxiproxyManager(_testDisplayName, IsRunningInCI, IsWindows))
            {
                await pm.InitializeAsync();

                ConnectionFactory cf = CreateConnectionFactory();
                cf.Port = pm.ProxyPort;
                cf.AutomaticRecoveryEnabled = true;
                cf.NetworkRecoveryInterval = TimeSpan.FromSeconds(1);
                cf.RequestedHeartbeat = TimeSpan.FromSeconds(1);

                var messagePublishedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var connectionShutdownTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var recoverySucceededTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var testSucceededTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                Task pubTask = Task.Run(async () =>
                {
                    using (IConnection conn = await cf.CreateConnectionAsync())
                    {
                        conn.CallbackException += (s, ea) =>
                        {
                            _output.WriteLine($"[ERROR] unexpected callback exception {ea.Detail} {ea.Exception}");
                            recoverySucceededTcs.SetResult(false);
                        };

                        conn.ConnectionRecoveryError += (s, ea) =>
                        {
                            _output.WriteLine($"[ERROR] connection recovery error {ea.Exception}");
                            recoverySucceededTcs.SetResult(false);
                        };

                        conn.ConnectionShutdown += (s, ea) =>
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
                        };

                        conn.RecoverySucceeded += (s, ea) =>
                        {
                            if (IsVerbose)
                            {
                                _output.WriteLine($"[INFO] connection recovery succeeded");
                            }

                            recoverySucceededTcs.SetResult(true);
                        };

                        async Task PublishLoop()
                        {
                            using (IChannel ch = await conn.CreateChannelAsync())
                            {
                                await ch.ConfirmSelectAsync();
                                QueueDeclareOk q = await ch.QueueDeclareAsync();
                                while (conn.IsOpen)
                                {
                                    await ch.BasicPublishAsync("", q.QueueName, GetRandomBody());
                                    messagePublishedTcs.TrySetResult(true);
                                    /*
                                     * Note:
                                     * In this test, it is possible that the connection
                                     * will be closed before the ack is returned,
                                     * and this await will throw an exception
                                     */
                                    try
                                    {
                                        await ch.WaitForConfirmsAsync();
                                    }
                                    catch (AlreadyClosedException ex)
                                    {
                                        if (IsVerbose)
                                        {
                                            _output.WriteLine($"[WARNING] WaitForConfirmsAsync ex: {ex}");
                                        }
                                    }
                                }

                                await ch.CloseAsync();
                            }
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
                    }
                });

                Assert.True(await messagePublishedTcs.Task);

                Task disableProxyTask = pm.DisableAsync();

                await Task.WhenAll(disableProxyTask, connectionShutdownTcs.Task);

                Task enableProxyTask = pm.EnableAsync();

                Task whenAllTask = Task.WhenAll(enableProxyTask, recoverySucceededTcs.Task);
                await whenAllTask.WaitAsync(TimeSpan.FromSeconds(15));

                Assert.True(await recoverySucceededTcs.Task);

                testSucceededTcs.SetResult(true);
                await pubTask;
            }
        }

        [SkippableFact]
        [Trait("Category", "Toxiproxy")]
        public async Task TestThatStoppedSocketResultsInHeartbeatTimeout()
        {
            Skip.IfNot(AreToxiproxyTestsEnabled, "RABBITMQ_TOXIPROXY_TESTS is not set, skipping test");

            using (var pm = new ToxiproxyManager(_testDisplayName, IsRunningInCI, IsWindows))
            {
                await pm.InitializeAsync();

                ConnectionFactory cf = CreateConnectionFactory();
                cf.Port = pm.ProxyPort;
                cf.RequestedHeartbeat = _heartbeatTimeout;
                cf.AutomaticRecoveryEnabled = false;

                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                Task pubTask = Task.Run(async () =>
                {
                    using (IConnection conn = await cf.CreateConnectionAsync())
                    {
                        using (IChannel ch = await conn.CreateChannelAsync())
                        {
                            await ch.ConfirmSelectAsync();
                            QueueDeclareOk q = await ch.QueueDeclareAsync();
                            while (conn.IsOpen)
                            {
                                await ch.BasicPublishAsync("", q.QueueName, GetRandomBody());
                                await ch.WaitForConfirmsAsync();
                                await Task.Delay(TimeSpan.FromSeconds(1));
                                tcs.TrySetResult(true);
                            }

                            await ch.CloseAsync();
                            await conn.CloseAsync();
                        }
                    }
                });

                Assert.True(await tcs.Task);

                var timeoutToxic = new TimeoutToxic();
                timeoutToxic.Attributes.Timeout = 0;
                timeoutToxic.Toxicity = 1.0;

                Task<TimeoutToxic> addToxicTask = pm.AddToxicAsync(timeoutToxic);

                await Assert.ThrowsAsync<AlreadyClosedException>(() =>
                {
                    return Task.WhenAll(addToxicTask, pubTask);
                });
            }
        }

        [SkippableFact]
        [Trait("Category", "Toxiproxy")]
        public async Task TestTcpReset_GH1464()
        {
            Skip.IfNot(AreToxiproxyTestsEnabled, "RABBITMQ_TOXIPROXY_TESTS is not set, skipping test");

            using (var pm = new ToxiproxyManager(_testDisplayName, IsRunningInCI, IsWindows))
            {
                await pm.InitializeAsync();

                ConnectionFactory cf = CreateConnectionFactory();
                cf.Endpoint = new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), pm.ProxyPort);
                cf.RequestedHeartbeat = TimeSpan.FromSeconds(5);
                cf.AutomaticRecoveryEnabled = true;

                var channelCreatedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var connectionShutdownTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                Task recoveryTask = Task.Run(async () =>
                {
                    using (IConnection conn = await cf.CreateConnectionAsync())
                    {
                        conn.ConnectionShutdown += (o, ea) =>
                        {
                            connectionShutdownTcs.SetResult(true);
                        };

                        using (IChannel ch = await conn.CreateChannelAsync())
                        {
                            channelCreatedTcs.SetResult(true);
                            await WaitForRecoveryAsync(conn);
                            await ch.CloseAsync();
                        }

                        await conn.CloseAsync();
                    }
                });

                Assert.True(await channelCreatedTcs.Task);

                const string toxicName = "rmq-localhost-reset_peer";
                var resetPeerToxic = new ResetPeerToxic();
                resetPeerToxic.Name = toxicName;
                resetPeerToxic.Attributes.Timeout = 500;
                resetPeerToxic.Toxicity = 1.0;

                Task<ResetPeerToxic> addToxicTask = pm.AddToxicAsync(resetPeerToxic);

                await Task.WhenAll(addToxicTask, connectionShutdownTcs.Task);

                await pm.RemoveToxicAsync(toxicName);

                await recoveryTask;
            }
        }

        private bool AreToxiproxyTestsEnabled
        {
            get
            {
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
            }
        }
    }
}
