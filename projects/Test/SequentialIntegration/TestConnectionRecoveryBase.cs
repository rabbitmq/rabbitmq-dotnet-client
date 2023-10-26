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
using System.Threading;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing.Impl;
using Xunit;
using Xunit.Abstractions;

namespace Test.SequentialIntegration
{
    public class TestConnectionRecoveryBase : SequentialIntegrationFixture
    {
        protected readonly byte[] _messageBody;
        protected const ushort _totalMessageCount = 8192;
        protected const ushort _closeAtCount = 16;

        public TestConnectionRecoveryBase(ITestOutputHelper output) : base(output)
        {
            _messageBody = GetRandomBody(4096);
        }

        protected override void TearDown()
        {
            Unblock();
        }

        protected void AssertConsumerCount(string q, int count)
        {
            WithTemporaryChannel((m) =>
            {
                RabbitMQ.Client.QueueDeclareOk ok = m.QueueDeclarePassive(q);
                Assert.Equal((uint)count, ok.ConsumerCount);
            });
        }

        protected void AssertConsumerCount(IChannel m, string q, uint count)
        {
            RabbitMQ.Client.QueueDeclareOk ok = m.QueueDeclarePassive(q);
            Assert.Equal(count, ok.ConsumerCount);
        }

        protected void AssertExchangeRecovery(IChannel m, string x)
        {
            m.ConfirmSelect();
            WithTemporaryNonExclusiveQueue(m, (_, q) =>
            {
                string rk = "routing-key";
                m.QueueBind(q, x, rk);
                m.BasicPublish(x, rk, _messageBody);

                Assert.True(WaitForConfirms(m));
                m.ExchangeDeclarePassive(x);
            });
        }

        protected void AssertQueueRecovery(IChannel m, string q)
        {
            AssertQueueRecovery(m, q, true);
        }

        protected void AssertQueueRecovery(IChannel m, string q, bool exclusive, IDictionary<string, object> arguments = null)
        {
            m.ConfirmSelect();
            m.QueueDeclarePassive(q);
            RabbitMQ.Client.QueueDeclareOk ok1 = m.QueueDeclare(q, false, exclusive, false, arguments);
            Assert.Equal(0u, ok1.MessageCount);
            m.BasicPublish("", q, _messageBody);
            Assert.True(WaitForConfirms(m));
            RabbitMQ.Client.QueueDeclareOk ok2 = m.QueueDeclare(q, false, exclusive, false, arguments);
            Assert.Equal(1u, ok2.MessageCount);
        }

        internal void AssertRecordedExchanges(AutorecoveringConnection c, int n)
        {
            Assert.Equal(n, c.RecordedExchangesCount);
        }

        internal void AssertRecordedQueues(AutorecoveringConnection c, int n)
        {
            Assert.Equal(n, c.RecordedQueuesCount);
        }

        internal AutorecoveringConnection CreateAutorecoveringConnection()
        {
            return CreateAutorecoveringConnection(RecoveryInterval);
        }

        internal AutorecoveringConnection CreateAutorecoveringConnection(TimeSpan networkRecoveryInterval)
        {
            var cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.NetworkRecoveryInterval = networkRecoveryInterval;
            return (AutorecoveringConnection)cf.CreateConnection();
        }

        internal AutorecoveringConnection CreateAutorecoveringConnection(IList<AmqpTcpEndpoint> endpoints)
        {
            var cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            // tests that use this helper will likely list unreachable hosts,
            // make sure we time out quickly on those
            cf.RequestedConnectionTimeout = TimeSpan.FromSeconds(1);
            cf.NetworkRecoveryInterval = RecoveryInterval;
            return (AutorecoveringConnection)cf.CreateConnection(endpoints);
        }

        internal AutorecoveringConnection CreateAutorecoveringConnectionWithTopologyRecoveryDisabled()
        {
            var cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.TopologyRecoveryEnabled = false;
            cf.NetworkRecoveryInterval = RecoveryInterval;
            return (AutorecoveringConnection)cf.CreateConnection();
        }

        internal AutorecoveringConnection CreateAutorecoveringConnectionWithTopologyRecoveryFilter(TopologyRecoveryFilter filter)
        {
            var cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.TopologyRecoveryEnabled = true;
            cf.TopologyRecoveryFilter = filter;
            return (AutorecoveringConnection)cf.CreateConnection();
        }

        internal AutorecoveringConnection CreateAutorecoveringConnectionWithTopologyRecoveryExceptionHandler(TopologyRecoveryExceptionHandler handler)
        {
            var cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.TopologyRecoveryEnabled = true;
            cf.TopologyRecoveryExceptionHandler = handler;
            return (AutorecoveringConnection)cf.CreateConnection();
        }

        protected void CloseConnection(IConnection conn)
        {
            _rabbitMQCtl.CloseConnection(conn);
        }

        protected void CloseAndWaitForRecovery()
        {
            CloseAndWaitForRecovery((AutorecoveringConnection)_conn);
        }

        internal void CloseAndWaitForRecovery(AutorecoveringConnection conn)
        {
            ManualResetEventSlim sl = PrepareForShutdown(conn);
            ManualResetEventSlim rl = PrepareForRecovery(conn);
            CloseConnection(conn);
            Wait(sl, "connection shutdown");
            Wait(rl, "connection recovery");
        }

        internal void CloseAndWaitForShutdown(AutorecoveringConnection conn)
        {
            ManualResetEventSlim sl = PrepareForShutdown(conn);
            CloseConnection(conn);
            Wait(sl, "connection shutdown");
        }

        protected string DeclareNonDurableExchange(IChannel m, string x)
        {
            m.ExchangeDeclare(x, "fanout", false);
            return x;
        }

