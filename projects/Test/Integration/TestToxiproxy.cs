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
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Toxiproxy.Net;
using Toxiproxy.Net.Toxics;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class ProxyManager : IDisposable
    {
        private static int s_proxyPort = 55669;

        private readonly string _proxyName;
        private readonly int _proxyPort;
        private readonly Connection _proxyConnection;
        private readonly Client _proxyClient;
        private readonly Proxy _proxy;
        private bool _disposedValue;

        public ProxyManager(string testName, bool isRunningInCI, bool isWindows)
        {
            _proxyPort = Interlocked.Increment(ref s_proxyPort);
            _proxyConnection = new Connection(resetAllToxicsAndProxiesOnClose: true);
            _proxyClient = _proxyConnection.Client();

            _proxyName = $"rabbitmq-localhost-{testName}-{_proxyPort}";

            // to start, assume everything is on localhost
            _proxy = new Proxy
            {
                Name = _proxyName,
                Enabled = true,
                Listen = $"{IPAddress.Loopback}:{_proxyPort}",
                Upstream = $"{IPAddress.Loopback}:5672",
            };

            if (isRunningInCI)
            {
                _proxy.Listen = $"0.0.0.0:{_proxyPort}";

                // GitHub Actions
                if (false == isWindows)
                {
                    /*
                     * Note: See the following setup script:
                     * .ci/ubuntu/gha-setup.sh
                     */
                    _proxy.Upstream = "rabbitmq-dotnet-client-rabbitmq:5672";
                }
            }

            Proxy p = _proxyClient.AddAsync(_proxy).GetAwaiter().GetResult();
            Assert.True(p.Enabled);
        }

        public int ProxyPort => _proxyPort;

        public Task<T> AddToxicAsync<T>(T toxic) where T : ToxicBase
        {
            return _proxy.AddAsync(toxic);
        }

        public Task RemoveToxicAsync<T>(T toxic) where T : ToxicBase
        {
            return _proxy.RemoveToxicAsync(toxic.Name);
        }

        public Task DisableAsync()
        {
            _proxy.Enabled = false;
            return _proxyClient.UpdateAsync(_proxy);
        }

        public Task EnableAsync()
        {
            _proxy.Enabled = true;
            return _proxyClient.UpdateAsync(_proxy);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _proxyClient.DeleteAsync(_proxy).Wait();
                    _proxyConnection.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

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

            using (var proxyManager = new ProxyManager(_testDisplayName, IsRunningInCI, IsWindows))
            {
                ConnectionFactory cf = CreateConnectionFactory();
                cf.Port = proxyManager.ProxyPort;
                cf.RequestedHeartbeat = _heartbeatTimeout;
                cf.AutomaticRecoveryEnabled = true;

                var messagePublishedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var connectionShutdownTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var recoverySucceededTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var testSucceededTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                Task pubTask = Task.Run(async () =>
                {
                    using (IConnection conn = await cf.CreateConnectionAsync())
                    {
                        conn.ConnectionShutdown += (s, ea) => connectionShutdownTcs.SetResult(true);
                        conn.RecoverySucceeded += (s, ea) => recoverySucceededTcs.SetResult(true);

                        async Task PublishLoop()
                        {
                            using (IChannel ch = await conn.CreateChannelAsync())
                            {
                                await ch.ConfirmSelectAsync();
                                RabbitMQ.Client.QueueDeclareOk q = await ch.QueueDeclareAsync();
                                while (conn.IsOpen)
                                {
                                    await ch.BasicPublishAsync("", q.QueueName, GetRandomBody());
                                    await ch.WaitForConfirmsAsync();
                                    await Task.Delay(TimeSpan.FromSeconds(1));
                                    messagePublishedTcs.TrySetResult(true);
                                }

                                await ch.CloseAsync();
                            }
                        }

                        await PublishLoop();
                        Assert.True(await testSucceededTcs.Task);
                        await conn.CloseAsync();
                    }
                });

                Assert.True(await messagePublishedTcs.Task);

                Task disableProxyTask = proxyManager.DisableAsync();

                await Task.WhenAll(disableProxyTask, connectionShutdownTcs.Task);

                Task enableProxyTask = proxyManager.EnableAsync();

                await Task.WhenAll(enableProxyTask, recoverySucceededTcs.Task);
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

            using (var proxyManager = new ProxyManager(_testDisplayName, IsRunningInCI, IsWindows))
            {
                ConnectionFactory cf = CreateConnectionFactory();
                cf.Port = proxyManager.ProxyPort;
                cf.RequestedHeartbeat = _heartbeatTimeout;
                cf.AutomaticRecoveryEnabled = false;

                var canTimeoutConnectionTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

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
                                canTimeoutConnectionTcs.TrySetResult(true);
                            }

                            await ch.CloseAsync();
                        }

                        await conn.CloseAsync();
                    }
                });

                Assert.True(await canTimeoutConnectionTcs.Task);

                var timeoutToxic = new TimeoutToxic();
                timeoutToxic.Attributes.Timeout = 0;
                timeoutToxic.Toxicity = 1.0;

                Task addToxicTask = proxyManager.AddToxicAsync(timeoutToxic);

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

            using (var proxyManager = new ProxyManager(_testDisplayName, IsRunningInCI, IsWindows))
            {
                ConnectionFactory cf = CreateConnectionFactory();
                cf.Endpoint = new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), proxyManager.ProxyPort);
                cf.Port = proxyManager.ProxyPort;
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

                Task addToxicTask = proxyManager.AddToxicAsync(resetPeerToxic);

                await Task.WhenAll(addToxicTask, connectionShutdownTcs.Task);

                await proxyManager.RemoveToxicAsync(resetPeerToxic);

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
