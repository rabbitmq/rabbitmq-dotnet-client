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
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing;
using Xunit;
using Xunit.Abstractions;

namespace Test
{
    public class TestConnectionRecoveryBase : IntegrationFixture, IDisposable
    {
        private readonly Util _util;
        protected readonly byte[] _messageBody;
        protected const ushort TotalMessageCount = 16384;
        protected const ushort CloseAtCount = 16;

        public TestConnectionRecoveryBase(ITestOutputHelper output)
            : base(output)
        {
            _util = new Util(output);
            _messageBody = GetRandomBody(4096);
        }

        public void Dispose() => _util.Dispose();

        protected Task AssertConsumerCountAsync(string q, int count)
        {
            return WithTemporaryChannelAsync(async ch =>
            {
                RabbitMQ.Client.QueueDeclareOk ok = await ch.QueueDeclarePassiveAsync(q);
                Assert.Equal((uint)count, ok.ConsumerCount);
            });
        }

        protected async Task AssertConsumerCountAsync(IChannel ch, string q, uint count)
        {
            RabbitMQ.Client.QueueDeclareOk ok = await ch.QueueDeclarePassiveAsync(q);
            Assert.Equal(count, ok.ConsumerCount);
        }

        protected async Task AssertExchangeRecoveryAsync(IChannel ch, string x)
        {
            await WithTemporaryNonExclusiveQueueAsync(ch, async (_, q) =>
            {
                string rk = "routing-key";
                await ch.QueueBindAsync(q, x, rk);
                await ch.BasicPublishAsync(x, rk, _messageBody);
                await ch.ExchangeDeclarePassiveAsync(x);
            });
        }

        protected Task AssertExclusiveQueueRecoveryAsync(IChannel m, string q)
        {
            return AssertQueueRecoveryAsync(m, q, true);
        }

        protected async Task AssertQueueRecoveryAsync(IChannel ch, string q, bool exclusive, IDictionary<string, object> arguments = null)
        {
            await ch.QueueDeclarePassiveAsync(q);

            RabbitMQ.Client.QueueDeclareOk ok1 = await ch.QueueDeclareAsync(queue: q, passive: false,
                durable: false, exclusive: exclusive, autoDelete: false, arguments: arguments);
            Assert.Equal(0u, ok1.MessageCount);

            await ch.BasicPublishAsync("", q, _messageBody);

            RabbitMQ.Client.QueueDeclareOk ok2 = await ch.QueueDeclareAsync(queue: q, passive: false,
                durable: false, exclusive: exclusive, autoDelete: false, arguments: arguments);
            Assert.Equal(1u, ok2.MessageCount);
        }

        internal void AssertRecordedExchanges(AutorecoveringConnection c, int n)
        {
            Assert.Equal(n, c.RecordedExchangesCount);
        }

        internal Task<AutorecoveringConnection> CreateAutorecoveringConnectionAsync()
        {
            return CreateAutorecoveringConnectionAsync(RecoveryInterval);
        }

        internal async Task<AutorecoveringConnection> CreateAutorecoveringConnectionAsync(TimeSpan networkRecoveryInterval)
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.NetworkRecoveryInterval = networkRecoveryInterval;
            IConnection conn = await cf.CreateConnectionAsync();
            return (AutorecoveringConnection)conn;
        }

