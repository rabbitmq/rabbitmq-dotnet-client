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
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Impl;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration.ConnectionRecovery
{
    public class TestConsumerRecovery : TestConnectionRecoveryBase
    {
        public TestConsumerRecovery(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task TestConsumerRecoveryWithManyConsumers()
        {
            string q = (await _channel.QueueDeclareAsync(GenerateQueueName(), false, true, false)).QueueName;
            int n = 1024;

            for (int i = 0; i < n; i++)
            {
                var cons = new AsyncEventingBasicConsumer(_channel);
                await _channel.BasicConsumeAsync(q, true, cons);
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ((AutorecoveringConnection)_conn).ConsumerTagChangeAfterRecoveryAsync += (prev, current) =>
            {
                tcs.TrySetResult(true);
                return Task.CompletedTask;
            };

            await CloseAndWaitForRecoveryAsync();
            await WaitAsync(tcs, "consumer tag change after recovery");
            Assert.True(_channel.IsOpen);
            await AssertConsumerCountAsync(q, n);
        }

        [Fact]
        public async Task TestThatCancelledConsumerDoesNotReappearOnRecovery()
        {
            string q = (await _channel.QueueDeclareAsync(GenerateQueueName(), false, true, false)).QueueName;
            int n = 1024;

            for (int i = 0; i < n; i++)
            {
                var cons = new AsyncEventingBasicConsumer(_channel);
                string tag = await _channel.BasicConsumeAsync(q, true, cons);
                await _channel.BasicCancelAsync(tag);
            }
            await CloseAndWaitForRecoveryAsync();
            Assert.True(_channel.IsOpen);
            await AssertConsumerCountAsync(q, 0);
        }

        [Fact]
        public async Task TestConsumerShutdownReasonIsClearedAfterRecovery_GH2006()
        {
            // The delivery barrier below holds only at a concurrency of one, so pin the fixture's
            // default. #2006, docs/internal/connection-shutdown-and-cancellation.md.
            Assert.Equal((ushort)1, _consumerDispatchConcurrency);

            string q = (await _channel.QueueDeclareAsync(GenerateQueueName(), false, true, false)).QueueName;
            var consumer = new AsyncEventingBasicConsumer(_channel);

            // The consume-ok runs on the dispatcher, so it need not have happened when
            // BasicConsumeAsync returns; observe the initial state from the event, not the call.
            var firstRegistrationTcs =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            consumer.RegisteredAsync += (sender, ea) =>
            {
                firstRegistrationTcs.TrySetResult(true);
                return Task.CompletedTask;
            };

            // Asserts the transition, not "null before, null after": without this a change that
            // stopped delivering the shutdown would leave both assertions passing.
            ShutdownEventArgs shutdownReasonSeen = null;
            consumer.ShutdownAsync += (sender, ea) =>
            {
                shutdownReasonSeen = consumer.ShutdownReason;
                return Task.CompletedTask;
            };

            var deliveredAfterRecoveryTcs =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            consumer.ReceivedAsync += (sender, ea) =>
            {
                deliveredAfterRecoveryTcs.TrySetResult(true);
                return Task.CompletedTask;
            };

            await _channel.BasicConsumeAsync(q, true, consumer);
            await WaitAsync(firstRegistrationTcs, "consumer registered");

            Assert.True(consumer.IsRunning);
            Assert.Null(consumer.ShutdownReason);

            await CloseAndWaitForRecoveryAsync();

            Assert.True(shutdownReasonSeen is not null,
                "the consumer never observed a shutdown reason, so the connection drop did not " +
                "reach this consumer and the clear-on-recovery assertions below prove nothing");

            // A delivery, not the registration event: the registration count can be satisfied by a
            // recovery that re-registered and then failed, leaving the state read mid-attempt.
            await _channel.BasicPublishAsync(string.Empty, q, _encoding.GetBytes("after recovery"));
            await WaitAsync(deliveredAfterRecoveryTcs, "delivery after recovery");

            Assert.True(_channel.IsOpen);
            Assert.True(consumer.IsRunning);
            Assert.Null(consumer.ShutdownReason);
        }

        [Fact]
        public async Task TestBasicConsumeRejectsATagInUseOnTheChannel_GH2080()
        {
            string q = (await _channel.QueueDeclareAsync(GenerateQueueName(), false, true, false)).QueueName;
            string tag = GenerateShortUuid();
            var consumer = new AsyncEventingBasicConsumer(_channel);
            var deliveredTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            consumer.ReceivedAsync += (sender, ea) =>
            {
                deliveredTcs.TrySetResult(true);
                return Task.CompletedTask;
            };
            await _channel.BasicConsumeAsync(q, true, tag, consumer);

            InvalidOperationException ex = await AssertNoConnectionShutdownAsync(_conn, _channel, () =>
                Assert.ThrowsAsync<InvalidOperationException>(() =>
                    _channel.BasicConsumeAsync(q, true, tag, new AsyncEventingBasicConsumer(_channel))));
            Assert.Contains(tag, ex.Message);

            await _channel.BasicPublishAsync(string.Empty, q, _encoding.GetBytes("after duplicate"));
            await WaitAsync(deliveredTcs, "delivery to the first consumer");
            await AssertConsumerCountAsync(q, 1);
        }

        [Fact]
        public async Task TestBasicConsumeFromAChannelShutdownHandlerThrowsAlreadyClosed_GH2080()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = false;
            await using IConnection conn = await cf.CreateConnectionAsync();
            await using IChannel ch = await conn.CreateChannelAsync();
            string q = (await ch.QueueDeclareAsync(GenerateQueueName(), false, true, false)).QueueName;
            string tag = GenerateShortUuid();
            await ch.BasicConsumeAsync(q, true, tag, new AsyncEventingBasicConsumer(ch));

            // Shutdown handlers run before the dispatcher forgets the channel's tags.
            var resubscribeTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            ch.ChannelShutdownAsync += async (sender, ea) =>
            {
                try
                {
                    await ch.BasicConsumeAsync(q, true, tag, new AsyncEventingBasicConsumer(ch));
                    resubscribeTcs.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    resubscribeTcs.TrySetResult(ex);
                }
            };

            await Assert.ThrowsAsync<OperationInterruptedException>(() => ch.QueueDeclarePassiveAsync(GenerateQueueName()));
            await resubscribeTcs.Task.WaitAsync(WaitSpan);
            Assert.IsType<AlreadyClosedException>(await resubscribeTcs.Task);
        }

        [Fact]
        public async Task TestBasicConsumeWithARecoveredTagIsRejectedAndTheConsumerSurvives_GH2080()
        {
            string q = (await _channel.QueueDeclareAsync(GenerateQueueName(), false, true, false)).QueueName;
            string tag = GenerateShortUuid();
            var consumer = new AsyncEventingBasicConsumer(_channel);
            var deliveredTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            consumer.ReceivedAsync += (sender, ea) =>
            {
                deliveredTcs.TrySetResult(true);
                return Task.CompletedTask;
            };
            await _channel.BasicConsumeAsync(q, true, tag, consumer);

            await CloseAndWaitForRecoveryAsync();

            // An application re-subscribing with its stored tag after a connection loss.
            await AssertNoConnectionShutdownAsync(_conn, _channel, () =>
                Assert.ThrowsAsync<InvalidOperationException>(() => _channel.BasicConsumeAsync(q, true, tag, consumer)));

            await _channel.BasicPublishAsync(string.Empty, q, _encoding.GetBytes("after recovery"));
            await WaitAsync(deliveredTcs, "delivery to the recovered consumer");
            await AssertConsumerCountAsync(q, 1);
        }

        [Fact]
        public async Task TestBrokerClosesTheConnectionWith530ForAReusedTagAndRecoveryRestoresTheConsumer_GH2080()
        {
            string q = (await _channel.QueueDeclareAsync(GenerateQueueName(), false, true, false)).QueueName;
            string tag = GenerateShortUuid();
            var consumer = new AsyncEventingBasicConsumer(_channel);
            var deliveredTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            consumer.ReceivedAsync += (sender, ea) =>
            {
                deliveredTcs.TrySetResult(true);
                return Task.CompletedTask;
            };
            await _channel.BasicConsumeAsync(q, true, tag, consumer);

            // Test-only bypass of the local check, through internals no application can reach. The broker
            // still has the consumer and the channel does not, so the duplicate below reaches the broker.
            ((AutorecoveringChannel)_channel).ConsumerDispatcher.GetAndRemoveConsumer(tag);

            TaskCompletionSource<bool> recoveredTcs = PrepareForRecovery(_conn);
            OperationInterruptedException ex = await Assert.ThrowsAnyAsync<OperationInterruptedException>(() =>
                _channel.BasicConsumeAsync(q, true, tag, new AsyncEventingBasicConsumer(_channel)));
            AssertShutdownError(ex.ShutdownReason, 530);
            await WaitAsync(recoveredTcs, "connection recovery");

            await _channel.BasicPublishAsync(string.Empty, q, _encoding.GetBytes("after recovery"));
            await WaitAsync(deliveredTcs, "delivery to the recovered consumer");
            await AssertConsumerCountAsync(q, 1);
        }

        [Fact]
        public async Task TestRecoverySkipsATagTheApplicationRegisteredFirst_GH2080()
        {
            Exception recoveryFailure = null;
            var exceptionHandler = new TopologyRecoveryExceptionHandler
            {
                ConsumerRecoveryExceptionCondition = (c, ex) => true,
                ConsumerRecoveryExceptionHandlerAsync = (c, ex, connection) =>
                {
                    recoveryFailure = ex;
                    return Task.CompletedTask;
                }
            };

            AutorecoveringConnection conn = await CreateAutorecoveringConnectionWithTopologyRecoveryExceptionHandlerAsync(exceptionHandler);
            IChannel ch = await conn.CreateChannelAsync();
            try
            {
                string q = (await ch.QueueDeclareAsync(GenerateQueueName(), false, true, false)).QueueName;
                string tag = GenerateShortUuid();
                var consumer = new AsyncEventingBasicConsumer(ch);
                var deliveredTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                consumer.ReceivedAsync += (sender, ea) =>
                {
                    deliveredTcs.TrySetResult(true);
                    return Task.CompletedTask;
                };
                await ch.BasicConsumeAsync(q, true, tag, consumer);

                // Recovery runs this with _recordedEntitiesSemaphore released and the new inner channel
                // installed, which is the window in which an application consume reaches it first.
                // A handler's exception goes to CallbackExceptionAsync, so record the consume's result.
                int subscribedDuringRecovery = 0;
                string resubscribedTag = null;
                conn.RecoveringConsumerAsync += async (sender, ea) =>
                {
                    if (ea.ConsumerTag == tag && Interlocked.Exchange(ref subscribedDuringRecovery, 1) == 0)
                    {
                        resubscribedTag = await ch.BasicConsumeAsync(q, true, tag, consumer);
                    }
                };

                await CloseAndWaitForRecoveryAsync(conn);

                Assert.Equal(1, subscribedDuringRecovery);
                Assert.Equal(tag, resubscribedTag);
                Assert.Null(recoveryFailure);

                await ch.BasicPublishAsync(string.Empty, q, _encoding.GetBytes("after recovery"));
                await WaitAsync(deliveredTcs, "delivery after recovery");
                Assert.True(conn.IsOpen);
                Assert.True(ch.IsOpen);
                await AssertConsumerCountAsync(ch, q, 1);
            }
            finally
            {
                await ch.CloseAsync();
                await conn.CloseAsync();
                await ch.DisposeAsync();
                await conn.DisposeAsync();
            }
        }

        [Fact]
        public async Task TestRecoveryDoesNotRaiseRecoveringConsumerForATagAlreadyLive_GH2080()
        {
            Exception recoveryFailure = null;
            var exceptionHandler = new TopologyRecoveryExceptionHandler
            {
                ConsumerRecoveryExceptionCondition = (c, ex) => true,
                ConsumerRecoveryExceptionHandlerAsync = (c, ex, connection) =>
                {
                    recoveryFailure = ex;
                    return Task.CompletedTask;
                }
            };

            AutorecoveringConnection conn = await CreateAutorecoveringConnectionWithTopologyRecoveryExceptionHandlerAsync(exceptionHandler);
            IChannel ch = await conn.CreateChannelAsync();
            try
            {
                var queuesByTag = new Dictionary<string, string>();
                var consumersByTag = new Dictionary<string, AsyncEventingBasicConsumer>();
                for (int i = 0; i < 2; i++)
                {
                    string q = (await ch.QueueDeclareAsync(GenerateQueueName(), false, true, false)).QueueName;
                    string tag = GenerateShortUuid();
                    var consumer = new AsyncEventingBasicConsumer(ch);
                    await ch.BasicConsumeAsync(q, true, tag, consumer);
                    queuesByTag[tag] = q;
                    consumersByTag[tag] = consumer;
                }

                // Recovery order over the recorded consumers is unspecified, so the first handler to run
                // subscribes the other tag, which is then live before recovery reaches it.
                var raisedFor = new List<string>();
                string subscribedTag = null;
                conn.RecoveringConsumerAsync += async (sender, ea) =>
                {
                    raisedFor.Add(ea.ConsumerTag);
                    if (subscribedTag is null)
                    {
                        foreach (string tag in queuesByTag.Keys)
                        {
                            if (tag != ea.ConsumerTag)
                            {
                                subscribedTag = tag;
                            }
                        }
                        await ch.BasicConsumeAsync(queuesByTag[subscribedTag], true, subscribedTag, consumersByTag[subscribedTag]);
                    }
                };

                await CloseAndWaitForRecoveryAsync(conn);

                Assert.Null(recoveryFailure);
                Assert.NotNull(subscribedTag);
                Assert.DoesNotContain(subscribedTag, raisedFor);
                Assert.Single(raisedFor);
                Assert.True(conn.IsOpen);
                Assert.True(ch.IsOpen);
                foreach (string q in queuesByTag.Values)
                {
                    await AssertConsumerCountAsync(ch, q, 1);
                }
            }
            finally
            {
                await ch.CloseAsync();
                await conn.CloseAsync();
                await ch.DisposeAsync();
                await conn.DisposeAsync();
            }
        }

        [Fact]
        public async Task TestRecoveryDoesNotReportATagTheApplicationIsStillSubscribing_GH2080()
        {
            Exception recoveryFailure = null;
            var exceptionHandler = new TopologyRecoveryExceptionHandler
            {
                ConsumerRecoveryExceptionCondition = (c, ex) => true,
                ConsumerRecoveryExceptionHandlerAsync = (c, ex, connection) =>
                {
                    recoveryFailure = ex;
                    return Task.CompletedTask;
                }
            };

            AutorecoveringConnection conn = await CreateAutorecoveringConnectionWithTopologyRecoveryExceptionHandlerAsync(exceptionHandler);
            IChannel ch = await conn.CreateChannelAsync();
            try
            {
                string q = (await ch.QueueDeclareAsync(GenerateQueueName(), false, true, false)).QueueName;
                string tag = GenerateShortUuid();
                var consumer = new AsyncEventingBasicConsumer(ch);
                var deliveredTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                consumer.ReceivedAsync += (sender, ea) =>
                {
                    deliveredTcs.TrySetResult(true);
                    return Task.CompletedTask;
                };
                await ch.BasicConsumeAsync(q, true, tag, consumer);

                // Not awaited: the consume holds _rpcSemaphore waiting for consume-ok while recovery's post-check
                // runs, so recovery's own consume queues behind it and then fails the local check.
                Task<string> applicationConsume = null;
                conn.RecoveringConsumerAsync += (sender, ea) =>
                {
                    if (ea.ConsumerTag == tag && applicationConsume is null)
                    {
                        applicationConsume = ch.BasicConsumeAsync(q, true, tag, consumer);
                    }
                    return Task.CompletedTask;
                };

                await CloseAndWaitForRecoveryAsync(conn);

                Assert.NotNull(applicationConsume);
                Assert.Equal(tag, await applicationConsume);
                Assert.Null(recoveryFailure);

                await ch.BasicPublishAsync(string.Empty, q, _encoding.GetBytes("after recovery"));
                await WaitAsync(deliveredTcs, "delivery after recovery");
                Assert.True(conn.IsOpen);
                Assert.True(ch.IsOpen);
                await AssertConsumerCountAsync(ch, q, 1);
            }
            finally
            {
                await ch.CloseAsync();
                await conn.CloseAsync();
                await ch.DisposeAsync();
                await conn.DisposeAsync();
            }
        }
    }
}
