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
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using Xunit;
using Xunit.Abstractions;

namespace Test.SequentialIntegration
{
    public class TestConnectionTopologyRecovery : TestConnectionRecoveryBase
    {
        public TestConnectionTopologyRecovery(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void TestRecoverTopologyOnDisposedChannel()
        {
            string x = GenerateExchangeName();
            string q = GenerateQueueName();
            const string rk = "routing-key";

            using (IChannel ch = _conn.CreateChannel())
            {
                ch.ExchangeDeclare(exchange: x, type: "fanout");
                ch.QueueDeclare(q, false, false, false, null);
                ch.QueueBind(q, x, rk);
            }

            var cons = new EventingBasicConsumer(_channel);
            _channel.BasicConsume(q, true, cons);
            AssertConsumerCount(_channel, q, 1);

            CloseAndWaitForRecovery();
            AssertConsumerCount(_channel, q, 1);

            var latch = new ManualResetEventSlim(false);
            cons.Received += (s, args) => latch.Set();

            _channel.BasicPublish("", q, _messageBody);
            Wait(latch, "received event");

            _channel.QueueUnbind(q, x, rk);
            _channel.ExchangeDelete(x);
            _channel.QueueDelete(q);
        }

        [Fact]
        public void TestTopologyRecoveryQueueFilter()
        {
            var latch = new ManualResetEventSlim(false);

            var filter = new TopologyRecoveryFilter
            {
                QueueFilter = queue => !queue.Name.Contains("filtered")
            };

            using AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryFilter(filter);
            conn.RecoverySucceeded += (source, ea) => latch.Set();
            using IChannel ch = conn.CreateChannel();

            string queueToRecover = "recovered.queue";
            string queueToIgnore = "filtered.queue";
            ch.QueueDeclare(queueToRecover, false, false, false, null);
            ch.QueueDeclare(queueToIgnore, false, false, false, null);

            _channel.QueueDelete(queueToRecover);
            _channel.QueueDelete(queueToIgnore);

            CloseAndWaitForRecovery(conn);
            Wait(latch, "recovery succeeded");

            Assert.True(ch.IsOpen);
            AssertQueueRecovery(ch, queueToRecover, false);

            try
            {
                AssertQueueRecovery(ch, queueToIgnore, false);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException e)
            {
                AssertShutdownError(e.ShutdownReason, 404);
            }
        }

        [Fact]
        public void TestTopologyRecoveryExchangeFilter()
        {
            var latch = new ManualResetEventSlim(false);

            var filter = new TopologyRecoveryFilter
            {
                ExchangeFilter = exchange => exchange.Type == "topic" && !exchange.Name.Contains("filtered")
            };

            using AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryFilter(filter);
            conn.RecoverySucceeded += (source, ea) => latch.Set();
            using IChannel ch = conn.CreateChannel();

            string exchangeToRecover = "recovered.exchange";
            string exchangeToIgnore = "filtered.exchange";
            ch.ExchangeDeclare(exchangeToRecover, "topic", false, true);
            ch.ExchangeDeclare(exchangeToIgnore, "direct", false, true);

            _channel.ExchangeDelete(exchangeToRecover);
            _channel.ExchangeDelete(exchangeToIgnore);

            CloseAndWaitForRecovery(conn);
            Wait(latch, "recovery succeeded");

            Assert.True(ch.IsOpen);
            AssertExchangeRecovery(ch, exchangeToRecover);

            try
            {
                AssertExchangeRecovery(ch, exchangeToIgnore);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException e)
            {
                AssertShutdownError(e.ShutdownReason, 404);
            }
        }

        [Fact]
        public void TestTopologyRecoveryBindingFilter()
        {
            var latch = new ManualResetEventSlim(false);

            var filter = new TopologyRecoveryFilter
            {
                BindingFilter = binding => !binding.RoutingKey.Contains("filtered")
            };

            using AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryFilter(filter);
            conn.RecoverySucceeded += (source, ea) => latch.Set();
            using IChannel ch = conn.CreateChannel();

            string exchange = "topology.recovery.exchange";
            string queueWithRecoveredBinding = "topology.recovery.queue.1";
            string queueWithIgnoredBinding = "topology.recovery.queue.2";
            string bindingToRecover = "recovered.binding";
            string bindingToIgnore = "filtered.binding";

            ch.ExchangeDeclare(exchange, "direct");
            ch.QueueDeclare(queueWithRecoveredBinding, false, false, false, null);
            ch.QueueDeclare(queueWithIgnoredBinding, false, false, false, null);
            ch.QueueBind(queueWithRecoveredBinding, exchange, bindingToRecover);
            ch.QueueBind(queueWithIgnoredBinding, exchange, bindingToIgnore);
            ch.QueuePurge(queueWithRecoveredBinding);
            ch.QueuePurge(queueWithIgnoredBinding);

            _channel.QueueUnbind(queueWithRecoveredBinding, exchange, bindingToRecover);
            _channel.QueueUnbind(queueWithIgnoredBinding, exchange, bindingToIgnore);

            CloseAndWaitForRecovery(conn);
            Wait(latch, "recovery succeeded");

            Assert.True(ch.IsOpen);
            Assert.True(SendAndConsumeMessage(_conn, queueWithRecoveredBinding, exchange, bindingToRecover));
            Assert.False(SendAndConsumeMessage(_conn, queueWithIgnoredBinding, exchange, bindingToIgnore));
        }

        [Fact]
        public void TestTopologyRecoveryDefaultFilterRecoversAllEntities()
        {
            var latch = new ManualResetEventSlim(false);
            var filter = new TopologyRecoveryFilter();
            using AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryFilter(filter);
            conn.RecoverySucceeded += (source, ea) => latch.Set();
            using IChannel ch = conn.CreateChannel();
            ch.ConfirmSelect();

            string exchange = "topology.recovery.exchange";
            string queue1 = "topology.recovery.queue.1";
            string queue2 = "topology.recovery.queue.2";
            string binding1 = "recovered.binding";
            string binding2 = "filtered.binding";

            ch.ExchangeDeclare(exchange, "direct");
            ch.QueueDeclare(queue1, false, false, false, null);
            ch.QueueDeclare(queue2, false, false, false, null);
            ch.QueueBind(queue1, exchange, binding1);
            ch.QueueBind(queue2, exchange, binding2);
            ch.QueuePurge(queue1);
            ch.QueuePurge(queue2);

            var consumerLatch1 = new ManualResetEventSlim(false);
            var consumer1 = new EventingBasicConsumer(ch);
            consumer1.Received += (source, ea) => consumerLatch1.Set();
            ch.BasicConsume(queue1, true, "recovered.consumer", consumer1);

            var consumerLatch2 = new ManualResetEventSlim(false);
            var consumer2 = new EventingBasicConsumer(ch);
            consumer2.Received += (source, ea) => consumerLatch2.Set();
            ch.BasicConsume(queue2, true, "filtered.consumer", consumer2);

            _channel.ExchangeDelete(exchange);
            _channel.QueueDelete(queue1);
            _channel.QueueDelete(queue2);

            CloseAndWaitForRecovery(conn);
            Wait(latch, "recovery succeeded");

            Assert.True(ch.IsOpen);
            AssertExchangeRecovery(ch, exchange);
            ch.QueueDeclarePassive(queue1);
            ch.QueueDeclarePassive(queue2);

            ch.BasicPublish(exchange, binding1, _encoding.GetBytes("test message"));
            ch.BasicPublish(exchange, binding2, _encoding.GetBytes("test message"));

            Assert.True(consumerLatch1.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(consumerLatch2.Wait(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void TestTopologyRecoveryQueueExceptionHandler()
        {
            var latch = new ManualResetEventSlim(false);

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
                QueueRecoveryExceptionHandler = (rq, ex, connection) =>
                {
                    using (IChannel channel = connection.CreateChannel())
                    {
                        channel.QueueDeclare(rq.Name, false, false, false, changedQueueArguments);
                    }
                }
            };

            using AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryExceptionHandler(exceptionHandler);
            conn.RecoverySucceeded += (source, ea) => latch.Set();
            using IChannel ch = conn.CreateChannel();

            string queueToRecoverWithException = "recovery.exception.queue";
            string queueToRecoverSuccessfully = "successfully.recovered.queue";
            ch.QueueDeclare(queueToRecoverWithException, false, false, false, null);
            ch.QueueDeclare(queueToRecoverSuccessfully, false, false, false, null);

            _channel.QueueDelete(queueToRecoverSuccessfully);
            _channel.QueueDelete(queueToRecoverWithException);
            _channel.QueueDeclare(queueToRecoverWithException, false, false, false, changedQueueArguments);

            try
            {
                CloseAndWaitForRecovery(conn);
                Wait(latch, "recovery succeded");

                Assert.True(ch.IsOpen);
                AssertQueueRecovery(ch, queueToRecoverSuccessfully, false);
                AssertQueueRecovery(ch, queueToRecoverWithException, false, changedQueueArguments);
            }
            finally
            {
                _channel.QueueDelete(queueToRecoverWithException);
            }
        }

        [Fact]
        public void TestTopologyRecoveryExchangeExceptionHandler()
        {
            var latch = new ManualResetEventSlim(false);

            var exceptionHandler = new TopologyRecoveryExceptionHandler
            {
                ExchangeRecoveryExceptionCondition = (re, ex) =>
                {
                    return re.Name.Contains("exception")
                        && ex is OperationInterruptedException operationInterruptedException
                        && operationInterruptedException.ShutdownReason.ReplyCode == Constants.PreconditionFailed;
                },
                ExchangeRecoveryExceptionHandler = (re, ex, connection) =>
                {
                    using (IChannel channel = connection.CreateChannel())
                    {
                        channel.ExchangeDeclare(re.Name, "topic", false, false);
                    }
                }
            };

            using AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryExceptionHandler(exceptionHandler);
            conn.RecoverySucceeded += (source, ea) => latch.Set();

            string exchangeToRecoverWithException = "recovery.exception.exchange";
            string exchangeToRecoverSuccessfully = "successfully.recovered.exchange";
            using IChannel ch = conn.CreateChannel();
            ch.ExchangeDeclare(exchangeToRecoverWithException, "direct", false, false);
            ch.ExchangeDeclare(exchangeToRecoverSuccessfully, "direct", false, false);

            _channel.ExchangeDelete(exchangeToRecoverSuccessfully);
            _channel.ExchangeDelete(exchangeToRecoverWithException);
            _channel.ExchangeDeclare(exchangeToRecoverWithException, "topic", false, false);

            try
            {
                CloseAndWaitForRecovery(conn);
                Wait(latch, "recovery succeeded");

                Assert.True(_channel.IsOpen);
                AssertExchangeRecovery(_channel, exchangeToRecoverSuccessfully);
                AssertExchangeRecovery(_channel, exchangeToRecoverWithException);
            }
            finally
            {
                _channel.ExchangeDelete(exchangeToRecoverWithException);
            }
        }

        [Fact]
        public void TestTopologyRecoveryBindingExceptionHandler()
        {
            var latch = new ManualResetEventSlim(false);

            string exchange = "topology.recovery.exchange";
            string queueWithExceptionBinding = "recovery.exception.queue";
            string bindingToRecoverWithException = "recovery.exception.binding";

            var exceptionHandler = new TopologyRecoveryExceptionHandler
            {
                BindingRecoveryExceptionCondition = (b, ex) =>
                {
                    return b.RoutingKey.Contains("exception")
                        && ex is OperationInterruptedException operationInterruptedException
                        && operationInterruptedException.ShutdownReason.ReplyCode == Constants.NotFound;
                },
                BindingRecoveryExceptionHandler = (b, ex, connection) =>
                {
                    using (IChannel channel = connection.CreateChannel())
                    {
                        channel.QueueDeclare(queueWithExceptionBinding, false, false, false, null);
                        channel.QueueBind(queueWithExceptionBinding, exchange, bindingToRecoverWithException);
                    }
                }
            };

            using AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryExceptionHandler(exceptionHandler);
            conn.RecoverySucceeded += (source, ea) => latch.Set();
            using IChannel ch = conn.CreateChannel();

            string queueWithRecoveredBinding = "successfully.recovered.queue";
            string bindingToRecoverSuccessfully = "successfully.recovered.binding";

            _channel.QueueDeclare(queueWithExceptionBinding, false, false, false, null);

            ch.ExchangeDeclare(exchange, "direct");
            ch.QueueDeclare(queueWithRecoveredBinding, false, false, false, null);
            ch.QueueBind(queueWithRecoveredBinding, exchange, bindingToRecoverSuccessfully);
            ch.QueueBind(queueWithExceptionBinding, exchange, bindingToRecoverWithException);
            ch.QueuePurge(queueWithRecoveredBinding);
            ch.QueuePurge(queueWithExceptionBinding);

            _channel.QueueUnbind(queueWithRecoveredBinding, exchange, bindingToRecoverSuccessfully);
            _channel.QueueUnbind(queueWithExceptionBinding, exchange, bindingToRecoverWithException);
            _channel.QueueDelete(queueWithExceptionBinding);

            CloseAndWaitForRecovery(conn);
            Wait(latch, "recovery succeeded");

            Assert.True(ch.IsOpen);
            Assert.True(SendAndConsumeMessage(conn, queueWithRecoveredBinding, exchange, bindingToRecoverSuccessfully));
            Assert.True(SendAndConsumeMessage(conn, queueWithExceptionBinding, exchange, bindingToRecoverWithException));
        }

        [Fact]
        public void TestTopologyRecoveryConsumerExceptionHandler()
        {
            var latch = new ManualResetEventSlim(false);

            string queueWithExceptionConsumer = "recovery.exception.queue";

            var exceptionHandler = new TopologyRecoveryExceptionHandler
            {
                ConsumerRecoveryExceptionCondition = (c, ex) =>
                {
                    return c.ConsumerTag.Contains("exception")
                        && ex is OperationInterruptedException operationInterruptedException
                        && operationInterruptedException.ShutdownReason.ReplyCode == Constants.NotFound;
                },
                ConsumerRecoveryExceptionHandler = (c, ex, connection) =>
                {
                    using (IChannel channel = connection.CreateChannel())
                    {
                        channel.QueueDeclare(queueWithExceptionConsumer, false, false, false, null);
                    }

                    // So topology recovery runs again. This time he missing queue should exist, making
                    // it possible to recover the consumer successfully.
                    throw ex;
                }
            };

            using AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryExceptionHandler(exceptionHandler);
            conn.RecoverySucceeded += (source, ea) => latch.Set();
            using IChannel ch = conn.CreateChannel();
            ch.ConfirmSelect();

            _channel.QueueDeclare(queueWithExceptionConsumer, false, false, false, null);
            _channel.QueuePurge(queueWithExceptionConsumer);

            var recoverLatch = new ManualResetEventSlim(false);
            var consumerToRecover = new EventingBasicConsumer(ch);
            consumerToRecover.Received += (source, ea) => recoverLatch.Set();
            ch.BasicConsume(queueWithExceptionConsumer, true, "exception.consumer", consumerToRecover);

            _channel.QueueDelete(queueWithExceptionConsumer);

            CloseAndWaitForShutdown(conn);
            Wait(latch, TimeSpan.FromSeconds(20), "recovery succeeded");

            Assert.True(ch.IsOpen);

            ch.BasicPublish("", queueWithExceptionConsumer, _encoding.GetBytes("test message"));

            Assert.True(recoverLatch.Wait(TimeSpan.FromSeconds(5)));

            try
            {
                ch.BasicConsume(queueWithExceptionConsumer, true, "exception.consumer", consumerToRecover);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException e)
            {
                AssertShutdownError(e.ShutdownReason, 530); // NOT_ALLOWED - not allowed to reuse consumer tag
            }
        }
    }
}
