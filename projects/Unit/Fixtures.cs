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
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        internal IModel _model;
        internal Encoding _encoding = new UTF8Encoding();

        public static TimeSpan RECOVERY_INTERVAL = TimeSpan.FromSeconds(2);

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
        }

        protected virtual void SetUp()
        {
            _connFactory = new ConnectionFactory();
            _conn = _connFactory.CreateConnection();
            _model = _conn.CreateModel();
        }

        public virtual void Dispose()
        {
            if (_model.IsOpen)
            {
                _model.Close();
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

        internal AutorecoveringConnection CreateAutorecoveringConnection(IList<string> hostnames)
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                // tests that use this helper will likely list unreachable hosts,
                // make sure we time out quickly on those
                RequestedConnectionTimeout = TimeSpan.FromSeconds(1),
                NetworkRecoveryInterval = RECOVERY_INTERVAL
            };
            return (AutorecoveringConnection)cf.CreateConnection(hostnames, $"{_testDisplayName}:{Guid.NewGuid()}");
        }

        internal AutorecoveringConnection CreateAutorecoveringConnection(TimeSpan? interval = null)
        {
            interval ??= RECOVERY_INTERVAL;
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = interval.Value
            };
            return (AutorecoveringConnection)cf.CreateConnection($"{_testDisplayName}:{Guid.NewGuid()}");
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

        internal void WithTemporaryModel(Action<IModel> action)
        {
            IModel model = _conn.CreateModel();

            try
            {
                action(model);
            }
            finally
            {
                model.Abort();
            }
        }

        internal void WithClosedModel(Action<IModel> action)
        {
            IModel model = _conn.CreateModel();
            model.Close();

            action(model);
        }

        internal bool WaitForConfirms(IModel m)
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

        internal string DeclareNonDurableExchange(IModel m, [CallerMemberName]string x = null)
        {
            m.ExchangeDeclare(x, "fanout", false);
            return x;
        }

        internal string DeclareNonDurableExchangeNoWait(IModel m, [CallerMemberName]string x = null)
        {
            m.ExchangeDeclareNoWait(x, "fanout", false, false, null);
            return x;
        }

        //
        // Queues
        //

        internal string GenerateQueueName([CallerMemberName] string callerName = null)
        {
            return $"queue{Guid.NewGuid()}{callerName}";
        }

        internal Task WithTemporaryNonExclusiveQueueAsync(Func<IModel, string, Task> action)
        {
            return WithTemporaryNonExclusiveQueueAsync(_model, action);
        }

        internal async Task WithTemporaryNonExclusiveQueueAsync(IModel model, Func<IModel, string, Task> action, [CallerMemberName] string queueName = null)
        {
            try
            {
                model.QueueDeclare(queueName, false, false, false, null);
                await action(model, queueName);
            }
            finally
            {
                WithTemporaryModel(tm => tm.QueueDelete(queueName));
            }
        }

        internal async Task WithTemporaryQueueNoWaitAsync(IModel model, Func<IModel, string, Task> action, string queue)
        {
            try
            {
                model.QueueDeclareNoWait(queue, false, true, false, null);
                await action(model, queue);
            }
            finally
            {
                WithTemporaryModel(x => x.QueueDelete(queue));
            }
        }

        internal void EnsureNotEmpty(string q, string body)
        {
            WithTemporaryModel(x => x.BasicPublish("", q, _encoding.GetBytes(body)));
        }

        internal Task WithNonEmptyQueueAsync(Func<IModel, string, Task> action)
        {
            return WithNonEmptyQueueAsync(action, "msg");
        }

        internal Task WithNonEmptyQueueAsync(Func<IModel, string, Task> action, string msg)
        {
            return WithTemporaryNonExclusiveQueueAsync(async (m, q) =>
            {
                EnsureNotEmpty(q, msg);
                await action(m, q);
            });
        }

        internal Task WithEmptyQueueAsync(Func<IModel, string, Task> action)
        {
            return WithTemporaryNonExclusiveQueueAsync(async (model, queue) =>
            {
                model.QueuePurge(queue);
                await action(model, queue);
            });
        }

        internal void AssertMessageCount(string q, uint count)
        {
            WithTemporaryModel((m) =>
            {
                QueueDeclareOk ok = m.QueueDeclarePassive(q);
                Assert.Equal(count, ok.MessageCount);
            });
        }

        internal void AssertConsumerCount(string q, int count)
        {
            WithTemporaryModel((m) =>
            {
                QueueDeclareOk ok = m.QueueDeclarePassive(q);
                Assert.Equal((uint)count, ok.ConsumerCount);
            });
        }

        internal void AssertConsumerCount(IModel m, string q, uint count)
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
        // Flow Control
        //

        internal Task BlockAsync()
        {
            return RabbitMQCtl.BlockAsync(_conn, _encoding, _output);
        }

        internal void Unblock()
        {
            RabbitMQCtl.Unblock(_output);
        }

        //
        // Connection Closure
        //

        internal void CloseConnection(IConnection conn)
        {
            RabbitMQCtl.CloseConnection(conn, _output);
        }

        internal Task RestartRabbitMQAsync()
        {
            return RabbitMQCtl.RestartRabbitMQAsync(_output);
        }

        internal void StopRabbitMQ()
        {
            RabbitMQCtl.StopRabbitMQ(_output);
        }

        internal void StartRabbitMQ()
        {
            RabbitMQCtl.StartRabbitMQ(_output);
            RabbitMQCtl.AwaitRabbitMQ(_output);
        }

        //
        // Concurrency and Coordination
        //

        internal void Wait(ManualResetEventSlim latch, TimeSpan? timeSpan = null)
        {
            Assert.True(latch.Wait(timeSpan ?? TimeSpan.FromSeconds(30)), "waiting on a latch timed out");
        }

        internal void Wait(CountdownEvent countdownEvent, TimeSpan? timeSpan = null)
        {
            Assert.True(countdownEvent.Wait(timeSpan ?? TimeSpan.FromSeconds(30)), "waiting on a latch timed out");
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
        public static readonly TimeSpan TimingInterval_4X = TimeSpan.FromMilliseconds(1200);
        public static readonly TimeSpan SafetyMargin = TimeSpan.FromMilliseconds(150);
        public static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    }
}
