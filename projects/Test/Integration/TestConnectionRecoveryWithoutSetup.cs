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
using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestConnectionRecoveryWithoutSetup : TestConnectionRecoveryBase
    {
        public TestConnectionRecoveryWithoutSetup(ITestOutputHelper output) : base(output)
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

        [Fact]
        public async Task TestBasicConnectionRecoveryWithHostnameList()
        {
            await using AutorecoveringConnection c = await CreateAutorecoveringConnectionAsync(new List<string> { "127.0.0.1", "localhost" });
            Assert.True(c.IsOpen);
            await CloseAndWaitForRecoveryAsync(c);
            Assert.True(c.IsOpen);
            await c.CloseAsync();
        }

        [Fact]
        public async Task TestBasicConnectionRecoveryWithHostnameListAndUnreachableHosts()
        {
            await using AutorecoveringConnection c = await CreateAutorecoveringConnectionAsync(new List<string> { "191.72.44.22", "127.0.0.1", "localhost" });
            Assert.True(c.IsOpen);
            await CloseAndWaitForRecoveryAsync(c);
            Assert.True(c.IsOpen);
            await c.CloseAsync();
        }

        [Fact]
        public async Task TestBasicConnectionRecoveryWithEndpointList()
        {
            await using AutorecoveringConnection c = await CreateAutorecoveringConnectionAsync(
                new List<AmqpTcpEndpoint>
                {
                    new AmqpTcpEndpoint("127.0.0.1"),
                    new AmqpTcpEndpoint("localhost")
                });
            Assert.True(c.IsOpen);
            await CloseAndWaitForRecoveryAsync(c);
            Assert.True(c.IsOpen);
            await c.CloseAsync();
        }

        [Fact]
        public async Task TestBasicConnectionRecoveryWithEndpointListAndUnreachableHosts()
        {
            await using AutorecoveringConnection c = await CreateAutorecoveringConnectionAsync(
                new List<AmqpTcpEndpoint>
                {
                    new AmqpTcpEndpoint("191.72.44.22"),
                    new AmqpTcpEndpoint("127.0.0.1"),
                    new AmqpTcpEndpoint("localhost")
                });
            Assert.True(c.IsOpen);
            await CloseAndWaitForRecoveryAsync(c);
            Assert.True(c.IsOpen);
            await c.CloseAsync();
        }

        [Fact]
        public async Task TestConsumerWorkServiceRecovery()
        {
            await using AutorecoveringConnection c = await CreateAutorecoveringConnectionAsync();
            await using (IChannel ch = await c.CreateChannelAsync())
            {
                string q = (await ch.QueueDeclareAsync("dotnet-client.recovery.consumer_work_pool1",
                    false, false, false)).QueueName;
                var cons = new AsyncEventingBasicConsumer(ch);
                await ch.BasicConsumeAsync(q, true, cons);
                await AssertConsumerCountAsync(ch, q, 1);

                await CloseAndWaitForRecoveryAsync(c);

                Assert.True(ch.IsOpen);
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                cons.ReceivedAsync += (s, args) =>
                {
                    tcs.SetResult(true);
                    return Task.CompletedTask;
                };

                await ch.BasicPublishAsync("", q, _encoding.GetBytes("msg"));
                await WaitAsync(tcs, "received event");

                await ch.QueueDeleteAsync(q);
                await ch.CloseAsync();
            }

            await c.CloseAsync();
        }

        [Fact]
        public async Task TestConsumerRecoveryOnClientNamedQueueWithOneRecovery()
        {
            const string q0 = "dotnet-client.recovery.queue1";
            // connection #1
            await using AutorecoveringConnection c = await CreateAutorecoveringConnectionAsync();
            await using (IChannel ch = await c.CreateChannelAsync())
            {
                string q1 = (await ch.QueueDeclareAsync(q0, false, false, false)).QueueName;
                Assert.Equal(q0, q1);

                var cons = new AsyncEventingBasicConsumer(ch);
                await ch.BasicConsumeAsync(q1, true, cons);
                await AssertConsumerCountAsync(ch, q1, 1);

                bool queueNameChangeAfterRecoveryCalled = false;
                c.QueueNameChangedAfterRecoveryAsync += (source, ea) =>
                {
                    queueNameChangeAfterRecoveryCalled = true;
                    return Task.CompletedTask;
                };

                // connection #2
                await CloseAndWaitForRecoveryAsync(c);
                await AssertConsumerCountAsync(ch, q1, 1);
                Assert.False(queueNameChangeAfterRecoveryCalled);

                // connection #3
                await CloseAndWaitForRecoveryAsync(c);
                await AssertConsumerCountAsync(ch, q1, 1);
                Assert.False(queueNameChangeAfterRecoveryCalled);

                // connection #4
                await CloseAndWaitForRecoveryAsync(c);
                await AssertConsumerCountAsync(ch, q1, 1);
                Assert.False(queueNameChangeAfterRecoveryCalled);

                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                cons.ReceivedAsync += (s, args) =>
                {
                    tcs.SetResult(true);
                    return Task.CompletedTask;
                };

                await ch.BasicPublishAsync("", q1, _encoding.GetBytes("msg"));
                await WaitAsync(tcs, "received event");

                await ch.QueueDeleteAsync(q1);
                await ch.CloseAsync();
            }

            await c.CloseAsync();
        }

        [Fact]
        public async Task TestConsumerRecoveryWithServerNamedQueue()
        {
            // https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1238
            await using AutorecoveringConnection c = await CreateAutorecoveringConnectionAsync();
            await using (IChannel ch = await c.CreateChannelAsync())
            {
                RabbitMQ.Client.QueueDeclareOk queueDeclareResult =
                    await ch.QueueDeclareAsync(queue: string.Empty, durable: false, exclusive: true, autoDelete: true, arguments: null);
                string qname = queueDeclareResult.QueueName;
                Assert.False(string.IsNullOrEmpty(qname));

                var cons = new AsyncEventingBasicConsumer(ch);
                await ch.BasicConsumeAsync(string.Empty, true, cons);
                await AssertConsumerCountAsync(ch, qname, 1);

                bool queueNameBeforeIsEqual = false;
                bool queueNameChangeAfterRecoveryCalled = false;
                string qnameAfterRecovery = null;
                c.QueueNameChangedAfterRecoveryAsync += (source, ea) =>
                {
                    queueNameChangeAfterRecoveryCalled = true;
                    queueNameBeforeIsEqual = qname.Equals(ea.NameBefore);
                    qnameAfterRecovery = ea.NameAfter;
                    return Task.CompletedTask;
                };

                await CloseAndWaitForRecoveryAsync(c);

                await AssertConsumerCountAsync(ch, qnameAfterRecovery, 1);
                Assert.True(queueNameChangeAfterRecoveryCalled);
                Assert.True(queueNameBeforeIsEqual);

                await ch.CloseAsync();
            }

            await c.CloseAsync();
        }

        [Fact]
        public async Task TestCreateChannelOnClosedAutorecoveringConnectionDoesNotHang()
        {
            // we don't want this to recover quickly in this test
            await using AutorecoveringConnection conn = await CreateAutorecoveringConnectionAsync(TimeSpan.FromSeconds(20));
            try
            {
                await conn.CloseAsync();
                await WaitForShutdownAsync(conn);
                Assert.False(conn.IsOpen);
                await conn.CreateChannelAsync();
                Assert.Fail("Expected an exception");
            }
            catch (AlreadyClosedException)
            {
                // expected
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

        [Fact]
        public async Task TestTopologyRecoveryConsumerFilter()
        {
            string exchange = GenerateExchangeName();
            string queueWithRecoveredConsumer = GenerateQueueName();
            string queueWithIgnoredConsumer = GenerateQueueName();
            const string binding1 = "recovered.binding.1";
            const string binding2 = "recovered.binding.2";

            var filter = new TopologyRecoveryFilter
            {
                ConsumerFilter = consumer => !consumer.ConsumerTag.Contains("filtered")
            };

            var connectionRecoveryTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            await using AutorecoveringConnection conn = await CreateAutorecoveringConnectionWithTopologyRecoveryFilterAsync(filter);
            conn.RecoverySucceededAsync += (source, ea) =>
            {
                connectionRecoveryTcs.SetResult(true);
                return Task.CompletedTask;
            };
            conn.ConnectionRecoveryErrorAsync += (source, ea) =>
            {
                connectionRecoveryTcs.SetException(ea.Exception);
                return Task.CompletedTask;
            };
            conn.CallbackExceptionAsync += (source, ea) =>
            {
                connectionRecoveryTcs.SetException(ea.Exception);
                return Task.CompletedTask;
            };

            await using (IChannel ch = await conn.CreateChannelAsync(new CreateChannelOptions { PublisherConfirmationsEnabled = true, PublisherConfirmationTrackingEnabled = true }))
            {
                await ch.ExchangeDeclareAsync(exchange, "direct");
                await ch.QueueDeclareAsync(queueWithRecoveredConsumer, false, false, false);
                await ch.QueueDeclareAsync(queueWithIgnoredConsumer, false, false, false);
                await ch.QueueBindAsync(queueWithRecoveredConsumer, exchange, binding1);
                await ch.QueueBindAsync(queueWithIgnoredConsumer, exchange, binding2);
                await ch.QueuePurgeAsync(queueWithRecoveredConsumer);
                await ch.QueuePurgeAsync(queueWithIgnoredConsumer);

                var consumerRecoveryTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var consumerToRecover = new AsyncEventingBasicConsumer(ch);
                consumerToRecover.ReceivedAsync += (source, ea) =>
                {
                    consumerRecoveryTcs.SetResult(true);
                    return Task.CompletedTask;
                };
                await ch.BasicConsumeAsync(queueWithRecoveredConsumer, true, "recovered.consumer", consumerToRecover);

                var ignoredTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var consumerToIgnore = new AsyncEventingBasicConsumer(ch);
                consumerToIgnore.ReceivedAsync += (source, ea) =>
                {
                    ignoredTcs.SetResult(true);
                    return Task.CompletedTask;
                };
                await ch.BasicConsumeAsync(queueWithIgnoredConsumer, true, "filtered.consumer", consumerToIgnore);

                try
                {
                    await CloseAndWaitForRecoveryAsync(conn);
                    await WaitAsync(connectionRecoveryTcs, "recovery succeeded");

                    Assert.True(ch.IsOpen);
                    await ch.BasicPublishAsync(exchange, binding1, _encoding.GetBytes("test message"));
                    await ch.BasicPublishAsync(exchange, binding2, _encoding.GetBytes("test message"));

                    await consumerRecoveryTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.True(await consumerRecoveryTcs.Task);

                    bool sawTimeout = false;
                    try
                    {
                        await ignoredTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    catch (TimeoutException)
                    {
                        sawTimeout = true;
                    }
                    Assert.True(sawTimeout);

                    await ch.BasicConsumeAsync(queueWithIgnoredConsumer, true, "filtered.consumer", consumerToIgnore);

                    try
                    {
                        await ch.BasicConsumeAsync(queueWithRecoveredConsumer, true, "recovered.consumer", consumerToRecover);
                        Assert.Fail("Expected an exception");
                    }
                    catch (OperationInterruptedException e)
                    {
                        AssertShutdownError(e.ShutdownReason, 530); // NOT_ALLOWED - not allowed to reuse consumer tag
                    }
                }
                finally
                {
                    await ch.CloseAsync();
                }
            }

            await conn.CloseAsync();
        }

        [Fact]
        public async Task TestRecoveryWithTopologyDisabled()
        {
            string queueName = GenerateQueueName() + "-dotnet-client.test.recovery.q2";

            await using AutorecoveringConnection conn = await CreateAutorecoveringConnectionWithTopologyRecoveryDisabledAsync();
            await using (IChannel ch = await conn.CreateChannelAsync())
            {
                try
                {
                    await ch.QueueDeleteAsync(queueName);
                    await ch.QueueDeclareAsync(queue: queueName,
                        durable: false, exclusive: true, autoDelete: false, arguments: null);
                    await ch.QueueDeclareAsync(queue: queueName,
                        passive: true, durable: false, exclusive: true, autoDelete: false, arguments: null);

                    Assert.True(ch.IsOpen);
                    await CloseAndWaitForRecoveryAsync(conn);

                    Assert.True(ch.IsOpen);
                    await ch.QueueDeclareAsync(queue: queueName, passive: true, durable: false, exclusive: true, autoDelete: false, arguments: null);

                    Assert.Fail("Expected an exception");
                }
                catch (OperationInterruptedException)
                {
                    // expected
                }
                finally
                {
                    await ch.CloseAsync();
                }
            }

            await conn.CloseAsync();
        }
    }
}
