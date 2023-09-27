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

#pragma warning disable 2002

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using RabbitMQ.Client.Framing.Impl;
using Xunit;
using Xunit.Abstractions;

namespace RabbitMQ.Client.Unit
{
    [Collection("IntegrationFixture")]
    public class IntegrationFixture : IDisposable
    {
        internal IConnectionFactory _connFactory;
        internal IConnection _conn;
        internal IChannel _channel;
        internal Encoding _encoding = new UTF8Encoding();

        public static TimeSpan RECOVERY_INTERVAL = TimeSpan.FromSeconds(2);
        protected readonly TimeSpan _waitSpan;
        protected readonly ITestOutputHelper _output;
        protected readonly string _testDisplayName;

        public IntegrationFixture(ITestOutputHelper output)
        {
            _output = output;
            var type = _output.GetType();
            var testMember = type.GetField("test", BindingFlags.Instance | BindingFlags.NonPublic);
            var test = (ITest)testMember.GetValue(output);
            _testDisplayName = test.DisplayName;

            SetUp();

            if (IsRunningInCI())
            {
                _waitSpan = TimeSpan.FromSeconds(30);
            }
            else
            {
                _waitSpan = TimeSpan.FromSeconds(10);
            }
        }

        protected virtual void SetUp()
        {
            _connFactory = new ConnectionFactory();
            _conn = _connFactory.CreateConnection();
            _channel = _conn.CreateChannel();
        }

        public virtual void Dispose()
        {
            if (_channel.IsOpen)
            {
                _channel.Close();
            }

            if (_conn.IsOpen)
            {
                _conn.Close();
            }

            ReleaseResources();
        }

        protected virtual void ReleaseResources()
        {
            // no-op
        }

        //
        // Connections
        //

        internal AutorecoveringConnection CreateAutorecoveringConnection()
        {
            return CreateAutorecoveringConnection(RECOVERY_INTERVAL);
        }

        internal AutorecoveringConnection CreateAutorecoveringConnection(ICredentialsProvider credentialsProvider)
        {
            return CreateAutorecoveringConnection(RECOVERY_INTERVAL, credentialsProvider);
        }

        internal AutorecoveringConnection CreateAutorecoveringConnection(IList<string> hostnames)
        {
            return CreateAutorecoveringConnection(RECOVERY_INTERVAL, hostnames);
        }

