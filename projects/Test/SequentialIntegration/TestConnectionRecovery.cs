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
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;
using Xunit;
using Xunit.Abstractions;
using QueueDeclareOk = RabbitMQ.Client.QueueDeclareOk;

namespace Test.SequentialIntegration
{
    public class TestConnectionRecovery : SequentialIntegrationFixture
    {
        private readonly string _queueName;

        public TestConnectionRecovery(ITestOutputHelper output) : base(output)
        {
            _queueName = $"{nameof(TestConnectionRecovery)}-{Guid.NewGuid()}";
        }

        protected override void DisposeAssertions()
        {
            /*
             * Note: don't do anything since these tests can cause callback
             * exceptions during recovery, due to recovery taking longer than
             * the recovery interval. There may be connection exceptions that happen
             * that are OK.
             */
        }

        public override async Task DisposeAsync()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.ClientProvidedName += "-TearDown";
            using (IConnection conn = await cf.CreateConnectionAsync())
            {
                using (IChannel ch = await conn.CreateChannelAsync())
                {
                    await ch.QueueDeleteAsync(_queueName);
                    await ch.CloseAsync();
                }
                await conn.CloseAsync();
            }

            await base.DisposeAsync();
        }

        [Fact]
        public async Task TestBasicChannelRecoveryOnServerRestart()
        {
            Assert.True(_channel.IsOpen);
            await RestartServerAndWaitForRecoveryAsync();
            Assert.True(_channel.IsOpen);
        }

        // https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1086
        [Fact]
        public async Task TestChannelAfterDispose_GH1086()
        {
            TaskCompletionSource<bool> sawChannelShutdownTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            Task _channel_ChannelShutdownAsync(object sender, ShutdownEventArgs e)
            {
                sawChannelShutdownTcs.TrySetResult(true);
                return Task.CompletedTask;
            }

            _channel.ChannelShutdownAsync += _channel_ChannelShutdownAsync;

            Assert.True(_channel.IsOpen);

            string queueName = GenerateQueueName();
            RabbitMQ.Client.QueueDeclareOk queueDeclareOk = await _channel.QueueDeclareAsync(queue: queueName, exclusive: false, autoDelete: false);
            Assert.Equal(queueName, queueDeclareOk.QueueName);

            byte[] body = GetRandomBody(64);

            await RestartServerAndWaitForRecoveryAsync();

            Task publishTask = Task.Run(async () =>
            {
                while (false == sawChannelShutdownTcs.Task.IsCompleted)
                {
                    try
                    {
                        await _channel.BasicPublishAsync(exchange: "", routingKey: queueName, body: body, mandatory: true);
                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"{_testDisplayName} caught exception: {ex}");
                        break;
                    }
                }
            });

            await Task.WhenAny(sawChannelShutdownTcs.Task, publishTask);

            bool sawChannelShutdown = await sawChannelShutdownTcs.Task;
            Assert.True(sawChannelShutdown);

            // This is false because the channel has been recovered
            Assert.False(_channel.IsClosed);

            await _channel.CloseAsync();
            _channel.Dispose();
            Assert.True(_channel.IsClosed);
            _channel = null;

