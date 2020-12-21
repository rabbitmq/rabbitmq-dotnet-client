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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RabbitMQ.Client.client.impl.Channel;
using RabbitMQ.Client.Framing.Impl;
using static RabbitMQ.Client.Unit.RabbitMQCtl;

namespace RabbitMQ.Client.Unit
{
    public class IntegrationFixture
    {
        internal ConnectionFactory _connFactory;
        internal IConnection _conn;
        internal IChannel _channel;
        internal Encoding _encoding = new UTF8Encoding();
        public static TimeSpan RECOVERY_INTERVAL = TimeSpan.FromSeconds(2);

        [SetUp]
        public virtual async Task Init()
        {
            _connFactory = new ConnectionFactory();
            _conn = _connFactory.CreateConnection();
            _channel = await _conn.CreateChannelAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task Dispose()
        {
            if(_channel != null && _channel.IsOpen)
            {
                await _channel.CloseAsync().ConfigureAwait(false);
            }
            if(_conn.IsOpen)
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

        internal AutorecoveringConnection CreateAutorecoveringConnection(IList<string> hostnames)
        {
            return CreateAutorecoveringConnection(RECOVERY_INTERVAL, hostnames);
        }

        internal AutorecoveringConnection CreateAutorecoveringConnection(TimeSpan interval)
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = interval
            };
            return (AutorecoveringConnection)cf.CreateConnection($"UNIT_CONN:{Guid.NewGuid()}");
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
            return (AutorecoveringConnection)cf.CreateConnection(hostnames, $"UNIT_CONN:{Guid.NewGuid()}");
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
            return (AutorecoveringConnection)cf.CreateConnection(endpoints, $"UNIT_CONN:{Guid.NewGuid()}");
        }

