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
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;
using Xunit;
using Xunit.Abstractions;
using QueueDeclareOk = RabbitMQ.Client.QueueDeclareOk;

namespace Test.SequentialIntegration
{
    public class TestConnectionRecovery : TestConnectionRecoveryBase
    {
        private readonly string _queueName;

        public TestConnectionRecovery(ITestOutputHelper output) : base(output)
        {
            _queueName = $"{nameof(TestConnectionRecovery)}-{Guid.NewGuid()}";
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
        public async Task TestBasicAckAfterChannelRecovery()
        {
            var allMessagesSeenTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cons = new AckingBasicConsumer(_channel, _totalMessageCount, allMessagesSeenTcs);

            QueueDeclareOk q = await _channel.QueueDeclareAsync(_queueName, false, false, false);
            string queueName = q.QueueName;
            Assert.Equal(queueName, _queueName);

            await _channel.BasicQosAsync(0, 1, false);
            await _channel.BasicConsumeAsync(queueName, false, cons);

            TaskCompletionSource<bool> sl = PrepareForShutdown(_conn);
            TaskCompletionSource<bool> rl = PrepareForRecovery(_conn);

            await PublishMessagesWhileClosingConnAsync(queueName);

            await WaitAsync(sl, "connection shutdown");
            await WaitAsync(rl, "connection recovery");
            await WaitAsync(allMessagesSeenTcs, "all messages seen");
        }

        [Fact]
        public async Task TestBasicNackAfterChannelRecovery()
        {
            var allMessagesSeenTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cons = new NackingBasicConsumer(_channel, _totalMessageCount, allMessagesSeenTcs);

            QueueDeclareOk q = await _channel.QueueDeclareAsync(_queueName, false, false, false);
            string queueName = q.QueueName;
            Assert.Equal(queueName, _queueName);

            await _channel.BasicQosAsync(0, 1, false);
            await _channel.BasicConsumeAsync(queueName, false, cons);

            TaskCompletionSource<bool> sl = PrepareForShutdown(_conn);
            TaskCompletionSource<bool> rl = PrepareForRecovery(_conn);

            await PublishMessagesWhileClosingConnAsync(queueName);

            await WaitAsync(sl, "connection shutdown");
            await WaitAsync(rl, "connection recovery");
            await WaitAsync(allMessagesSeenTcs, "all messages seen");
        }

        [Fact]
        public async Task TestBasicRejectAfterChannelRecovery()
        {
            var allMessagesSeenTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cons = new RejectingBasicConsumer(_channel, _totalMessageCount, allMessagesSeenTcs);

            string queueName = (await _channel.QueueDeclareAsync(_queueName, false, false, false)).QueueName;
            Assert.Equal(queueName, _queueName);

            await _channel.BasicQosAsync(0, 1, false);
            await _channel.BasicConsumeAsync(queueName, false, cons);

            TaskCompletionSource<bool> sl = PrepareForShutdown(_conn);
            TaskCompletionSource<bool> rl = PrepareForRecovery(_conn);

            await PublishMessagesWhileClosingConnAsync(queueName);

            await WaitAsync(sl, "connection shutdown");
            await WaitAsync(rl, "connection recovery");
            await WaitAsync(allMessagesSeenTcs, "all messages seen");
        }

        [Fact]
        public async Task TestBasicAckAfterBasicGetAndChannelRecovery()
        {
            string q = GenerateQueueName();
            await _channel.QueueDeclareAsync(q, false, false, false);
            // create an offset
            await _channel.BasicPublishAsync("", q, _messageBody);
            await Task.Delay(50);
            BasicGetResult g = await _channel.BasicGetAsync(q, false);
            await CloseAndWaitForRecoveryAsync();
            Assert.True(_conn.IsOpen);
            Assert.True(_channel.IsOpen);
            // ack the message after recovery - this should be out of range and ignored
            await _channel.BasicAckAsync(g.DeliveryTag, false);
            // do a sync operation to 'check' there is no channel exception
            await _channel.BasicGetAsync(q, false);
        }

        [Fact]
        public async Task TestBasicAckEventHandlerRecovery()
        {
            await _channel.ConfirmSelectAsync();
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ((AutorecoveringChannel)_channel).BasicAcks += (m, args) => tcs.SetResult(true);
            ((AutorecoveringChannel)_channel).BasicNacks += (m, args) => tcs.SetResult(true);

            await CloseAndWaitForRecoveryAsync();
            await CloseAndWaitForRecoveryAsync();
            Assert.True(_channel.IsOpen);

            await WithTemporaryNonExclusiveQueueAsync(_channel, (ch, q) =>
            {
                return ch.BasicPublishAsync("", q, _messageBody).AsTask();
            });

            await WaitAsync(tcs, "basic acks/nacks");
        }

        [Fact]
        public async Task TestBasicConnectionRecovery()
        {
            Assert.True(_conn.IsOpen);
            await CloseAndWaitForRecoveryAsync();
            Assert.True(_conn.IsOpen);
        }

        [Fact]
        public async Task BasicConnectionRecoveryOnBrokerRestart()
        {
            Assert.True(_conn.IsOpen);
            await RestartServerAndWaitForRecoveryAsync();
            Assert.True(_conn.IsOpen);
        }

        [Fact]
        public async Task TestBasicChannelRecovery()
        {
            Assert.True(_channel.IsOpen);
            await CloseAndWaitForRecoveryAsync();
            Assert.True(_channel.IsOpen);
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

            void _channel_ChannelShutdown(object sender, ShutdownEventArgs e)
            {
                sawChannelShutdownTcs.SetResult(true);
            }

            _channel.ChannelShutdown += _channel_ChannelShutdown;

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
                _conn.ConnectionBlocked += (c, reason) => tcs.SetResult(true);
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
        public Task TestClientNamedQueueRecovery()
        {
            string s = "dotnet-client.test.recovery.q1";
            return WithTemporaryNonExclusiveQueueAsync(_channel, async (m, q) =>
            {
                await CloseAndWaitForRecoveryAsync();
                await AssertQueueRecoveryAsync(m, q, false);
                await _channel.QueueDeleteAsync(q);
            }, s);
        }

        [Fact]
        public Task TestClientNamedQueueRecoveryNoWait()
        {
            string s = "dotnet-client.test.recovery.q1-nowait";
            return WithTemporaryExclusiveQueueNoWaitAsync(_channel, async (ch, q) =>
            {
                await CloseAndWaitForRecoveryAsync();
                await AssertExclusiveQueueRecoveryAsync(ch, q);
            }, s);
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

        [Fact]
        public async Task TestConsumerRecoveryWithManyConsumers()
        {
            string q = (await _channel.QueueDeclareAsync(GenerateQueueName(), false, false, false)).QueueName;
            int n = 1024;

            for (int i = 0; i < n; i++)
            {
                var cons = new EventingBasicConsumer(_channel);
                await _channel.BasicConsumeAsync(q, true, cons);
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ((AutorecoveringConnection)_conn).ConsumerTagChangeAfterRecovery += (prev, current) => tcs.SetResult(true);

            await CloseAndWaitForRecoveryAsync();
            await WaitAsync(tcs, "consumer tag change after recovery");
            Assert.True(_channel.IsOpen);
            await AssertConsumerCountAsync(q, n);
        }

        [Fact]
        public async Task TestDeclarationOfManyAutoDeleteExchangesWithTransientExchangesThatAreDeleted()
        {
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 3; i++)
            {
                string x1 = $"source-{Guid.NewGuid()}";
                await _channel.ExchangeDeclareAsync(x1, "fanout", false, true);

                string x2 = $"destination-{Guid.NewGuid()}";
                await _channel.ExchangeDeclareAsync(x2, "fanout", false, false);

                await _channel.ExchangeBindAsync(x2, x1, "");
                await _channel.ExchangeDeleteAsync(x2);
            }
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
        }

        [Fact]
        public async Task TestDeclarationOfManyAutoDeleteExchangesWithTransientExchangesThatAreUnbound()
        {
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string x1 = $"source-{Guid.NewGuid()}";
                await _channel.ExchangeDeclareAsync(x1, "fanout", false, true);
                string x2 = $"destination-{Guid.NewGuid()}";
                await _channel.ExchangeDeclareAsync(x2, "fanout", false, false);
                await _channel.ExchangeBindAsync(x2, x1, "");
                await _channel.ExchangeUnbindAsync(x2, x1, "");
                await _channel.ExchangeDeleteAsync(x2);
            }
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
        }

        [Fact]
        public async Task TestDeclarationOfManyAutoDeleteExchangesWithTransientQueuesThatAreDeleted()
        {
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string x = Guid.NewGuid().ToString();
                await _channel.ExchangeDeclareAsync(x, "fanout", false, true);
                RabbitMQ.Client.QueueDeclareOk q = await _channel.QueueDeclareAsync();
                await _channel.QueueBindAsync(q, x, "");
                await _channel.QueueDeleteAsync(q);
            }
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
        }

        [Fact]
        public async Task TestDeclarationOfManyAutoDeleteExchangesWithTransientQueuesThatAreUnbound()
        {
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string x = Guid.NewGuid().ToString();
                await _channel.ExchangeDeclareAsync(x, "fanout", false, true);
                RabbitMQ.Client.QueueDeclareOk q = await _channel.QueueDeclareAsync();
                await _channel.QueueBindAsync(q, x, "");
                await _channel.QueueUnbindAsync(q, x, "", null);
            }
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
        }