            await publishTask;
        }

        [Fact]
        public async Task TestBlockedListenersRecovery()
        {
            try
            {
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _conn.ConnectionBlockedAsync += (c, reason) =>
                {
                    tcs.SetResult(true);
                    return Task.CompletedTask;
                };
                await CloseAndWaitForRecoveryAsync();
                await CloseAndWaitForRecoveryAsync();
                await BlockAsync(_channel);
                await WaitAsync(tcs, "connection blocked");
            }
            finally
            {
                await UnblockAsync();
            }
        }

        [Fact]
        public Task TestClientNamedQueueRecoveryOnServerRestart()
        {
            string s = "dotnet-client.test.recovery.q1";
            return WithTemporaryNonExclusiveQueueAsync(_channel, async (m, q) =>
            {
                await RestartServerAndWaitForRecoveryAsync();
                await AssertQueueRecoveryAsync(m, q, false);
                await _channel.QueueDeleteAsync(q);
            }, s);
        }

        // rabbitmq/rabbitmq-dotnet-client#43
        [Fact]
        public async Task TestClientNamedTransientAutoDeleteQueueAndBindingRecovery()
        {
            string queueName = GenerateQueueName();
            string exchangeName = GenerateExchangeName();
            try
            {
                await _channel.QueueDeleteAsync(queueName);
                await _channel.ExchangeDeleteAsync(exchangeName);

                await _channel.ExchangeDeclareAsync(exchange: exchangeName, type: "fanout");
                await _channel.QueueDeclareAsync(queue: queueName, durable: false, exclusive: false, autoDelete: true, arguments: null);
                await _channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: "");

                await RestartServerAndWaitForRecoveryAsync();
                Assert.True(_channel.IsOpen);

                await _channel.ConfirmSelectAsync();
                QueueDeclareOk ok0 = await _channel.QueueDeclarePassiveAsync(queue: queueName);
                Assert.Equal(queueName, ok0.QueueName);
                await _channel.QueuePurgeAsync(queueName);
                await _channel.ExchangeDeclarePassiveAsync(exchange: exchangeName);
                await _channel.BasicPublishAsync(exchange: exchangeName, routingKey: "", body: _encoding.GetBytes("msg"));

                await WaitForConfirmsWithCancellationAsync(_channel);

                QueueDeclareOk ok1 = await _channel.QueueDeclarePassiveAsync(queue: queueName);
                Assert.Equal(1u, ok1.MessageCount);
            }
            finally
            {
                await _channel.QueueDeleteAsync(queueName);
                await _channel.ExchangeDeleteAsync(exchangeName);
            }
        }

        // rabbitmq/rabbitmq-dotnet-client#43
        [Fact]
        public async Task TestServerNamedTransientAutoDeleteQueueAndBindingRecovery()
        {
            string x = "tmp-fanout";
            await _channel.ExchangeDeleteAsync(x);
            await _channel.ExchangeDeclareAsync(exchange: x, type: "fanout");
            string q = (await _channel.QueueDeclareAsync(queue: "", durable: false, exclusive: false, autoDelete: true, arguments: null)).QueueName;
            string nameBefore = q;
            string nameAfter = null;
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            ((AutorecoveringConnection)_conn).QueueNameChangedAfterRecoveryAsync += (source, ea) =>
            {
                nameBefore = ea.NameBefore;
                nameAfter = ea.NameAfter;
                tcs.SetResult(true);
                return Task.CompletedTask;
            };

            await _channel.QueueBindAsync(queue: nameBefore, exchange: x, routingKey: "");
            await RestartServerAndWaitForRecoveryAsync();

            await WaitAsync(tcs, "queue name change after recovery");
            Assert.True(_channel.IsOpen);
            Assert.NotEqual(nameBefore, nameAfter);

            await _channel.ConfirmSelectAsync();
            await _channel.ExchangeDeclareAsync(exchange: x, type: "fanout");
            await _channel.BasicPublishAsync(exchange: x, routingKey: "", body: _encoding.GetBytes("msg"));
            await WaitForConfirmsWithCancellationAsync(_channel);

            QueueDeclareOk ok = await _channel.QueueDeclarePassiveAsync(nameAfter);
            Assert.Equal(1u, ok.MessageCount);
            await _channel.QueueDeleteAsync(q);
            await _channel.ExchangeDeleteAsync(x);
        }

        [Fact]
        public async Task TestShutdownEventHandlersRecoveryOnConnectionAfterDelayedServerRestart()
        {
            int counter = 0;
            _conn.ConnectionShutdownAsync += (c, args) =>
            {
                Interlocked.Increment(ref counter);
                return Task.CompletedTask;
            };
            TaskCompletionSource<bool> shutdownLatch = PrepareForShutdown(_conn);
            TaskCompletionSource<bool> recoveryLatch = PrepareForRecovery((AutorecoveringConnection)_conn);

            Assert.True(_conn.IsOpen);

            try
            {
                await StopRabbitMqAsync();
                await Task.Delay(TimeSpan.FromSeconds(7));
            }
            finally
            {
                await StartRabbitMqAsync();
            }

            await WaitAsync(shutdownLatch, WaitSpan, "connection shutdown");
            await WaitAsync(recoveryLatch, WaitSpan, "connection recovery");

            Assert.True(_conn.IsOpen);
            Assert.True(counter >= 1);
        }

        [Fact]
        public async Task TestShutdownEventHandlersRecoveryOnConnectionAfterTwoDelayedServerRestarts_GH1623()
        {
            const int restartCount = 2;
            int counter = 0;
            TimeSpan delaySpan = TimeSpan.FromSeconds(_connFactory.NetworkRecoveryInterval.TotalSeconds * 2);

            AutorecoveringConnection aconn = (AutorecoveringConnection)_conn;

            aconn.ConnectionRecoveryErrorAsync += (c, args) =>
            {
                // Uncomment for debugging
                // _output.WriteLine("[INFO] ConnectionRecoveryError: {0}", args.Exception);
                return Task.CompletedTask;
            };

            aconn.ConnectionShutdownAsync += (c, args) =>
            {
                Interlocked.Increment(ref counter);
                return Task.CompletedTask;
            };

            Assert.True(_conn.IsOpen);

            TaskCompletionSource<bool> recoveryLatch = null;

            for (int i = 0; i < restartCount; i++)
            {
                if (i == (restartCount - 1))
                {
                    recoveryLatch = PrepareForRecovery(aconn);
                }

                try
                {
                    await StopRabbitMqAsync();
                    await Task.Delay(delaySpan);
                }
                finally
                {
                    await StartRabbitMqAsync();
                    // Ensure recovery has a chance to connect!
                    await Task.Delay(delaySpan);
                }
            }

            await WaitAsync(recoveryLatch, WaitSpan, "connection recovery");

            Assert.True(aconn.IsOpen);
            Assert.Equal(restartCount, counter);
        }

        [Fact]
        public async Task TestUnblockedListenersRecovery()
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _conn.ConnectionUnblockedAsync += (source, ea) =>
            {
                tcs.SetResult(true);
                return Task.CompletedTask;
            };
            await CloseAndWaitForRecoveryAsync();
            await CloseAndWaitForRecoveryAsync();
            await BlockAsync(_channel);
            await UnblockAsync();
            await WaitAsync(tcs, "connection unblocked");
        }
    }
}