        protected string DeclareNonDurableExchangeNoWait(IChannel m, string x)
        {
            m.ExchangeDeclareNoWait(x, "fanout", false, false, null);
            return x;
        }

        protected ManualResetEventSlim PrepareForRecovery(IConnection conn)
        {
            var latch = new ManualResetEventSlim(false);

            AutorecoveringConnection aconn = conn as AutorecoveringConnection;
            aconn.RecoverySucceeded += (source, ea) => latch.Set();

            return latch;
        }

        protected void PublishMessagesWhileClosingConn(string queueName)
        {
            using (AutorecoveringConnection publishingConn = CreateAutorecoveringConnection())
            {
                using (IChannel publishingChannel = publishingConn.CreateChannel())
                {
                    for (ushort i = 0; i < _totalMessageCount; i++)
                    {
                        if (i == _closeAtCount)
                        {
                            CloseConnection(_conn);
                        }
                        publishingChannel.BasicPublish(string.Empty, queueName, _messageBody);
                    }
                }
            }
        }

        protected static ManualResetEventSlim PrepareForShutdown(IConnection conn)
        {
            var latch = new ManualResetEventSlim(false);

            AutorecoveringConnection aconn = conn as AutorecoveringConnection;
            aconn.ConnectionShutdown += (c, args) => latch.Set();

            return latch;
        }

        protected void RestartServerAndWaitForRecovery()
        {
            RestartServerAndWaitForRecovery((AutorecoveringConnection)_conn);
        }

        internal void RestartServerAndWaitForRecovery(AutorecoveringConnection conn)
        {
            ManualResetEventSlim sl = PrepareForShutdown(conn);
            ManualResetEventSlim rl = PrepareForRecovery(conn);
            RestartRabbitMQ();
            Wait(sl, "connection shutdown");
            Wait(rl, "connection recovery");
        }

        protected bool WaitForConfirms(IChannel m)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            return m.WaitForConfirmsAsync(cts.Token).GetAwaiter().GetResult();
        }

        protected void WaitForRecovery()
        {
            Wait(PrepareForRecovery((AutorecoveringConnection)_conn), "recovery succeded");
        }

        internal void WaitForRecovery(AutorecoveringConnection conn)
        {
            Wait(PrepareForRecovery(conn), "recovery succeeded");
        }

        protected void WaitForShutdown()
        {
            Wait(PrepareForShutdown(_conn), "connection shutdown");
        }

        protected void WaitForShutdown(IConnection conn)
        {
            Wait(PrepareForShutdown(conn), "connection shutdown");
        }

        protected void WithTemporaryQueueNoWait(IChannel channel, Action<IChannel, string> action, string queue)
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

        public class AckingBasicConsumer : TestBasicConsumer
        {
            public AckingBasicConsumer(IChannel channel, ushort totalMessageCount, ManualResetEventSlim allMessagesSeenLatch)
                : base(channel, totalMessageCount, allMessagesSeenLatch)
            {
            }

            public override void PostHandleDelivery(ulong deliveryTag)
            {
                Channel.BasicAck(deliveryTag, false);
            }
        }

        public class NackingBasicConsumer : TestBasicConsumer
        {
            public NackingBasicConsumer(IChannel channel, ushort totalMessageCount, ManualResetEventSlim allMessagesSeenLatch)
                : base(channel, totalMessageCount, allMessagesSeenLatch)
            {
            }

            public override void PostHandleDelivery(ulong deliveryTag)
            {
                Channel.BasicNack(deliveryTag, false, false);
            }
        }

        public class RejectingBasicConsumer : TestBasicConsumer
        {
            public RejectingBasicConsumer(IChannel channel, ushort totalMessageCount, ManualResetEventSlim allMessagesSeenLatch)
                : base(channel, totalMessageCount, allMessagesSeenLatch)
            {
            }

            public override void PostHandleDelivery(ulong deliveryTag)
            {
                Channel.BasicReject(deliveryTag, false);
            }
        }

        public class TestBasicConsumer : DefaultBasicConsumer
        {
            protected readonly ManualResetEventSlim _allMessagesSeenLatch;
            protected readonly ushort _totalMessageCount;
            protected ushort _counter = 0;

            public TestBasicConsumer(IChannel channel, ushort totalMessageCount, ManualResetEventSlim allMessagesSeenLatch)
                : base(channel)
            {
                _totalMessageCount = totalMessageCount;
                _allMessagesSeenLatch = allMessagesSeenLatch;
            }

            public override void HandleBasicDeliver(string consumerTag,
                ulong deliveryTag,
                bool redelivered,
                string exchange,
                string routingKey,
                in ReadOnlyBasicProperties properties,
                ReadOnlyMemory<byte> body)
            {
                try
                {
                    PostHandleDelivery(deliveryTag);
                }
                finally
                {
                    ++_counter;
                    if (_counter >= _totalMessageCount)
                    {
                        _allMessagesSeenLatch.Set();
                    }
                }
            }

            public virtual void PostHandleDelivery(ulong deliveryTag)
            {
            }
        }

        protected bool SendAndConsumeMessage(IConnection conn, string queue, string exchange, string routingKey)
        {
            using (IChannel ch = conn.CreateChannel())
            {
                var latch = new ManualResetEventSlim(false);

                var consumer = new AckingBasicConsumer(ch, 1, latch);

                ch.BasicConsume(queue, false, consumer);

                ch.BasicPublish(exchange, routingKey, _encoding.GetBytes("test message"));

                return latch.Wait(TimeSpan.FromSeconds(5));
            }
        }
    }
}