        internal AutorecoveringConnection CreateAutorecoveringConnection(TimeSpan interval, ICredentialsProvider credentialsProvider = null)
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                CredentialsProvider = credentialsProvider,
                NetworkRecoveryInterval = interval
            };
            return (AutorecoveringConnection)cf.CreateConnection($"{_testDisplayName}:{Guid.NewGuid()}");
        }

        internal AutorecoveringConnection CreateAutorecoveringConnection(TimeSpan interval, IList<string> hostnames)
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                // tests that use this helper will likely list unreachable hosts,
                // make sure we time out quickly on those
                RequestedConnectionTimeout = TimeSpan.FromSeconds(1),
                NetworkRecoveryInterval = interval
            };
            return (AutorecoveringConnection)cf.CreateConnection(hostnames, $"{_testDisplayName}:{Guid.NewGuid()}");
        }

        internal AutorecoveringConnection CreateAutorecoveringConnection(IList<AmqpTcpEndpoint> endpoints)
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                // tests that use this helper will likely list unreachable hosts,
                // make sure we time out quickly on those
                RequestedConnectionTimeout = TimeSpan.FromSeconds(1),
                NetworkRecoveryInterval = RECOVERY_INTERVAL
            };
            return (AutorecoveringConnection)cf.CreateConnection(endpoints, $"{_testDisplayName}:{Guid.NewGuid()}");
        }

        internal AutorecoveringConnection CreateAutorecoveringConnectionWithTopologyRecoveryDisabled()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = false,
                NetworkRecoveryInterval = RECOVERY_INTERVAL
            };
            return (AutorecoveringConnection)cf.CreateConnection($"{_testDisplayName}:{Guid.NewGuid()}");
        }

        internal AutorecoveringConnection CreateAutorecoveringConnectionWithTopologyRecoveryFilter(TopologyRecoveryFilter filter)
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                TopologyRecoveryFilter = filter
            };

            return (AutorecoveringConnection)cf.CreateConnection($"{_testDisplayName}:{Guid.NewGuid()}");
        }

        internal AutorecoveringConnection CreateAutorecoveringConnectionWithTopologyRecoveryExceptionHandler(TopologyRecoveryExceptionHandler handler)
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                TopologyRecoveryExceptionHandler = handler
            };

            return (AutorecoveringConnection)cf.CreateConnection($"{_testDisplayName}:{Guid.NewGuid()}");
        }

        internal IConnection CreateConnectionWithContinuationTimeout(bool automaticRecoveryEnabled, TimeSpan continuationTimeout)
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = automaticRecoveryEnabled,
                ContinuationTimeout = continuationTimeout
            };
            return cf.CreateConnection($"{_testDisplayName}:{Guid.NewGuid()}");
        }

        //
        // Channels
        //

        internal void WithTemporaryChannel(Action<IChannel> action)
        {
            IChannel channel = _conn.CreateChannel();

            try
            {
                action(channel);
            }
            finally
            {
                channel.Abort();
            }
        }

        internal void WithClosedChannel(Action<IChannel> action)
        {
            IChannel channel = _conn.CreateChannel();
            channel.Close();

            action(channel);
        }

        internal bool WaitForConfirms(IChannel m)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            return m.WaitForConfirmsAsync(cts.Token).GetAwaiter().GetResult();
        }

        //
        // Exchanges
        //

        internal string GenerateExchangeName()
        {
            return $"exchange{Guid.NewGuid()}";
        }

        internal byte[] RandomMessageBody()
        {
            return _encoding.GetBytes(Guid.NewGuid().ToString());
        }

        internal string DeclareNonDurableExchange(IChannel m, string x)
        {
            m.ExchangeDeclare(x, "fanout", false);
            return x;
        }

        internal string DeclareNonDurableExchangeNoWait(IChannel m, string x)
        {
            m.ExchangeDeclareNoWait(x, "fanout", false, false, null);
            return x;
        }

        //
        // Queues
        //

        internal string GenerateQueueName()
        {
            return $"queue{Guid.NewGuid()}";
        }

        internal void WithTemporaryNonExclusiveQueue(Action<IChannel, string> action)
        {
            WithTemporaryNonExclusiveQueue(_channel, action);
        }

        internal void WithTemporaryNonExclusiveQueue(IChannel channel, Action<IChannel, string> action)
        {
            WithTemporaryNonExclusiveQueue(channel, action, GenerateQueueName());
        }

        internal void WithTemporaryNonExclusiveQueue(IChannel channel, Action<IChannel, string> action, string queue)
        {
            try
            {
                channel.QueueDeclare(queue, false, false, false, null);
                action(channel, queue);
            }
            finally
            {
                WithTemporaryChannel(tm => tm.QueueDelete(queue));
            }
        }

        internal void WithTemporaryQueueNoWait(IChannel channel, Action<IChannel, string> action, string queue)
        {
            try
            {
                channel.QueueDeclareNoWait(queue, false, true, false, null);
                action(channel, queue);
            }
            finally
            {
                WithTemporaryChannel(x => x.QueueDelete(queue));
            }
        }

        internal void EnsureNotEmpty(string q, string body)
        {
            WithTemporaryChannel(x => x.BasicPublish("", q, _encoding.GetBytes(body)));
        }

        internal void WithNonEmptyQueue(Action<IChannel, string> action)
        {
            WithNonEmptyQueue(action, "msg");
        }

        internal void WithNonEmptyQueue(Action<IChannel, string> action, string msg)
        {
            WithTemporaryNonExclusiveQueue((m, q) =>
            {
                EnsureNotEmpty(q, msg);
                action(m, q);
            });
        }

        internal void WithEmptyQueue(Action<IChannel, string> action)
        {
            WithTemporaryNonExclusiveQueue((channel, queue) =>
            {
                channel.QueuePurge(queue);
                action(channel, queue);
            });
        }

        internal void AssertMessageCount(string q, uint count)
        {
            WithTemporaryChannel((m) =>
            {
                QueueDeclareOk ok = m.QueueDeclarePassive(q);
                Assert.Equal(count, ok.MessageCount);
            });
        }

        internal void AssertConsumerCount(string q, int count)
        {
            WithTemporaryChannel((m) =>
            {
                QueueDeclareOk ok = m.QueueDeclarePassive(q);
                Assert.Equal((uint)count, ok.ConsumerCount);
            });
        }

        internal void AssertConsumerCount(IChannel m, string q, uint count)
        {
            QueueDeclareOk ok = m.QueueDeclarePassive(q);
            Assert.Equal(count, ok.ConsumerCount);
        }

        //
        // Shutdown
        //

        internal void AssertShutdownError(ShutdownEventArgs args, int code)
        {
            Assert.Equal(args.ReplyCode, code);
        }

        internal void AssertPreconditionFailed(ShutdownEventArgs args)
        {
            AssertShutdownError(args, Constants.PreconditionFailed);
        }

        internal bool InitiatedByPeerOrLibrary(ShutdownEventArgs evt)
        {
            return !(evt.Initiator == ShutdownInitiator.Application);
        }

        //
        // Concurrency
        //

        internal void WaitOn(object o)
        {
            lock (o)
            {
                Monitor.Wait(o, TimingFixture.TestTimeout);
            }
        }

        //
        // Flow Control
        //

        internal void Block()
        {
            RabbitMQCtl.Block(_conn, _encoding);
        }

        internal void Unblock()
        {
            RabbitMQCtl.Unblock();
        }

        //
        // Connection Closure
        //

        internal void CloseConnection(IConnection conn)
        {
            RabbitMQCtl.CloseConnection(conn);
        }

        internal void CloseAllConnections()
        {
            RabbitMQCtl.CloseAllConnections();
        }

        internal void RestartRabbitMQ()
        {
            RabbitMQCtl.RestartRabbitMQ();
        }

        internal void StopRabbitMQ()
        {
            RabbitMQCtl.StopRabbitMQ();
        }

        internal void StartRabbitMQ()
        {
            RabbitMQCtl.StartRabbitMQ();
            RabbitMQCtl.AwaitRabbitMQ();
        }

        //
        // Concurrency and Coordination
        //

        internal void Wait(ManualResetEventSlim latch)
        {
            Assert.True(latch.Wait(_waitSpan), "waiting on a latch timed out");
        }

        internal void Wait(ManualResetEventSlim latch, TimeSpan timeSpan)
        {
            Assert.True(latch.Wait(timeSpan), "waiting on a latch timed out");
        }

        //
        // TLS
        //

        public static string CertificatesDirectory()
        {
            return Environment.GetEnvironmentVariable("SSL_CERTS_DIR");
        }

        private static bool IsRunningInCI()
        {
            if (bool.TryParse(Environment.GetEnvironmentVariable("CI"), out bool ci))
            {
                if (ci == true)
                {
                    return true;
                }
            }

            string concourse = Environment.GetEnvironmentVariable("CONCOURSE_CI_BUILD");
            string gha = Environment.GetEnvironmentVariable("GITHUB_ACTIONS");
            if (String.IsNullOrWhiteSpace(concourse) && String.IsNullOrWhiteSpace(gha))
            {
                return false;
            }

            return true;
        }
    }

    public sealed class IgnoreOnVersionsEarlierThan : FactAttribute
    {
        public IgnoreOnVersionsEarlierThan(int major, int minor)
        {
            if (!CheckMiniumVersion(new Version(major, minor)))
            {
                Skip = $"Skipped test. It requires RabbitMQ +{major}.{minor}";
            }
        }

        private bool CheckMiniumVersion(Version miniumVersion)
        {
            using (var _conn = new ConnectionFactory().CreateConnection())
            {
                System.Collections.Generic.IDictionary<string, object> properties = _conn.ServerProperties;

                if (properties.TryGetValue("version", out object versionVal))
                {
                    string versionStr = Encoding.UTF8.GetString((byte[])versionVal);

                    int dashIdx = Math.Max(versionStr.IndexOf('-'), versionStr.IndexOf('+'));
                    if (dashIdx > 0)
                    {
                        versionStr = versionStr.Remove(dashIdx);
                    }

                    if (Version.TryParse(versionStr, out Version version))
                    {
                        return version >= miniumVersion;
                    }
                }

                return false;
            }
        }
    }

    public class TimingFixture
    {
        public static readonly TimeSpan TimingInterval = TimeSpan.FromMilliseconds(300);
        public static readonly TimeSpan TimingInterval_2X = TimeSpan.FromMilliseconds(600);
        public static readonly TimeSpan TimingInterval_4X = TimeSpan.FromMilliseconds(1200);
        public static readonly TimeSpan TimingInterval_8X = TimeSpan.FromMilliseconds(2400);
        public static readonly TimeSpan TimingInterval_16X = TimeSpan.FromMilliseconds(4800);
        public static readonly TimeSpan SafetyMargin = TimeSpan.FromMilliseconds(150);
        public static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    }
}