        [Fact]
        public async Task TestDeclarationOfManyAutoDeleteQueuesWithTransientConsumer()
        {
            AssertRecordedQueues((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string q = Guid.NewGuid().ToString();
                await _channel.QueueDeclareAsync(q, false, false, true);
                var dummy = new EventingBasicConsumer(_channel);
                string tag = await _channel.BasicConsumeAsync(q, true, dummy);
                await _channel.BasicCancelAsync(tag);
            }
            AssertRecordedQueues((AutorecoveringConnection)_conn, 0);
        }

        [Fact]
        public async Task TestExchangeRecovery()
        {
            string x = "dotnet-client.test.recovery.x1";
            await DeclareNonDurableExchangeAsync(_channel, x);
            await CloseAndWaitForRecoveryAsync();
            await AssertExchangeRecoveryAsync(_channel, x);
            await _channel.ExchangeDeleteAsync(x);
        }

        [Fact]
        public async Task TestExchangeToExchangeBindingRecovery()
        {
            string q = (await _channel.QueueDeclareAsync("", false, false, false)).QueueName;
            string x1 = "amq.fanout";
            string x2 = GenerateExchangeName();

            await _channel.ExchangeDeclareAsync(x2, "fanout");
            await _channel.ExchangeBindAsync(x1, x2, "");
            await _channel.QueueBindAsync(q, x1, "");

            try
            {
                await CloseAndWaitForRecoveryAsync();
                Assert.True(_channel.IsOpen);
                await _channel.BasicPublishAsync(x2, "", _encoding.GetBytes("msg"));
                await AssertMessageCountAsync(q, 1);
            }
            finally
            {
                await WithTemporaryChannelAsync(async ch =>
                {
                    await ch.ExchangeDeleteAsync(x2);
                    await ch.QueueDeleteAsync(q);
                });
            }
        }

        [Fact]
        public async Task TestQueueRecoveryWithManyQueues()
        {
            var qs = new List<string>();
            int n = 1024;
            for (int i = 0; i < n; i++)
            {
                QueueDeclareOk q = await _channel.QueueDeclareAsync(GenerateQueueName(), false, false, false);
                qs.Add(q.QueueName);
            }
            await CloseAndWaitForRecoveryAsync();
            Assert.True(_channel.IsOpen);
            foreach (string q in qs)
            {
                await AssertQueueRecoveryAsync(_channel, q, false);
                await _channel.QueueDeleteAsync(q);
            }
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

            ((AutorecoveringConnection)_conn).QueueNameChangedAfterRecovery += (source, ea) =>
            {
                nameBefore = ea.NameBefore;
                nameAfter = ea.NameAfter;
                tcs.SetResult(true);
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
        public async Task TestRecoveryEventHandlersOnConnection()
        {
            int counter = 0;
            ((AutorecoveringConnection)_conn).RecoverySucceeded += (source, ea) => Interlocked.Increment(ref counter);

            await CloseAndWaitForRecoveryAsync();
            await CloseAndWaitForRecoveryAsync();
            await CloseAndWaitForRecoveryAsync();
            await CloseAndWaitForRecoveryAsync();
            Assert.True(_conn.IsOpen);
            Assert.True(counter >= 3);
        }

        [Fact]
        public async Task TestRecoveryEventHandlersOnChannel()
        {
            int counter = 0;
            ((AutorecoveringChannel)_channel).Recovery += (source, ea) => Interlocked.Increment(ref counter);

            await CloseAndWaitForRecoveryAsync();
            await CloseAndWaitForRecoveryAsync();
            await CloseAndWaitForRecoveryAsync();
            await CloseAndWaitForRecoveryAsync();
            Assert.True(_channel.IsOpen);
            Assert.True(counter >= 3);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public async Task TestRecoveringConsumerHandlerOnConnection(int iterations)
        {
            string q = (await _channel.QueueDeclareAsync(GenerateQueueName(), false, false, false)).QueueName;
            var cons = new EventingBasicConsumer(_channel);
            await _channel.BasicConsumeAsync(q, true, cons);

            int counter = 0;
            ((AutorecoveringConnection)_conn).RecoveringConsumer += (sender, args) => Interlocked.Increment(ref counter);

            for (int i = 0; i < iterations; i++)
            {
                await CloseAndWaitForRecoveryAsync();
            }

            Assert.Equal(iterations, counter);
        }

        [Fact]
        public async Task TestRecoveringConsumerHandlerOnConnection_EventArgumentsArePassedDown()
        {
            var myArgs = new Dictionary<string, object> { { "first-argument", "some-value" } };
            string q = (await _channel.QueueDeclareAsync(GenerateQueueName(), false, false, false)).QueueName;
            var cons = new EventingBasicConsumer(_channel);
            string expectedCTag = await _channel.BasicConsumeAsync(cons, q, arguments: myArgs);

            bool ctagMatches = false;
            bool consumerArgumentMatches = false;
            ((AutorecoveringConnection)_conn).RecoveringConsumer += (sender, args) =>
            {
                // We cannot assert here because NUnit throws when an assertion fails. This exception is caught and
                // passed to a CallbackExceptionHandler, instead of failing the test. Instead, we have to do this trick
                // and assert in the test function.
                ctagMatches = args.ConsumerTag == expectedCTag;
                consumerArgumentMatches = (string)args.ConsumerArguments["first-argument"] == "some-value";
                args.ConsumerArguments["first-argument"] = "event-handler-set-this-value";
            };

            await CloseAndWaitForRecoveryAsync();
            Assert.True(ctagMatches, "expected consumer tag to match");
            Assert.True(consumerArgumentMatches, "expected consumer arguments to match");
            string actualVal = (string)Assert.Contains("first-argument", myArgs as IDictionary<string, object>);
            Assert.Equal("event-handler-set-this-value", actualVal);
        }

        [Fact]
        public async Task TestServerNamedQueueRecovery()
        {
            string q = (await _channel.QueueDeclareAsync("", false, false, false)).QueueName;
            string x = "amq.fanout";
            await _channel.QueueBindAsync(q, x, "");

            string nameBefore = q;
            string nameAfter = null;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var connection = (AutorecoveringConnection)_conn;
            connection.RecoverySucceeded += (source, ea) => tcs.SetResult(true);
            connection.QueueNameChangedAfterRecovery += (source, ea) => { nameAfter = ea.NameAfter; };

            await CloseAndWaitForRecoveryAsync();
            await WaitAsync(tcs, "recovery succeeded");

            Assert.NotNull(nameAfter);
            Assert.StartsWith("amq.", nameBefore);
            Assert.StartsWith("amq.", nameAfter);
            Assert.NotEqual(nameBefore, nameAfter);

            await _channel.QueueDeclarePassiveAsync(nameAfter);
        }

        [Fact]
        public async Task TestShutdownEventHandlersRecoveryOnConnection()
        {
            int counter = 0;
            _conn.ConnectionShutdown += (c, args) => Interlocked.Increment(ref counter);

            Assert.True(_conn.IsOpen);
            await CloseAndWaitForRecoveryAsync();
            await CloseAndWaitForRecoveryAsync();
            await CloseAndWaitForRecoveryAsync();
            await CloseAndWaitForRecoveryAsync();
            Assert.True(_conn.IsOpen);

            Assert.True(counter >= 3);
        }

        [Fact]
        public async Task TestShutdownEventHandlersRecoveryOnConnectionAfterDelayedServerRestart()
        {
            int counter = 0;
            _conn.ConnectionShutdown += (c, args) => Interlocked.Increment(ref counter);
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
        public async Task TestShutdownEventHandlersRecoveryOnChannel()
        {
            int counter = 0;
            _channel.ChannelShutdown += (c, args) => Interlocked.Increment(ref counter);

            Assert.True(_channel.IsOpen);
            await CloseAndWaitForRecoveryAsync();
            await CloseAndWaitForRecoveryAsync();
            await CloseAndWaitForRecoveryAsync();
            await CloseAndWaitForRecoveryAsync();
            Assert.True(_channel.IsOpen);

            Assert.True(counter >= 3);
        }

        [Fact]
        public async Task TestRecoverTopologyOnDisposedChannel()
        {
            string x = GenerateExchangeName();
            string q = GenerateQueueName();
            const string rk = "routing-key";

            using (IChannel ch = await _conn.CreateChannelAsync())
            {
                await ch.ExchangeDeclareAsync(exchange: x, type: "fanout");
                await ch.QueueDeclareAsync(q, false, false, false);
                await ch.QueueBindAsync(q, x, rk);
                await ch.CloseAsync();
            }

            var cons = new EventingBasicConsumer(_channel);
            await _channel.BasicConsumeAsync(q, true, cons);
            await AssertConsumerCountAsync(_channel, q, 1);

            await CloseAndWaitForRecoveryAsync();
            await AssertConsumerCountAsync(_channel, q, 1);

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            cons.Received += (s, args) => tcs.SetResult(true);

            await _channel.BasicPublishAsync("", q, _messageBody);
            await WaitAsync(tcs, "received event");

            await _channel.QueueUnbindAsync(q, x, rk);
            await _channel.ExchangeDeleteAsync(x);
            await _channel.QueueDeleteAsync(q);
        }

        [Fact]
        public async Task TestPublishRpcRightAfterReconnect()
        {
            string testQueueName = $"dotnet-client.test.{nameof(TestPublishRpcRightAfterReconnect)}";
            await _channel.QueueDeclareAsync(testQueueName, false, false, false);
            var replyConsumer = new EventingBasicConsumer(_channel);
            await _channel.BasicConsumeAsync("amq.rabbitmq.reply-to", true, replyConsumer);
            var properties = new BasicProperties();
            properties.ReplyTo = "amq.rabbitmq.reply-to";

            TimeSpan doneSpan = TimeSpan.FromMilliseconds(100);
            var doneTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task closeTask = Task.Run(async () =>
            {
                try
                {

                    await CloseAndWaitForRecoveryAsync();
                }
                finally
                {
                    doneTcs.SetResult(true);
                }
            });

            while (false == doneTcs.Task.IsCompletedSuccessfully())
            {
                try
                {
                    await _channel.BasicPublishAsync(string.Empty, testQueueName, properties, _messageBody);
                }
                catch (Exception e)
                {
                    if (e is AlreadyClosedException a)
                    {
                        // 406 is received, when the reply consumer isn't yet recovered
                        Assert.NotEqual(406, a.ShutdownReason.ReplyCode);
                    }
                }

                try
                {
                    await doneTcs.Task.WaitAsync(doneSpan);
                }
                catch (TimeoutException)
                {
                }
            }

            await closeTask;
        }

        [Fact]
        public async Task TestThatCancelledConsumerDoesNotReappearOnRecovery()
        {
            string q = (await _channel.QueueDeclareAsync(GenerateQueueName(), false, false, false)).QueueName;
            int n = 1024;

            for (int i = 0; i < n; i++)
            {
                var cons = new EventingBasicConsumer(_channel);
                string tag = await _channel.BasicConsumeAsync(q, true, cons);
                await _channel.BasicCancelAsync(tag);
            }
            await CloseAndWaitForRecoveryAsync();
            Assert.True(_channel.IsOpen);
            await AssertConsumerCountAsync(q, 0);
        }

        [Fact]
        public async Task TestThatDeletedExchangeBindingsDontReappearOnRecovery()
        {
            string q = (await _channel.QueueDeclareAsync("", false, false, false)).QueueName;
            string x1 = "amq.fanout";
            string x2 = GenerateExchangeName();

            await _channel.ExchangeDeclareAsync(x2, "fanout");
            await _channel.ExchangeBindAsync(x1, x2, "");
            await _channel.QueueBindAsync(q, x1, "");
            await _channel.ExchangeUnbindAsync(x1, x2, "");

            try
            {
                await CloseAndWaitForRecoveryAsync();
                Assert.True(_channel.IsOpen);
                await _channel.BasicPublishAsync(x2, "", _encoding.GetBytes("msg"));
                await AssertMessageCountAsync(q, 0);
            }
            finally
            {
                await WithTemporaryChannelAsync(async ch =>
                {
                    await ch.ExchangeDeleteAsync(x2);
                    await ch.QueueDeleteAsync(q);
                });
            }
        }

        [Fact]
        public async Task TestThatDeletedExchangesDontReappearOnRecovery()
        {
            string x = GenerateExchangeName();
            await _channel.ExchangeDeclareAsync(x, "fanout");
            await _channel.ExchangeDeleteAsync(x);

            try
            {
                await CloseAndWaitForRecoveryAsync();
                Assert.True(_channel.IsOpen);
                await _channel.ExchangeDeclarePassiveAsync(x);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException e)
            {
                // expected
                AssertShutdownError(e.ShutdownReason, 404);
            }
        }

        [Fact]
        public async Task TestThatDeletedQueueBindingsDontReappearOnRecovery()
        {
            string q = (await _channel.QueueDeclareAsync("", false, false, false)).QueueName;
            string x1 = "amq.fanout";
            string x2 = GenerateExchangeName();

            await _channel.ExchangeDeclareAsync(x2, "fanout");
            await _channel.ExchangeBindAsync(x1, x2, "");
            await _channel.QueueBindAsync(q, x1, "");
            await _channel.QueueUnbindAsync(q, x1, "", null);

            try
            {
                await CloseAndWaitForRecoveryAsync();
                Assert.True(_channel.IsOpen);
                await _channel.BasicPublishAsync(x2, "", _encoding.GetBytes("msg"));
                await AssertMessageCountAsync(q, 0);
            }
            finally
            {
                await WithTemporaryChannelAsync(async ch =>
                {
                    await ch.ExchangeDeleteAsync(x2);
                    await ch.QueueDeleteAsync(q);
                });
            }
        }

        [Fact]
        public async Task TestThatDeletedQueuesDontReappearOnRecovery()
        {
            string q = "dotnet-client.recovery.q1";
            await _channel.QueueDeclareAsync(q, false, false, false);
            await _channel.QueueDeleteAsync(q);

            try
            {
                await CloseAndWaitForRecoveryAsync();
                Assert.True(_channel.IsOpen);
                await _channel.QueueDeclarePassiveAsync(q);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException e)
            {
                // expected
                AssertShutdownError(e.ShutdownReason, 404);
            }
        }

        [Fact]
        public async Task TestUnblockedListenersRecovery()
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _conn.ConnectionUnblocked += (source, ea) => tcs.SetResult(true);
            await CloseAndWaitForRecoveryAsync();
            await CloseAndWaitForRecoveryAsync();
            await BlockAsync(_channel);
            await UnblockAsync();
            await WaitAsync(tcs, "connection unblocked");
        }

        [Fact]
        public async Task TestBindingRecovery_GH1035()
        {
            const string routingKey = "unused";
            byte[] body = GetRandomBody();

            var receivedMessageSemaphore = new SemaphoreSlim(0, 1);

            void MessageReceived(object sender, BasicDeliverEventArgs e)
            {
                receivedMessageSemaphore.Release();
            }

            string exchangeName = $"ex-gh-1035-{Guid.NewGuid()}";
            string queueName = $"q-gh-1035-{Guid.NewGuid()}";

            await _channel.ExchangeDeclareAsync(exchange: exchangeName,
                type: "fanout", durable: false, autoDelete: true,
                arguments: null);

            QueueDeclareOk q0 = await _channel.QueueDeclareAsync(queue: queueName, exclusive: true);
            Assert.Equal(queueName, q0);

            await _channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: routingKey);

            await _channel.CloseAsync();
            _channel.Dispose();
            _channel = null;

            _channel = await _conn.CreateChannelAsync();

            await _channel.ExchangeDeclareAsync(exchange: exchangeName,
                type: "fanout", durable: false, autoDelete: true,
                arguments: null);

            QueueDeclareOk q1 = await _channel.QueueDeclareAsync(queue: queueName, exclusive: true);
            Assert.Equal(queueName, q1.QueueName);

            await _channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: routingKey);

            var c = new EventingBasicConsumer(_channel);
            c.Received += MessageReceived;
            await _channel.BasicConsumeAsync(queue: queueName, autoAck: true, consumer: c);

            using (IChannel pubCh = await _conn.CreateChannelAsync())
            {
                await pubCh.BasicPublishAsync(exchange: exchangeName, routingKey: routingKey, body: body);
                await pubCh.CloseAsync();
            }

            Assert.True(await receivedMessageSemaphore.WaitAsync(WaitSpan));

            await CloseAndWaitForRecoveryAsync();

            using (IChannel pubCh = await _conn.CreateChannelAsync())
            {
                await pubCh.BasicPublishAsync(exchange: exchangeName, routingKey: "unused", body: body);
                await pubCh.CloseAsync();
            }

            Assert.True(await receivedMessageSemaphore.WaitAsync(WaitSpan));
        }
    }
}