        internal AutorecoveringConnection CreateAutorecoveringConnectionWithTopologyRecoveryDisabled()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = false,
                NetworkRecoveryInterval = RECOVERY_INTERVAL
            };
            return (AutorecoveringConnection)cf.CreateConnection($"UNIT_CONN:{Guid.NewGuid()}");
        }

        internal IConnection CreateConnectionWithContinuationTimeout(bool automaticRecoveryEnabled, TimeSpan continuationTimeout)
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = automaticRecoveryEnabled,
                ContinuationTimeout = continuationTimeout
            };
            return cf.CreateConnection($"UNIT_CONN:{Guid.NewGuid()}");
        }

        //
        // Channels
        //

        internal async Task WithTemporaryChannelAsync(Func<IChannel, Task> action)
        {
            IChannel channel = await _conn.CreateChannelAsync().ConfigureAwait(false);

            try
            {
                await action(channel).ConfigureAwait(false);
            }
            finally
            {
                await channel.AbortAsync().ConfigureAwait(false);
            }
        }

        internal async Task WithClosedChannelAsync(Action<IChannel> action)
        {
            IChannel channel = await _conn.CreateChannelAsync().ConfigureAwait(false);
            await channel.CloseAsync().ConfigureAwait(false);

            action(channel);
        }

        internal bool WaitForConfirms(IChannel ch)
        {
            return ch.WaitForConfirms(TimeSpan.FromSeconds(4));
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

        //
        // Queues
        //

        internal string GenerateQueueName()
        {
            return $"queue{Guid.NewGuid()}";
        }

        internal Task WithTemporaryNonExclusiveQueueAsync(Func<IChannel, string, Task> action)
        {
            return WithTemporaryNonExclusiveQueueAsync(_channel, action);
        }

        internal Task WithTemporaryNonExclusiveQueueAsync(IChannel channel, Func<IChannel, string, Task> action)
        {
            return WithTemporaryNonExclusiveQueueAsync(channel, action, GenerateQueueName());
        }

        internal async Task WithTemporaryNonExclusiveQueueAsync(IChannel channel, Func<IChannel, string, Task> action, string queue)
        {
            try
            {
                await channel.DeclareQueueAsync(queue, false, false, false).ConfigureAwait(false);
                await action(channel, queue).ConfigureAwait(false);
            } finally
            {
                await WithTemporaryChannelAsync(ch => ch.DeleteQueueAsync(queue).AsTask()).ConfigureAwait(false);
            }
        }

        internal async Task WithTemporaryQueueNoWaitAsync(IChannel channel, Func<IChannel, string, Task> action, string queue)
        {
            try
            {
                await channel.DeclareQueueWithoutConfirmationAsync(queue, false, true, false).ConfigureAwait(false);
                await action(channel, queue).ConfigureAwait(false);
            } finally
            {
                await WithTemporaryChannelAsync(x => x.DeleteQueueAsync(queue).AsTask()).ConfigureAwait(false);
            }
        }

        internal Task EnsureNotEmpty(string q, string body)
        {
            return WithTemporaryChannelAsync(x => x.PublishMessageAsync("", q, null, _encoding.GetBytes(body)).AsTask());
        }

        internal Task WithNonEmptyQueueAsync(Func<IChannel, string, Task> action)
        {
            return WithNonEmptyQueueAsync(action, "msg");
        }

        internal Task WithNonEmptyQueueAsync(Func<IChannel, string, Task> action, string msg)
        {
            return WithTemporaryNonExclusiveQueueAsync((ch, q) =>
            {
                EnsureNotEmpty(q, msg);
                return action(ch, q);
            });
        }

        internal Task WithEmptyQueueAsync(Func<IChannel, string, Task> action)
        {
            return WithTemporaryNonExclusiveQueueAsync(async (channel, queue) =>
            {
                await channel.PurgeQueueAsync(queue).ConfigureAwait(false);
                await action(channel, queue).ConfigureAwait(false);
            });
        }

        internal Task AssertMessageCountAsync(string q, int count)
        {
            return WithTemporaryChannelAsync(async ch => {
                uint messageCount = await ch.GetQueueMessageCountAsync(q).ConfigureAwait(false);
                Assert.AreEqual(count, messageCount);
            });
        }

        internal Task AssertConsumerCountAsync(string q, int count)
        {
            return WithTemporaryChannelAsync(ch => AssertConsumerCountAsync(ch, q, count));
        }

        internal async Task AssertConsumerCountAsync(IChannel ch, string q, int count)
        {
            uint consumerCount = await ch.GetQueueConsumerCountAsync(q).ConfigureAwait(false);
            Assert.AreEqual(count, consumerCount);
        }

        //
        // Shutdown
        //

        internal void AssertShutdownError(ShutdownEventArgs args, int code)
        {
            Assert.AreEqual(args.ReplyCode, code);
        }

        internal void AssertPreconditionFailed(ShutdownEventArgs args)
        {
            AssertShutdownError(args, Constants.PreconditionFailed);
        }

        internal bool InitiatedByPeerOrLibrary(ShutdownEventArgs evt)
        {
            return evt.Initiator != ShutdownInitiator.Application;
        }

        //
        // Flow Control
        //

        internal Task BlockAsync()
        {
            return RabbitMQCtl.BlockAsync(_conn, _encoding);
        }

        internal void Unblock()
        {
            RabbitMQCtl.Unblock();
        }

        //
        // Connection Closure
        //

        internal List<ConnectionInfo> ListConnections()
        {
            return RabbitMQCtl.ListConnections();
        }

        internal void CloseConnection(IConnection conn)
        {
            RabbitMQCtl.CloseConnection(conn);
        }

        internal void CloseAllConnections()
        {
            RabbitMQCtl.CloseAllConnections();
        }

        internal void CloseConnection(string pid)
        {
            RabbitMQCtl.CloseConnection(pid);
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
        }

        //
        // Concurrency and Coordination
        //

        internal void Wait(ManualResetEventSlim latch)
        {
            Assert.IsTrue(latch.Wait(TimeSpan.FromSeconds(10)), "waiting on a latch timed out");
        }

        internal void Wait(ManualResetEventSlim latch, TimeSpan timeSpan)
        {
            Assert.IsTrue(latch.Wait(timeSpan), "waiting on a latch timed out");
        }

        //
        // TLS
        //

        public static string CertificatesDirectory()
        {
            return Environment.GetEnvironmentVariable("SSL_CERTS_DIR");
        }
    }

    public class TimingFixture
    {
        public static readonly TimeSpan TimingInterval = TimeSpan.FromMilliseconds(300);
        public static readonly TimeSpan TimingInterval_2X = TimeSpan.FromMilliseconds(600);
        public static readonly TimeSpan SafetyMargin = TimeSpan.FromMilliseconds(150);
        public static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    }
}
