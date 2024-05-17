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
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestConnectionTopologyRecovery : TestConnectionRecoveryBase
    {
        public TestConnectionTopologyRecovery(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestRecoverTopologyOnDisposedChannel()
        {
            ExchangeName x = GenerateExchangeName();
            QueueName q = GenerateQueueName();
            var rk = new RoutingKey("routing-key");

            using (IChannel ch = await _conn.CreateChannelAsync())
            {
                await ch.ExchangeDeclareAsync(exchange: x, type: ExchangeType.Fanout);
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

            await _channel.BasicPublishAsync(ExchangeName.Empty, (RoutingKey)q, _messageBody);
            await WaitAsync(tcs, "received event");

            await _channel.QueueUnbindAsync(q, x, rk);
            await _channel.ExchangeDeleteAsync(x);
            await _channel.QueueDeleteAsync(q);
        }

        [Fact]
        public async Task TestTopologyRecoveryQueueFilter()
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var filter = new TopologyRecoveryFilter
            {
                QueueFilter = queue => !queue.Name.Contains("filtered")
            };

            AutorecoveringConnection conn = await CreateAutorecoveringConnectionWithTopologyRecoveryFilterAsync(filter);
            conn.RecoverySucceeded += (source, ea) => tcs.SetResult(true);
            IChannel ch = await conn.CreateChannelAsync();

            var queueToRecover = new QueueName("recovered.queue");
            var queueToIgnore = new QueueName("filtered.queue");
            await ch.QueueDeclareAsync(queueToRecover, false, false, false);
            await ch.QueueDeclareAsync(queueToIgnore, false, false, false);

            await _channel.QueueDeleteAsync(queueToRecover);
            await _channel.QueueDeleteAsync(queueToIgnore);

            await CloseAndWaitForRecoveryAsync(conn);
            await WaitAsync(tcs, "recovery succeeded");

            Assert.True(ch.IsOpen);
            await AssertQueueRecoveryAsync(ch, queueToRecover, false);

            try
            {
                await AssertQueueRecoveryAsync(ch, queueToIgnore, false);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException e)
            {
                IntegrationFixture.AssertShutdownError(e.ShutdownReason, 404);
            }
            finally
            {
                await ch.CloseAsync();
                await conn.CloseAsync();
                ch.Dispose();
                conn.Dispose();
            }
        }

        [Fact]
        public async Task TestTopologyRecoveryExchangeFilter()
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var filter = new TopologyRecoveryFilter
            {
                ExchangeFilter = exchange => exchange.Type == "topic" && !exchange.Name.Contains("filtered")
            };

            AutorecoveringConnection conn = await CreateAutorecoveringConnectionWithTopologyRecoveryFilterAsync(filter);
            conn.RecoverySucceeded += (source, ea) => tcs.SetResult(true);
            IChannel ch = await conn.CreateChannelAsync();
            try
            {
                var exchangeToRecover = new ExchangeName("recovered.exchange");
                var exchangeToIgnore = new ExchangeName("filtered.exchange");
                await ch.ExchangeDeclareAsync(exchangeToRecover, ExchangeType.Topic, false, true);
                await ch.ExchangeDeclareAsync(exchangeToIgnore, ExchangeType.Direct, false, true);

                await _channel.ExchangeDeleteAsync(exchangeToRecover);
                await _channel.ExchangeDeleteAsync(exchangeToIgnore);

                await CloseAndWaitForRecoveryAsync(conn);
                await WaitAsync(tcs, "recovery succeeded");

                Assert.True(ch.IsOpen);
                await AssertExchangeRecoveryAsync(ch, exchangeToRecover);
                await AssertExchangeRecoveryAsync(ch, exchangeToIgnore);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException e)
            {
                AssertShutdownError(e.ShutdownReason, 404);
            }
            finally
            {
                await ch.CloseAsync();
                await conn.CloseAsync();
                ch.Dispose();
                conn.Dispose();
            }
        }

        [Fact]
        public async Task TestTopologyRecoveryBindingFilter()
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var filter = new TopologyRecoveryFilter
            {
                BindingFilter = binding => !binding.RoutingKey.Contains("filtered")
            };

            AutorecoveringConnection conn = await CreateAutorecoveringConnectionWithTopologyRecoveryFilterAsync(filter);
            conn.RecoverySucceeded += (source, ea) => tcs.SetResult(true);
            IChannel ch = await conn.CreateChannelAsync();
            try
            {
                var exchange = new ExchangeName("topology.recovery.exchange");
                var queueWithRecoveredBinding = new QueueName("topology.recovery.queue.1");
                var queueWithIgnoredBinding = new QueueName("topology.recovery.queue.2");
                var bindingToRecover = new RoutingKey("recovered.binding");
                var bindingToIgnore = new RoutingKey("filtered.binding");

                await ch.ExchangeDeclareAsync(exchange, ExchangeType.Direct);
                await ch.QueueDeclareAsync(queueWithRecoveredBinding, false, false, false);
                await ch.QueueDeclareAsync(queueWithIgnoredBinding, false, false, false);
                await ch.QueueBindAsync(queueWithRecoveredBinding, exchange, bindingToRecover);
                await ch.QueueBindAsync(queueWithIgnoredBinding, exchange, bindingToIgnore);
                await ch.QueuePurgeAsync(queueWithRecoveredBinding);
                await ch.QueuePurgeAsync(queueWithIgnoredBinding);

                await _channel.QueueUnbindAsync(queueWithRecoveredBinding, exchange, bindingToRecover);
                await _channel.QueueUnbindAsync(queueWithIgnoredBinding, exchange, bindingToIgnore);

                await CloseAndWaitForRecoveryAsync(conn);
                await WaitAsync(tcs, "recovery succeeded");

                Assert.True(ch.IsOpen);
                Assert.True(await SendAndConsumeMessageAsync(_conn, queueWithRecoveredBinding, exchange, bindingToRecover));
                Assert.False(await SendAndConsumeMessageAsync(_conn, queueWithIgnoredBinding, exchange, bindingToIgnore));
            }
            finally
            {
                await ch.CloseAsync();
                await conn.CloseAsync();
                ch.Dispose();
                conn.Dispose();
            }
        }

        [Fact]
        public async Task TestTopologyRecoveryDefaultFilterRecoversAllEntities()
        {
            var connectionRecoveryTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var filter = new TopologyRecoveryFilter();
            AutorecoveringConnection conn = await CreateAutorecoveringConnectionWithTopologyRecoveryFilterAsync(filter);
            conn.RecoverySucceeded += (source, ea) => connectionRecoveryTcs.SetResult(true);
            IChannel ch = await conn.CreateChannelAsync();
            try
            {
                await ch.ConfirmSelectAsync();

                var exchange = new ExchangeName("topology.recovery.exchange");
                var queue1 = new QueueName("topology.recovery.queue.1");
                var queue2 = new QueueName("topology.recovery.queue.2");
                var binding1 = new RoutingKey("recovered.binding");
                var binding2 = new RoutingKey("filtered.binding");

                await ch.ExchangeDeclareAsync(exchange, ExchangeType.Direct);
                await ch.QueueDeclareAsync(queue1, false, false, false);
                await ch.QueueDeclareAsync(queue2, false, false, false);
                await ch.QueueBindAsync(queue1, exchange, binding1);
                await ch.QueueBindAsync(queue2, exchange, binding2);
                await ch.QueuePurgeAsync(queue1);
                await ch.QueuePurgeAsync(queue2);

                var consumerReceivedTcs1 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var consumer1 = new EventingBasicConsumer(ch);
                consumer1.Received += (source, ea) => consumerReceivedTcs1.SetResult(true);
                await ch.BasicConsumeAsync(queue1, true, (ConsumerTag)"recovered.consumer", consumer1);

                var consumerReceivedTcs2 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var consumer2 = new EventingBasicConsumer(ch);
                consumer2.Received += (source, ea) => consumerReceivedTcs2.SetResult(true);
                await ch.BasicConsumeAsync(queue2, true, (ConsumerTag)"filtered.consumer", consumer2);

                await _channel.ExchangeDeleteAsync(exchange);
                await _channel.QueueDeleteAsync(queue1);
                await _channel.QueueDeleteAsync(queue2);

                await CloseAndWaitForRecoveryAsync(conn);
                await WaitAsync(connectionRecoveryTcs, "recovery succeeded");

                Assert.True(ch.IsOpen);
                await AssertExchangeRecoveryAsync(ch, exchange);
                await ch.QueueDeclarePassiveAsync(queue1);
                await ch.QueueDeclarePassiveAsync(queue2);

                await ch.BasicPublishAsync(exchange, binding1, _encoding.GetBytes("test message"), mandatory: true);
                // await ch.WaitForConfirmsOrDieAsync();

                await ch.BasicPublishAsync(exchange, binding2, _encoding.GetBytes("test message"), mandatory: true);
                // await ch.WaitForConfirmsOrDieAsync();

                await consumerReceivedTcs1.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await consumerReceivedTcs2.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.True(consumerReceivedTcs1.Task.IsCompletedSuccessfully());
                Assert.True(consumerReceivedTcs2.Task.IsCompletedSuccessfully());
            }
            finally
            {
                await ch.CloseAsync();
                await conn.CloseAsync();
                ch.Dispose();
                conn.Dispose();
            }
        }

        [Fact]
        public async Task TestTopologyRecoveryQueueExceptionHandler()
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var changedQueueArguments = new Dictionary<string, object>
            {
                { Headers.XMaxPriority, 20 }
            };

            var exceptionHandler = new TopologyRecoveryExceptionHandler
            {
                QueueRecoveryExceptionCondition = (rq, ex) =>
                {
                    return ((string)rq.Name).Contains("exception")
                        && ex is OperationInterruptedException operationInterruptedException
                        && operationInterruptedException.ShutdownReason.ReplyCode == Constants.PreconditionFailed;
                },
                QueueRecoveryExceptionHandlerAsync = async (rq, ex, connection) =>
                {
                    using (IChannel channel = await connection.CreateChannelAsync())
                    {
                        await channel.QueueDeclareAsync(rq.Name, false, false, false,
                            noWait: false, arguments: changedQueueArguments);
                        await channel.CloseAsync();
                    }
                }
            };

            AutorecoveringConnection conn = await CreateAutorecoveringConnectionWithTopologyRecoveryExceptionHandlerAsync(exceptionHandler);
            conn.RecoverySucceeded += (source, ea) => tcs.SetResult(true);
            IChannel ch = await conn.CreateChannelAsync();

            var queueToRecoverWithException = new QueueName("recovery.exception.queue");
            var queueToRecoverSuccessfully = new QueueName("successfully.recovered.queue");
            await ch.QueueDeclareAsync(queueToRecoverWithException, false, false, false);
            await ch.QueueDeclareAsync(queueToRecoverSuccessfully, false, false, false);

            await _channel.QueueDeleteAsync(queueToRecoverSuccessfully);
            await _channel.QueueDeleteAsync(queueToRecoverWithException);
            await _channel.QueueDeclareAsync(queueToRecoverWithException, false, false, false,
                noWait: false, arguments: changedQueueArguments);

            try
            {
                await CloseAndWaitForRecoveryAsync(conn);
                await WaitAsync(tcs, "recovery succeded");

                Assert.True(ch.IsOpen);
                await AssertQueueRecoveryAsync(ch, queueToRecoverSuccessfully, false);
                await AssertQueueRecoveryAsync(ch, queueToRecoverWithException, false, changedQueueArguments);
            }
            finally
            {
                await _channel.QueueDeleteAsync(queueToRecoverWithException);
                await ch.CloseAsync();
                await conn.CloseAsync();
                ch.Dispose();
                conn.Dispose();
            }
        }

        [Fact]
        public async Task TestTopologyRecoveryExchangeExceptionHandler()
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var exceptionHandler = new TopologyRecoveryExceptionHandler
            {
                ExchangeRecoveryExceptionCondition = (re, ex) =>
                {
                    return re.Name.Contains("exception")
                        && ex is OperationInterruptedException operationInterruptedException
                        && operationInterruptedException.ShutdownReason.ReplyCode == Constants.PreconditionFailed;
                },
                ExchangeRecoveryExceptionHandlerAsync = async (re, ex, connection) =>
                {
                    using (IChannel channel = await connection.CreateChannelAsync())
                    {
                        await channel.ExchangeDeclareAsync(re.Name, ExchangeType.Topic, false, false);
                        await channel.CloseAsync();
                    }
                }
            };

            AutorecoveringConnection conn = await CreateAutorecoveringConnectionWithTopologyRecoveryExceptionHandlerAsync(exceptionHandler);
            conn.RecoverySucceeded += (source, ea) => tcs.SetResult(true);

            var exchangeToRecoverWithException = new ExchangeName("recovery.exception.exchange");
            var exchangeToRecoverSuccessfully = new ExchangeName("successfully.recovered.exchange");
            IChannel ch = await conn.CreateChannelAsync();
            await ch.ExchangeDeclareAsync(exchangeToRecoverWithException, ExchangeType.Direct, false, false);
            await ch.ExchangeDeclareAsync(exchangeToRecoverSuccessfully, ExchangeType.Direct, false, false);

            await _channel.ExchangeDeleteAsync(exchangeToRecoverSuccessfully);
            await _channel.ExchangeDeleteAsync(exchangeToRecoverWithException);
            await _channel.ExchangeDeclareAsync(exchangeToRecoverWithException, ExchangeType.Topic, false, false);

            try
            {
                await CloseAndWaitForRecoveryAsync(conn);
                await WaitAsync(tcs, "recovery succeeded");

                Assert.True(_channel.IsOpen);
                await AssertExchangeRecoveryAsync(_channel, exchangeToRecoverSuccessfully);
                await AssertExchangeRecoveryAsync(_channel, exchangeToRecoverWithException);
            }
            finally
            {
                await _channel.ExchangeDeleteAsync(exchangeToRecoverWithException);

                await ch.CloseAsync();
                await conn.CloseAsync();
                ch.Dispose();
                conn.Dispose();
            }
        }

        [Fact]
        public async Task TestTopologyRecoveryBindingExceptionHandler()
        {
            var connectionRecoveryTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var exchange = new ExchangeName("topology.recovery.exchange");
            var queueWithExceptionBinding = new QueueName("recovery.exception.queue");
            var bindingToRecoverWithException = new RoutingKey("recovery.exception.binding");

            var exceptionHandler = new TopologyRecoveryExceptionHandler
            {
                BindingRecoveryExceptionCondition = (b, ex) =>
                {
                    return b.RoutingKey.Contains("exception")
                        && ex is OperationInterruptedException operationInterruptedException
                        && operationInterruptedException.ShutdownReason.ReplyCode == Constants.NotFound;
                },
                BindingRecoveryExceptionHandlerAsync = async (b, ex, connection) =>
                {
                    using (IChannel channel = await connection.CreateChannelAsync())
                    {
                        await channel.QueueDeclareAsync(queueWithExceptionBinding, false, false, false);
                        await channel.QueueBindAsync(queueWithExceptionBinding, exchange, bindingToRecoverWithException);
                        await channel.CloseAsync();
                    }
                }
            };

            AutorecoveringConnection conn = await CreateAutorecoveringConnectionWithTopologyRecoveryExceptionHandlerAsync(exceptionHandler);
            conn.RecoverySucceeded += (source, ea) => connectionRecoveryTcs.SetResult(true);
            IChannel ch = await conn.CreateChannelAsync();

            var queueWithRecoveredBinding = new QueueName("successfully.recovered.queue");
            var bindingToRecoverSuccessfully = new RoutingKey("successfully.recovered.binding");

            await _channel.QueueDeclareAsync(queueWithExceptionBinding, false, false, false);

            await ch.ExchangeDeclareAsync(exchange, ExchangeType.Direct);
            await ch.QueueDeclareAsync(queueWithRecoveredBinding, false, false, false);
            await ch.QueueBindAsync(queueWithRecoveredBinding, exchange, bindingToRecoverSuccessfully);
            await ch.QueueBindAsync(queueWithExceptionBinding, exchange, bindingToRecoverWithException);
            await ch.QueuePurgeAsync(queueWithRecoveredBinding);
            await ch.QueuePurgeAsync(queueWithExceptionBinding);

            await _channel.QueueUnbindAsync(queueWithRecoveredBinding, exchange, bindingToRecoverSuccessfully);
            await _channel.QueueUnbindAsync(queueWithExceptionBinding, exchange, bindingToRecoverWithException);
            await _channel.QueueDeleteAsync(queueWithExceptionBinding);

            await CloseAndWaitForRecoveryAsync(conn);
            await WaitAsync(connectionRecoveryTcs, "recovery succeeded");

            Assert.True(ch.IsOpen);
            Assert.True(await SendAndConsumeMessageAsync(conn, queueWithRecoveredBinding, exchange, bindingToRecoverSuccessfully));
            Assert.True(await SendAndConsumeMessageAsync(conn, queueWithExceptionBinding, exchange, bindingToRecoverWithException));

            await ch.CloseAsync();
            await conn.CloseAsync();
            ch.Dispose();
            conn.Dispose();
        }

        [Fact]
        public async Task TestTopologyRecoveryConsumerExceptionHandler()
        {
            var connectionRecoveryTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var queueWithExceptionConsumer = new QueueName("recovery.exception.queue");

            var exceptionHandler = new TopologyRecoveryExceptionHandler
            {
                ConsumerRecoveryExceptionCondition = (c, ex) =>
                {
                    return c.ConsumerTag.Contains("exception")
                        && ex is OperationInterruptedException operationInterruptedException
                        && operationInterruptedException.ShutdownReason.ReplyCode == Constants.NotFound;
                },
                ConsumerRecoveryExceptionHandlerAsync = async (c, ex, connection) =>
                {
                    using (IChannel channel = await connection.CreateChannelAsync())
                    {
                        await channel.QueueDeclareAsync(queueWithExceptionConsumer, false, false, false);
                        await channel.CloseAsync();
                    }

                    // So topology recovery runs again. This time he missing queue should exist, making
                    // it possible to recover the consumer successfully.
                    throw ex;
                }
            };

            AutorecoveringConnection conn = await CreateAutorecoveringConnectionWithTopologyRecoveryExceptionHandlerAsync(exceptionHandler);
            conn.RecoverySucceeded += (source, ea) => connectionRecoveryTcs.SetResult(true);
            IChannel ch = await conn.CreateChannelAsync();
            try
            {
                await ch.ConfirmSelectAsync();

                await _channel.QueueDeclareAsync(queueWithExceptionConsumer, false, false, false);
                await _channel.QueuePurgeAsync(queueWithExceptionConsumer);

                var recoveredConsumerReceivedTcs = new ManualResetEventSlim(false);
                var consumerToRecover = new EventingBasicConsumer(ch);
                var consumerTag = new ConsumerTag("exception.consumer");

                consumerToRecover.Received += (source, ea) => recoveredConsumerReceivedTcs.Set();
                await ch.BasicConsumeAsync(queueWithExceptionConsumer, true, consumerTag, consumerToRecover);

                await _channel.QueueDeleteAsync(queueWithExceptionConsumer);

                await CloseAndWaitForShutdownAsync(conn);
                await WaitAsync(connectionRecoveryTcs, TimeSpan.FromSeconds(20), "recovery succeeded");

                Assert.True(ch.IsOpen);

                await ch.BasicPublishAsync(ExchangeName.Empty, (RoutingKey)queueWithExceptionConsumer, _encoding.GetBytes("test message"));

                Assert.True(recoveredConsumerReceivedTcs.Wait(TimeSpan.FromSeconds(5)));

                await ch.BasicConsumeAsync(queueWithExceptionConsumer, true, consumerTag, consumerToRecover);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException e)
            {
                AssertShutdownError(e.ShutdownReason, 530); // NOT_ALLOWED - not allowed to reuse consumer tag
            }
            finally
            {
                await ch.CloseAsync();
                await conn.CloseAsync();
                ch.Dispose();
                conn.Dispose();
            }
        }
    }
}
