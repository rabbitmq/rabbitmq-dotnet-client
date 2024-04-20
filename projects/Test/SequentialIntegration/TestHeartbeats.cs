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
using System.Threading.Tasks;
using RabbitMQ.Client;
using Xunit;
using Xunit.Abstractions;

namespace Test.SequentialIntegration
{
    public class TestHeartbeats : SequentialIntegrationFixture
    {
        private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromSeconds(2);

        public TestHeartbeats(ITestOutputHelper output) : base(output)
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

        [SkippableFact(Timeout = 35000)]
        [Trait("Category", "LongRunning")]
        public async Task TestThatHeartbeatWriterUsesConfigurableInterval()
        {
            Skip.IfNot(LongRunningTestsEnabled(), "RABBITMQ_LONG_RUNNING_TESTS is not set, skipping test");

            ConnectionFactory cf = CreateConnectionFactory();
            cf.RequestedHeartbeat = _heartbeatTimeout;
            cf.AutomaticRecoveryEnabled = false;

            await RunSingleConnectionTestAsync(cf);
        }

        [SkippableFact]
        [Trait("Category", "LongRunning")]
        public async Task TestThatHeartbeatWriterWithTLSEnabled()
        {
            Skip.IfNot(LongRunningTestsEnabled(), "RABBITMQ_LONG_RUNNING_TESTS is not set, skipping test");

            var sslEnv = new SslEnv();
            Skip.IfNot(sslEnv.IsSslConfigured, "SSL_CERTS_DIR and/or PASSWORD are not configured, skipping test");

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Port = 5671;
            cf.RequestedHeartbeat = _heartbeatTimeout;
            cf.AutomaticRecoveryEnabled = false;

            cf.Ssl.ServerName = sslEnv.Hostname;
            cf.Ssl.CertPath = sslEnv.CertPath;
            cf.Ssl.CertPassphrase = sslEnv.CertPassphrase;
            cf.Ssl.Enabled = true;

            await RunSingleConnectionTestAsync(cf);
        }

        [SkippableFact(Timeout = 90000)]
        [Trait("Category", "LongRunning")]
        public async Task TestHundredsOfConnectionsWithRandomHeartbeatInterval()
        {
            Skip.IfNot(LongRunningTestsEnabled(), "RABBITMQ_LONG_RUNNING_TESTS is not set, skipping test");

            const ushort connectionCount = 200;

            var rnd = new Random();
            var conns = new List<IConnection>();

            try
            {
                for (int i = 0; i < connectionCount; i++)
                {
                    ushort n = Convert.ToUInt16(rnd.Next(2, 6));
                    ConnectionFactory cf = CreateConnectionFactory();
                    cf.RequestedHeartbeat = TimeSpan.FromSeconds(n);
                    cf.AutomaticRecoveryEnabled = false;

                    IConnection conn = await cf.CreateConnectionAsync($"{_testDisplayName}:{i}");
                    conns.Add(conn);
                    IChannel ch = await conn.CreateChannelAsync();
                    conn.ConnectionShutdown += (sender, evt) =>
                        {
                            CheckInitiator(evt);
                        };
                }

                await SleepFor(60);
            }
            finally
            {
                foreach (IConnection conn in conns)
                {
                    await conn.CloseAsync();
                }
            }
        }

        private async Task RunSingleConnectionTestAsync(ConnectionFactory cf)
        {
            using (IConnection conn = await cf.CreateConnectionAsync(_testDisplayName))
            {
                using (IChannel ch = await conn.CreateChannelAsync())
                {
                    bool wasShutdown = false;

                    conn.ConnectionShutdown += (sender, evt) =>
                    {
                        lock (conn)
                        {
                            if (InitiatedByPeerOrLibrary(evt))
                            {
                                CheckInitiator(evt);
                                wasShutdown = true;
                            }
                        }
                    };

                    await SleepFor(30);

                    Assert.False(wasShutdown, "shutdown event should not have been fired");
                    Assert.True(conn.IsOpen, "connection should be open");

                    await ch.CloseAsync();
                }

                await conn.CloseAsync();
            }
        }

        private bool LongRunningTestsEnabled()
        {
            string s = Environment.GetEnvironmentVariable("RABBITMQ_LONG_RUNNING_TESTS");

            if (String.IsNullOrEmpty(s))
            {
                return false;
            }

            if (Boolean.TryParse(s, out bool enabled))
            {
                return enabled;
            }

            return false;
        }

        private Task SleepFor(int t)
        {
            // _output.WriteLine("Testing heartbeats, sleeping for {0} seconds", t);
            return Task.Delay(t * 1000);
        }

        private bool InitiatedByPeerOrLibrary(ShutdownEventArgs evt)
        {
            return !(evt.Initiator == ShutdownInitiator.Application);
        }

        private void CheckInitiator(ShutdownEventArgs evt)
        {
            if (InitiatedByPeerOrLibrary(evt))
            {
                _output.WriteLine(((Exception)evt.Cause).StackTrace);
                string s = string.Format("Shutdown: {0}, initiated by: {1}", evt, evt.Initiator);
                _output.WriteLine(s);
                Assert.Fail(s);
            }
        }
    }
}