        internal async Task<AutorecoveringConnection> CreateAutorecoveringConnectionAsync(IList<AmqpTcpEndpoint> endpoints)
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            // tests that use this helper will likely list unreachable hosts,
            // make sure we time out quickly on those
            cf.RequestedConnectionTimeout = TimeSpan.FromSeconds(1);
            cf.NetworkRecoveryInterval = RecoveryInterval;
            IConnection conn = await cf.CreateConnectionAsync(endpoints);
            return (AutorecoveringConnection)conn;
        }

        internal async Task<AutorecoveringConnection> CreateAutorecoveringConnectionWithTopologyRecoveryDisabledAsync()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.TopologyRecoveryEnabled = false;
            cf.NetworkRecoveryInterval = RecoveryInterval;
            IConnection conn = await cf.CreateConnectionAsync();
            return (AutorecoveringConnection)conn;
        }

        internal async Task<AutorecoveringConnection> CreateAutorecoveringConnectionWithTopologyRecoveryFilterAsync(TopologyRecoveryFilter filter)
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.TopologyRecoveryEnabled = true;
            cf.TopologyRecoveryFilter = filter;
            IConnection conn = await cf.CreateConnectionAsync();
            return (AutorecoveringConnection)conn;
        }

        internal async Task<AutorecoveringConnection> CreateAutorecoveringConnectionWithTopologyRecoveryExceptionHandlerAsync(TopologyRecoveryExceptionHandler handler)
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.TopologyRecoveryEnabled = true;
            cf.TopologyRecoveryExceptionHandler = handler;
            IConnection conn = await cf.CreateConnectionAsync();
            return (AutorecoveringConnection)conn;
        }

        protected Task CloseConnectionAsync(IConnection conn)
        {
            return _util.CloseConnectionAsync(conn.ClientProvidedName);
        }

        protected Task CloseAndWaitForRecoveryAsync()
        {
            return CloseAndWaitForRecoveryAsync((AutorecoveringConnection)_conn);
        }

        internal async Task CloseAndWaitForRecoveryAsync(AutorecoveringConnection conn)
        {
            TaskCompletionSource<bool> sl = PrepareForShutdown(conn);
            TaskCompletionSource<bool> rl = PrepareForRecovery(conn);
            await CloseConnectionAsync(conn);
            await WaitAsync(sl, "connection shutdown");
            await WaitAsync(rl, "connection recovery");
        }

        internal async Task CloseAndWaitForShutdownAsync(AutorecoveringConnection conn)
        {
            TaskCompletionSource<bool> sl = PrepareForShutdown(conn);
            await CloseConnectionAsync(conn);
            await WaitAsync(sl, "connection shutdown");
        }

        protected static async Task<string> DeclareNonDurableExchangeAsync(IChannel ch, string exchangeName)
        {
            await ch.ExchangeDeclareAsync(exchangeName, "fanout", false);
            return exchangeName;
        }

        protected async Task PublishMessagesWhileClosingConnAsync(string queueName)
        {
            using (AutorecoveringConnection publishingConn = await CreateAutorecoveringConnectionAsync())
            {
                using (IChannel publishingChannel = await publishingConn.CreateChannelAsync(_createChannelOptions))
                {
                    for (ushort i = 0; i < TotalMessageCount; i++)
                    {
                        if (i == CloseAtCount)
                        {
                            await CloseConnectionAsync(_conn);
                        }

                        await publishingChannel.BasicPublishAsync(string.Empty, queueName, _messageBody);
                    }

                    await publishingChannel.CloseAsync();
                }
            }
        }

        protected static TaskCompletionSource<bool> PrepareForShutdown(IConnection conn)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            AutorecoveringConnection aconn = conn as AutorecoveringConnection;
            aconn.ConnectionShutdownAsync += (c, args) =>
            {
                tcs.TrySetResult(true);
                return Task.CompletedTask;
            };

            return tcs;
        }

        protected Task WaitForShutdownAsync()
        {
            TaskCompletionSource<bool> tcs = PrepareForShutdown(_conn);
            return WaitAsync(tcs, "connection shutdown");
        }

        protected static Task WaitForShutdownAsync(IConnection conn)
        {
            TaskCompletionSource<bool> tcs = PrepareForShutdown(conn);
            return WaitAsync(tcs, "connection shutdown");
        }

        protected async Task WithTemporaryExclusiveQueueNoWaitAsync(IChannel channel, Func<IChannel, string, Task> action, string queue)
        {
            try
            {
                await channel.QueueDeclareAsync(queue: queue, durable: false, exclusive: true, autoDelete: false, noWait: true);
                await action(channel, queue);
            }
            finally
            {
                await WithTemporaryChannelAsync((ch) => ch.QueueDeleteAsync(queue));
            }
        }

        public class AckingBasicConsumer : TestBasicConsumer
        {
            public AckingBasicConsumer(IChannel channel, ushort totalMessageCount, TaskCompletionSource<bool> allMessagesSeenLatch)
                : base(channel, totalMessageCount, allMessagesSeenLatch)
            {
            }

            public override Task PostHandleDeliveryAsync(ulong deliveryTag,
                CancellationToken cancellationToken = default)
            {
                return Channel.BasicAckAsync(deliveryTag, false, cancellationToken).AsTask();
            }
        }

        public class NackingBasicConsumer : TestBasicConsumer
        {
            public NackingBasicConsumer(IChannel channel, ushort totalMessageCount, TaskCompletionSource<bool> allMessagesSeenTcs)
                : base(channel, totalMessageCount, allMessagesSeenTcs)
            {
            }

            public override Task PostHandleDeliveryAsync(ulong deliveryTag,
                CancellationToken cancellationToken = default)
            {
                return Channel.BasicNackAsync(deliveryTag, false, false, cancellationToken).AsTask();
            }
        }

        public class RejectingBasicConsumer : TestBasicConsumer
        {
            public RejectingBasicConsumer(IChannel channel, ushort totalMessageCount, TaskCompletionSource<bool> allMessagesSeenTcs)
                : base(channel, totalMessageCount, allMessagesSeenTcs)
            {
            }

            public override Task PostHandleDeliveryAsync(ulong deliveryTag,
                CancellationToken cancellationToken = default)
            {
                return Channel.BasicRejectAsync(deliveryTag, false, cancellationToken).AsTask();
            }
        }

        public class TestBasicConsumer : AsyncDefaultBasicConsumer
        {
            protected readonly TaskCompletionSource<bool> _allMessagesSeenTcs;
            protected readonly ushort _totalMessageCount;
            protected ushort _counter = 0;

            public TestBasicConsumer(IChannel channel, ushort totalMessageCount, TaskCompletionSource<bool> allMessagesSeenTcs)
                : base(channel)
            {
                _totalMessageCount = totalMessageCount;
                _allMessagesSeenTcs = allMessagesSeenTcs;
            }

            public override Task HandleBasicDeliverAsync(string consumerTag,
                ulong deliveryTag,
                bool redelivered,
                string exchange,
                string routingKey,
                IReadOnlyBasicProperties properties,
                ReadOnlyMemory<byte> body,
                CancellationToken cancellationToken = default)
            {
                try
                {
                    return PostHandleDeliveryAsync(deliveryTag, cancellationToken);
                }
                finally
                {
                    ++_counter;
                    if (_counter >= _totalMessageCount)
                    {
                        _allMessagesSeenTcs.SetResult(true);
                    }
                }
            }

            public virtual Task PostHandleDeliveryAsync(ulong deliveryTag,
                CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        protected async Task<bool> SendAndConsumeMessageAsync(IConnection conn, string queue, string exchange, string routingKey)
        {
            using (IChannel ch = await conn.CreateChannelAsync(_createChannelOptions))
            {
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                var consumer = new AckingBasicConsumer(ch, 1, tcs);

                await ch.BasicConsumeAsync(queue, false, consumer);

                await ch.BasicPublishAsync(exchange: exchange, routingKey: routingKey,
                    body: _encoding.GetBytes("test message"), mandatory: true);

                try
                {
                    await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    return tcs.Task.IsCompletedSuccessfully();
                }
                catch (TimeoutException)
                {
                    return false;
                }
                finally
                {
                    await ch.CloseAsync();
                }
            }
        }
    }
}
