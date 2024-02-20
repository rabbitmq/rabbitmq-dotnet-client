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
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Toxiproxy.Net;
using Toxiproxy.Net.Toxics;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestToxiproxy : IntegrationFixture
    {
        private const ushort ProxyPort = 55672;
        private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromSeconds(1);
        private readonly Connection _proxyConnection;
        private readonly Client _proxyClient;
        private readonly Proxy _rmqProxy;

        public TestToxiproxy(ITestOutputHelper output) : base(output)
        {
            if (AreToxiproxyTestsEnabled)
            {
                _proxyConnection = new Connection();
                _proxyClient = _proxyConnection.Client();

                // to start, assume everything is on localhost
                _rmqProxy = new Proxy
                {
                    Name = "rmq-localhost",
                    Enabled = true,
                    Listen = $"{IPAddress.Loopback}:{ProxyPort}",
                    Upstream = $"{IPAddress.Loopback}:5672",
                };

                if (IsRunningInCI)
                {
                    _rmqProxy.Listen = $"0.0.0.0:{ProxyPort}";

                    // GitHub Actions
                    if (false == IsWindows)
                    {
                        /*
                         * Note: See the following setup script:
                         * .ci/ubuntu/gha-setup.sh
                         */
                        _rmqProxy.Upstream = "rabbitmq-dotnet-client-rabbitmq:5672";
                    }
                }
            }
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
                return _proxyClient.AddAsync(_rmqProxy);
            }
            else
            {
                return Task.CompletedTask;
            }
        }

        public override Task DisposeAsync()
        {
            if (_proxyClient != null)
            {
                return _proxyClient.DeleteAsync(_rmqProxy);
            }
            else
            {
                return Task.CompletedTask;
            }
        }

        [SkippableFact]
        [Trait("Category", "Toxiproxy")]
        public async Task TestThatStoppedSocketResultsInHeartbeatTimeout()
        {
            Skip.IfNot(AreToxiproxyTestsEnabled, "RABBITMQ_TOXIPROXY_TESTS is not set, skipping test");

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Port = ProxyPort;
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

            await _rmqProxy.AddAsync(timeoutToxic);
            var sw = new Stopwatch();
            sw.Start();
            Task<Proxy> updateProxyTask = _rmqProxy.UpdateAsync();

            await Assert.ThrowsAsync<AlreadyClosedException>(() =>
            {
                return Task.WhenAll(updateProxyTask, pubTask);
            });

            sw.Stop();

            _output.WriteLine($"[INFO] heartbeat timeout took {sw.Elapsed}");
        }

        [SkippableFact]
        [Trait("Category", "Toxiproxy")]
        public async Task TestTcpReset_GH1464()
        {
            Skip.IfNot(AreToxiproxyTestsEnabled, "RABBITMQ_TOXIPROXY_TESTS is not set, skipping test");

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Endpoint = new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), ProxyPort);
            cf.Port = ProxyPort;
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

            var sw = new Stopwatch();
            sw.Start();

            await _rmqProxy.AddAsync(resetPeerToxic);
            Task<Proxy> updateProxyTask = _rmqProxy.UpdateAsync();

            await Task.WhenAll(updateProxyTask, connectionShutdownTcs.Task);

            await _rmqProxy.RemoveToxicAsync(toxicName);

            await recoveryTask;

            sw.Stop();

            _output.WriteLine($"[INFO] reset peer took {sw.Elapsed}");
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
