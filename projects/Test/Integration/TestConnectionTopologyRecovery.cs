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

            var cons = new AsyncEventingBasicConsumer(_channel);
            await _channel.BasicConsumeAsync(q, true, cons);
            await AssertConsumerCountAsync(_channel, q, 1);

            await CloseAndWaitForRecoveryAsync();
            await AssertConsumerCountAsync(_channel, q, 1);

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            cons.Received += (s, args) =>
            {
                tcs.SetResult(true);
                return Task.CompletedTask;
            };

            await _channel.BasicPublishAsync("", q, _messageBody);
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
            conn.RecoverySucceededAsync += (source, ea) =>
            {
                tcs.SetResult(true);
                return Task.CompletedTask;
            };
            IChannel ch = await conn.CreateChannelAsync();

            string queueToRecover = "recovered.queue";
            string queueToIgnore = "filtered.queue";
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
            conn.RecoverySucceededAsync += (source, ea) =>
            {
                tcs.SetResult(true);
                return Task.CompletedTask;
            };
            IChannel ch = await conn.CreateChannelAsync();
            try
            {
                string exchangeToRecover = "recovered.exchange";
                string exchangeToIgnore = "filtered.exchange";
                await ch.ExchangeDeclareAsync(exchangeToRecover, "topic", false, true);
                await ch.ExchangeDeclareAsync(exchangeToIgnore, "direct", false, true);

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
            conn.RecoverySucceededAsync += (source, ea) =>
            {
                tcs.SetResult(true);
                return Task.CompletedTask;
            };

            IChannel ch = await conn.CreateChannelAsync();

            try
            {
                string exchange = "topology.recovery.exchange";
                string queueWithRecoveredBinding = "topology.recovery.queue.1";
                string queueWithIgnoredBinding = "topology.recovery.queue.2";
                string bindingToRecover = "recovered.binding";
                string bindingToIgnore = "filtered.binding";

                await ch.ExchangeDeclareAsync(exchange, "direct");
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
            const string exchange = "topology.recovery.exchange";
            const string queue1 = "topology.recovery.queue.1";
            const string queue2 = "topology.recovery.queue.2";
            const string binding1 = "recovered.binding";
            const string binding2 = "filtered.binding";

            var connectionRecoveryTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var filter = new TopologyRecoveryFilter();
            AutorecoveringConnection conn = await CreateAutorecoveringConnectionWithTopologyRecoveryFilterAsync(filter);
            conn.RecoverySucceededAsync += (source, ea) =>
            {
                connectionRecoveryTcs.SetResult(true);
                return Task.CompletedTask;
            };
            conn.ConnectionRecoveryError += (source, ea) =>
            {
                connectionRecoveryTcs.SetException(ea.Exception);
                return Task.CompletedTask;
            };
            conn.CallbackExceptionAsync += (source, ea) =>
            {
                connectionRecoveryTcs.SetException(ea.Exception);
                return Task.CompletedTask;
            };

            IChannel ch = await conn.CreateChannelAsync();
            try
            {
                await ch.ConfirmSelectAsync();

                await ch.ExchangeDeclareAsync(exchange, "direct");
                await ch.QueueDeclareAsync(queue1, false, false, false);
                await ch.QueueDeclareAsync(queue2, false, false, false);
                await ch.QueueBindAsync(queue1, exchange, binding1);
                await ch.QueueBindAsync(queue2, exchange, binding2);
                await ch.QueuePurgeAsync(queue1);
                await ch.QueuePurgeAsync(queue2);

                var consumerReceivedTcs1 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var consumer1 = new AsyncEventingBasicConsumer(ch);
                consumer1.Received += (source, ea) =>
                {
                    consumerReceivedTcs1.SetResult(true);
                    return Task.CompletedTask;
                };
                await ch.BasicConsumeAsync(queue1, true, "recovered.consumer", consumer1);

                var consumerReceivedTcs2 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var consumer2 = new AsyncEventingBasicConsumer(ch);
                consumer2.Received += (source, ea) =>
                {
                    consumerReceivedTcs2.SetResult(true);
                    return Task.CompletedTask;
                };
                await ch.BasicConsumeAsync(queue2, true, "filtered.consumer", consumer2);

                await _channel.ExchangeDeleteAsync(exchange);
                await _channel.QueueDeleteAsync(queue1);
                await _channel.QueueDeleteAsync(queue2);

                await CloseAndWaitForRecoveryAsync(conn);
                await WaitAsync(connectionRecoveryTcs, "recovery succeeded");

                Assert.True(ch.IsOpen);
                await AssertExchangeRecoveryAsync(ch, exchange);
                await ch.QueueDeclarePassiveAsync(queue1);
                await ch.QueueDeclarePassiveAsync(queue2);

                var pt1 = ch.BasicPublishAsync(exchange, binding1, true, _encoding.GetBytes("test message"));
                var pt2 = ch.BasicPublishAsync(exchange, binding2, true, _encoding.GetBytes("test message"));
                await WaitForConfirmsWithCancellationAsync(ch);
                await Task.WhenAll(pt1.AsTask(), pt2.AsTask()).WaitAsync(WaitSpan);

                await Task.WhenAll(consumerReceivedTcs1.Task, consumerReceivedTcs2.Task).WaitAsync(WaitSpan);
                Assert.True(await consumerReceivedTcs1.Task);
                Assert.True(await consumerReceivedTcs2.Task);
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
                    return rq.Name.Contains("exception")
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
            conn.RecoverySucceededAsync += (source, ea) =>
            {
                tcs.SetResult(true);
                return Task.CompletedTask;
            };
            IChannel ch = await conn.CreateChannelAsync();

            string queueToRecoverWithException = "recovery.exception.queue";
            string queueToRecoverSuccessfully = "successfully.recovered.queue";
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
                        await channel.ExchangeDeclareAsync(re.Name, "topic", false, false);
                        await channel.CloseAsync();
                    }
                }
            };

            AutorecoveringConnection conn = await CreateAutorecoveringConnectionWithTopologyRecoveryExceptionHandlerAsync(exceptionHandler);
            conn.RecoverySucceededAsync += (source, ea) =>
            {
                tcs.SetResult(true);
                return Task.CompletedTask;
            };

            string exchangeToRecoverWithException = "recovery.exception.exchange";
            string exchangeToRecoverSuccessfully = "successfully.recovered.exchange";
            IChannel ch = await conn.CreateChannelAsync();
            await ch.ExchangeDeclareAsync(exchangeToRecoverWithException, "direct", false, false);
            await ch.ExchangeDeclareAsync(exchangeToRecoverSuccessfully, "direct", false, false);

            await _channel.ExchangeDeleteAsync(exchangeToRecoverSuccessfully);
            await _channel.ExchangeDeleteAsync(exchangeToRecoverWithException);
            await _channel.ExchangeDeclareAsync(exchangeToRecoverWithException, "topic", false, false);

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

            const string exchange = "topology.recovery.exchange";
            const string queueWithExceptionBinding = "recovery.exception.queue";
            const string bindingToRecoverWithException = "recovery.exception.binding";

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
            conn.RecoverySucceededAsync += (source, ea) =>
            {
                connectionRecoveryTcs.SetResult(true);
                return Task.CompletedTask;
            };
            IChannel ch = await conn.CreateChannelAsync();

            const string queueWithRecoveredBinding = "successfully.recovered.queue";
            const string bindingToRecoverSuccessfully = "successfully.recovered.binding";

            await _channel.QueueDeclareAsync(queueWithExceptionBinding, false, false, false);

            await ch.ExchangeDeclareAsync(exchange, "direct");
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

            string queueWithExceptionConsumer = "recovery.exception.queue";

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
            conn.RecoverySucceededAsync += (source, ea) =>
            {
                connectionRecoveryTcs.SetResult(true);
                return Task.CompletedTask;
            };
            IChannel ch = await conn.CreateChannelAsync();
            try
            {
                await ch.ConfirmSelectAsync();

                await _channel.QueueDeclareAsync(queueWithExceptionConsumer, false, false, false);
                await _channel.QueuePurgeAsync(queueWithExceptionConsumer);

                var recoveredConsumerReceivedTcs = new ManualResetEventSlim(false);
                var consumerToRecover = new AsyncEventingBasicConsumer(ch);
                consumerToRecover.Received += (source, ea) =>
                {
                    recoveredConsumerReceivedTcs.Set();
                    return Task.CompletedTask;
                };
                await ch.BasicConsumeAsync(queueWithExceptionConsumer, true, "exception.consumer", consumerToRecover);

                await _channel.QueueDeleteAsync(queueWithExceptionConsumer);

                await CloseAndWaitForShutdownAsync(conn);
                await WaitAsync(connectionRecoveryTcs, TimeSpan.FromSeconds(20), "recovery succeeded");

                Assert.True(ch.IsOpen);

                await ch.BasicPublishAsync("", queueWithExceptionConsumer, _encoding.GetBytes("test message"));

                Assert.True(recoveredConsumerReceivedTcs.Wait(TimeSpan.FromSeconds(5)));

                await ch.BasicConsumeAsync(queueWithExceptionConsumer, true, "exception.consumer", consumerToRecover);
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
