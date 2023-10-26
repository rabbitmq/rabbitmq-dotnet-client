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

namespace Test.SequentialIntegration
{
    public class TestConnectionRecovery : TestConnectionRecoveryBase
    {
        private readonly string _queueName;

        public TestConnectionRecovery(ITestOutputHelper output) : base(output)
        {
            _queueName = $"TestConnectionRecovery-queue-{Guid.NewGuid()}";
        }

        protected override void TearDown()
        {
            var cf = CreateConnectionFactory();
            cf.ClientProvidedName = cf.ClientProvidedName + "-TearDown";
            using IConnection conn = cf.CreateConnection();
            using IChannel ch = conn.CreateChannel();
            ch.QueueDelete(_queueName);
            base.TearDown();
        }

        [Fact]
        public void TestBasicAckAfterChannelRecovery()
        {
            var allMessagesSeenLatch = new ManualResetEventSlim(false);
            var cons = new AckingBasicConsumer(_channel, _totalMessageCount, allMessagesSeenLatch);

            string queueName = _channel.QueueDeclare(_queueName, false, false, false, null).QueueName;
            Assert.Equal(queueName, _queueName);

            _channel.BasicQos(0, 1, false);
            _channel.BasicConsume(queueName, false, cons);

            ManualResetEventSlim sl = PrepareForShutdown(_conn);
            ManualResetEventSlim rl = PrepareForRecovery(_conn);

            PublishMessagesWhileClosingConn(queueName);

            Wait(sl, "connection shutdown");
            Wait(rl, "connection recovery");
            Wait(allMessagesSeenLatch, "all messages seen");
        }

        [Fact]
        public void TestBasicNackAfterChannelRecovery()
        {
            var allMessagesSeenLatch = new ManualResetEventSlim(false);
            var cons = new NackingBasicConsumer(_channel, _totalMessageCount, allMessagesSeenLatch);

            string queueName = _channel.QueueDeclare(_queueName, false, false, false, null).QueueName;
            Assert.Equal(queueName, _queueName);

            _channel.BasicQos(0, 1, false);
            _channel.BasicConsume(queueName, false, cons);

            ManualResetEventSlim sl = PrepareForShutdown(_conn);
            ManualResetEventSlim rl = PrepareForRecovery(_conn);

            PublishMessagesWhileClosingConn(queueName);

            Wait(sl, "connection shutdown");
            Wait(rl, "connection recovery");
            Wait(allMessagesSeenLatch, "all messages seen");
        }

        [Fact]
        public void TestBasicRejectAfterChannelRecovery()
        {
            var allMessagesSeenLatch = new ManualResetEventSlim(false);
            var cons = new RejectingBasicConsumer(_channel, _totalMessageCount, allMessagesSeenLatch);

            string queueName = _channel.QueueDeclare(_queueName, false, false, false, null).QueueName;
            Assert.Equal(queueName, _queueName);

            _channel.BasicQos(0, 1, false);
            _channel.BasicConsume(queueName, false, cons);

            ManualResetEventSlim sl = PrepareForShutdown(_conn);
            ManualResetEventSlim rl = PrepareForRecovery(_conn);

            PublishMessagesWhileClosingConn(queueName);

            Wait(sl, "connection shutdown");
            Wait(rl, "connection recovery");
            Wait(allMessagesSeenLatch, "all messages seen");
        }

        [Fact]
        public void TestBasicAckAfterBasicGetAndChannelRecovery()
        {
            string q = GenerateQueueName();
            _channel.QueueDeclare(q, false, false, false, null);
            // create an offset
            _channel.BasicPublish("", q, _messageBody);
            Thread.Sleep(50);
            BasicGetResult g = _channel.BasicGet(q, false);
            CloseAndWaitForRecovery();
            Assert.True(_conn.IsOpen);
            Assert.True(_channel.IsOpen);
            // ack the message after recovery - this should be out of range and ignored
            _channel.BasicAck(g.DeliveryTag, false);
            // do a sync operation to 'check' there is no channel exception
            _channel.BasicGet(q, false);
        }

        [Fact]
        public void TestBasicAckEventHandlerRecovery()
        {
            _channel.ConfirmSelect();
            var latch = new ManualResetEventSlim(false);
            ((AutorecoveringChannel)_channel).BasicAcks += (m, args) => latch.Set();
            ((AutorecoveringChannel)_channel).BasicNacks += (m, args) => latch.Set();

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.True(_channel.IsOpen);

            WithTemporaryNonExclusiveQueue(_channel, (m, q) => m.BasicPublish("", q, _messageBody));
            Wait(latch, "basic acks/nacks");
        }

        [Fact]
        public void TestBasicConnectionRecovery()
        {
            Assert.True(_conn.IsOpen);
            CloseAndWaitForRecovery();
            Assert.True(_conn.IsOpen);
        }

        [Fact]
        public void TestBasicConnectionRecoveryOnBrokerRestart()
        {
            Assert.True(_conn.IsOpen);
            RestartServerAndWaitForRecovery();
            Assert.True(_conn.IsOpen);
        }

        [Fact]
        public void TestBasicChannelRecovery()
        {
            Assert.True(_channel.IsOpen);
            CloseAndWaitForRecovery();
            Assert.True(_channel.IsOpen);
        }

        [Fact]
        public void TestBasicChannelRecoveryOnServerRestart()
        {
            Assert.True(_channel.IsOpen);
            RestartServerAndWaitForRecovery();
            Assert.True(_channel.IsOpen);
        }

        [Fact]
        public void TestBlockedListenersRecovery()
        {
            var latch = new ManualResetEventSlim(false);
            _conn.ConnectionBlocked += (c, reason) => latch.Set();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();

            Block();
            Wait(latch, "connection blocked");

            Unblock();
        }

        [Fact]
        public void TestClientNamedQueueRecovery()
        {
            string s = "dotnet-client.test.recovery.q1";
            WithTemporaryNonExclusiveQueue(_channel, (m, q) =>
            {
                CloseAndWaitForRecovery();
                AssertQueueRecovery(m, q, false);
                _channel.QueueDelete(q);
            }, s);
        }

        [Fact]
        public void TestClientNamedQueueRecoveryNoWait()
        {
            string s = "dotnet-client.test.recovery.q1-nowait";
            WithTemporaryQueueNoWait(_channel, (m, q) =>
            {
                CloseAndWaitForRecovery();
                AssertQueueRecovery(m, q);
            }, s);
        }

        [Fact]
        public void TestClientNamedQueueRecoveryOnServerRestart()
        {
            string s = "dotnet-client.test.recovery.q1";
            WithTemporaryNonExclusiveQueue(_channel, (m, q) =>
            {
                RestartServerAndWaitForRecovery();
                AssertQueueRecovery(m, q, false);
                _channel.QueueDelete(q);
            }, s);
        }

        [Fact]
        public void TestConsumerRecoveryWithManyConsumers()
        {
            string q = _channel.QueueDeclare(GenerateQueueName(), false, false, false, null).QueueName;
            int n = 1024;

            for (int i = 0; i < n; i++)
            {
                var cons = new EventingBasicConsumer(_channel);
                _channel.BasicConsume(q, true, cons);
            }

            var latch = new ManualResetEventSlim(false);
            ((AutorecoveringConnection)_conn).ConsumerTagChangeAfterRecovery += (prev, current) => latch.Set();

            CloseAndWaitForRecovery();
            Wait(latch, "consumer tag change after recovery");
            Assert.True(_channel.IsOpen);
            AssertConsumerCount(q, n);
        }

        [Fact]
        public void TestDeclarationOfManyAutoDeleteExchangesWithTransientExchangesThatAreDeleted()
        {
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 3; i++)
            {
                string x1 = $"source-{Guid.NewGuid()}";
                _channel.ExchangeDeclare(x1, "fanout", false, true, null);
                string x2 = $"destination-{Guid.NewGuid()}";
                _channel.ExchangeDeclare(x2, "fanout", false, false, null);
                _channel.ExchangeBind(x2, x1, "");
                _channel.ExchangeDelete(x2);
            }
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
        }

        [Fact]
        public void TestDeclarationOfManyAutoDeleteExchangesWithTransientExchangesThatAreUnbound()
        {
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string x1 = $"source-{Guid.NewGuid()}";
                _channel.ExchangeDeclare(x1, "fanout", false, true, null);
                string x2 = $"destination-{Guid.NewGuid()}";
                _channel.ExchangeDeclare(x2, "fanout", false, false, null);
                _channel.ExchangeBind(x2, x1, "");
                _channel.ExchangeUnbind(x2, x1, "");
                _channel.ExchangeDelete(x2);
            }
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
        }

        [Fact]
        public void TestDeclarationOfManyAutoDeleteExchangesWithTransientQueuesThatAreDeleted()
        {
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string x = Guid.NewGuid().ToString();
                _channel.ExchangeDeclare(x, "fanout", false, true, null);
                RabbitMQ.Client.QueueDeclareOk q = _channel.QueueDeclare();
                _channel.QueueBind(q, x, "");
                _channel.QueueDelete(q);
            }
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
        }

        [Fact]
        public void TestDeclarationOfManyAutoDeleteExchangesWithTransientQueuesThatAreUnbound()
        {
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string x = Guid.NewGuid().ToString();
                _channel.ExchangeDeclare(x, "fanout", false, true, null);
                RabbitMQ.Client.QueueDeclareOk q = _channel.QueueDeclare();
                _channel.QueueBind(q, x, "");
                _channel.QueueUnbind(q, x, "", null);
            }
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
        }

        [Fact]
        public void TestDeclarationOfManyAutoDeleteQueuesWithTransientConsumer()
        {
            AssertRecordedQueues((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string q = Guid.NewGuid().ToString();
                _channel.QueueDeclare(q, false, false, true, null);
                var dummy = new EventingBasicConsumer(_channel);
                string tag = _channel.BasicConsume(q, true, dummy);
                _channel.BasicCancel(tag);
            }
            AssertRecordedQueues((AutorecoveringConnection)_conn, 0);
        }

        [Fact]
        public void TestExchangeRecovery()
        {
            string x = "dotnet-client.test.recovery.x1";
            DeclareNonDurableExchange(_channel, x);
            CloseAndWaitForRecovery();
            AssertExchangeRecovery(_channel, x);
            _channel.ExchangeDelete(x);
        }

        [Fact]
        public void TestExchangeRecoveryWithNoWait()
        {
            string x = "dotnet-client.test.recovery.x1-nowait";
            DeclareNonDurableExchangeNoWait(_channel, x);
            CloseAndWaitForRecovery();
            AssertExchangeRecovery(_channel, x);
            _channel.ExchangeDelete(x);
        }

        [Fact]
        public void TestExchangeToExchangeBindingRecovery()
        {
            string q = _channel.QueueDeclare("", false, false, false, null).QueueName;
            string x1 = "amq.fanout";
            string x2 = GenerateExchangeName();

            _channel.ExchangeDeclare(x2, "fanout");
            _channel.ExchangeBind(x1, x2, "");
            _channel.QueueBind(q, x1, "");

            try
            {
                CloseAndWaitForRecovery();
                Assert.True(_channel.IsOpen);
                _channel.BasicPublish(x2, "", _encoding.GetBytes("msg"));
                AssertMessageCount(q, 1);
            }
            finally
            {
                WithTemporaryChannel(m =>
                {
                    m.ExchangeDelete(x2);
                    m.QueueDelete(q);
                });
            }
        }

        [Fact]
        public void TestQueueRecoveryWithManyQueues()
        {
            var qs = new List<string>();
            int n = 1024;
            for (int i = 0; i < n; i++)
            {
                qs.Add(_channel.QueueDeclare(GenerateQueueName(), false, false, false, null).QueueName);
            }
            CloseAndWaitForRecovery();
            Assert.True(_channel.IsOpen);
            foreach (string q in qs)
            {
                AssertQueueRecovery(_channel, q, false);
                _channel.QueueDelete(q);
            }
        }

        // rabbitmq/rabbitmq-dotnet-client#43
        [Fact]
        public void TestClientNamedTransientAutoDeleteQueueAndBindingRecovery()
        {
            string q = Guid.NewGuid().ToString();
            string x = "tmp-fanout";
            IChannel ch = _conn.CreateChannel();
            ch.QueueDelete(q);
            ch.ExchangeDelete(x);
            ch.ExchangeDeclare(exchange: x, type: "fanout");
            ch.QueueDeclare(queue: q, durable: false, exclusive: false, autoDelete: true, arguments: null);
            ch.QueueBind(queue: q, exchange: x, routingKey: "");
            RestartServerAndWaitForRecovery();
            Assert.True(ch.IsOpen);
            ch.ConfirmSelect();
            ch.QueuePurge(q);
            ch.ExchangeDeclare(exchange: x, type: "fanout");
            ch.BasicPublish(exchange: x, routingKey: "", body: _encoding.GetBytes("msg"));
            WaitForConfirms(ch);
            RabbitMQ.Client.QueueDeclareOk ok = ch.QueueDeclare(queue: q, durable: false, exclusive: false, autoDelete: true, arguments: null);
            Assert.Equal(1u, ok.MessageCount);
            ch.QueueDelete(q);
            ch.ExchangeDelete(x);
        }

        // rabbitmq/rabbitmq-dotnet-client#43
        [Fact]
        public void TestServerNamedTransientAutoDeleteQueueAndBindingRecovery()
        {
            string x = "tmp-fanout";
            IChannel ch = _conn.CreateChannel();
            ch.ExchangeDelete(x);
            ch.ExchangeDeclare(exchange: x, type: "fanout");
            string q = ch.QueueDeclare(queue: "", durable: false, exclusive: false, autoDelete: true, arguments: null).QueueName;
            string nameBefore = q;
            string nameAfter = null;
            var latch = new ManualResetEventSlim(false);
            ((AutorecoveringConnection)_conn).QueueNameChangeAfterRecovery += (source, ea) =>
            {
                nameBefore = ea.NameBefore;
                nameAfter = ea.NameAfter;
                latch.Set();
            };
            ch.QueueBind(queue: nameBefore, exchange: x, routingKey: "");
            RestartServerAndWaitForRecovery();
            Wait(latch, "queue name change after recovery");
            Assert.True(ch.IsOpen);
            Assert.NotEqual(nameBefore, nameAfter);
            ch.ConfirmSelect();
            ch.ExchangeDeclare(exchange: x, type: "fanout");
            ch.BasicPublish(exchange: x, routingKey: "", body: _encoding.GetBytes("msg"));
            WaitForConfirms(ch);
            RabbitMQ.Client.QueueDeclareOk ok = ch.QueueDeclarePassive(nameAfter);
            Assert.Equal(1u, ok.MessageCount);
            ch.QueueDelete(q);
            ch.ExchangeDelete(x);
        }

        [Fact]
        public void TestRecoveryEventHandlersOnConnection()
        {
            int counter = 0;
            ((AutorecoveringConnection)_conn).RecoverySucceeded += (source, ea) => Interlocked.Increment(ref counter);

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.True(_conn.IsOpen);
            Assert.True(counter >= 3);
        }

        [Fact]
        public void TestRecoveryEventHandlersOnChannel()
        {
            int counter = 0;
            ((AutorecoveringChannel)_channel).Recovery += (source, ea) => Interlocked.Increment(ref counter);

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.True(_channel.IsOpen);
            Assert.True(counter >= 3);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public void TestRecoveringConsumerHandlerOnConnection(int iterations)
        {
            string q = _channel.QueueDeclare(GenerateQueueName(), false, false, false, null).QueueName;
            var cons = new EventingBasicConsumer(_channel);
            _channel.BasicConsume(q, true, cons);

            int counter = 0;
            ((AutorecoveringConnection)_conn).RecoveringConsumer += (sender, args) => Interlocked.Increment(ref counter);

            for (int i = 0; i < iterations; i++)
            {
                CloseAndWaitForRecovery();
            }

            Assert.Equal(iterations, counter);
        }

        [Fact]
        public void TestRecoveringConsumerHandlerOnConnection_EventArgumentsArePassedDown()
        {
            var myArgs = new Dictionary<string, object> { { "first-argument", "some-value" } };
            string q = _channel.QueueDeclare(GenerateQueueName(), false, false, false, null).QueueName;
            var cons = new EventingBasicConsumer(_channel);
            string expectedCTag = _channel.BasicConsume(cons, q, arguments: myArgs);

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

            CloseAndWaitForRecovery();
            Assert.True(ctagMatches, "expected consumer tag to match");
            Assert.True(consumerArgumentMatches, "expected consumer arguments to match");
            string actualVal = (string)Assert.Contains("first-argument", myArgs as IDictionary<string, object>);
            Assert.Equal("event-handler-set-this-value", actualVal);
        }

        [Fact]
        public void TestServerNamedQueueRecovery()
        {
            string q = _channel.QueueDeclare("", false, false, false, null).QueueName;
            string x = "amq.fanout";
            _channel.QueueBind(q, x, "");

            string nameBefore = q;
            string nameAfter = null;

            var latch = new ManualResetEventSlim(false);
            var connection = (AutorecoveringConnection)_conn;
            connection.RecoverySucceeded += (source, ea) => latch.Set();
            connection.QueueNameChangeAfterRecovery += (source, ea) => { nameAfter = ea.NameAfter; };

            CloseAndWaitForRecovery();
            Wait(latch, "recovery succeeded");

            Assert.NotNull(nameAfter);
            Assert.StartsWith("amq.", nameBefore);
            Assert.StartsWith("amq.", nameAfter);
            Assert.NotEqual(nameBefore, nameAfter);

            _channel.QueueDeclarePassive(nameAfter);
        }

        [Fact]
        public void TestShutdownEventHandlersRecoveryOnConnection()
        {
            int counter = 0;
            _conn.ConnectionShutdown += (c, args) => Interlocked.Increment(ref counter);

            Assert.True(_conn.IsOpen);
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.True(_conn.IsOpen);

            Assert.True(counter >= 3);
        }

        [Fact]
        public void TestShutdownEventHandlersRecoveryOnConnectionAfterDelayedServerRestart()
        {
            int counter = 0;
            _conn.ConnectionShutdown += (c, args) => Interlocked.Increment(ref counter);
            ManualResetEventSlim shutdownLatch = PrepareForShutdown(_conn);
            ManualResetEventSlim recoveryLatch = PrepareForRecovery((AutorecoveringConnection)_conn);

            Assert.True(_conn.IsOpen);

            try
            {
                StopRabbitMQ();
                Thread.Sleep(7000);
            }
            finally
            {
                StartRabbitMQ();
            }

            Wait(shutdownLatch, WaitSpan, "connection shutdown");
            Wait(recoveryLatch, WaitSpan, "connection recovery");
            Assert.True(_conn.IsOpen);
            Assert.True(counter >= 1);
        }

        [Fact]
        public void TestShutdownEventHandlersRecoveryOnChannel()
        {
            int counter = 0;
            _channel.ChannelShutdown += (c, args) => Interlocked.Increment(ref counter);

            Assert.True(_channel.IsOpen);
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.True(_channel.IsOpen);

            Assert.True(counter >= 3);
        }

        [Fact]
        public void TestRecoverTopologyOnDisposedChannel()
        {
            string x = GenerateExchangeName();
            string q = GenerateQueueName();
            const string rk = "routing-key";

            using (IChannel m = _conn.CreateChannel())
            {
                m.ExchangeDeclare(exchange: x, type: "fanout");
                m.QueueDeclare(q, false, false, false, null);
                m.QueueBind(q, x, rk);
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

        [Fact(Skip = "TODO-FLAKY")]
        public void TestPublishRpcRightAfterReconnect()
        {
            string testQueueName = $"dotnet-client.test.{nameof(TestPublishRpcRightAfterReconnect)}";
            _channel.QueueDeclare(testQueueName, false, false, false, null);
            var replyConsumer = new EventingBasicConsumer(_channel);
            _channel.BasicConsume("amq.rabbitmq.reply-to", true, replyConsumer);
            var properties = new BasicProperties();
            properties.ReplyTo = "amq.rabbitmq.reply-to";

            TimeSpan doneSpan = TimeSpan.FromMilliseconds(100);
            var done = new ManualResetEventSlim(false);
            Task.Run(() =>
            {
                try
                {

                    CloseAndWaitForRecovery();
                }
                finally
                {
                    done.Set();
                }
            });

            while (!done.IsSet)
            {
                try
                {
                    _channel.BasicPublish(string.Empty, testQueueName, properties, _messageBody);
                }
                catch (Exception e)
                {
                    if (e is AlreadyClosedException a)
                    {
                        // 406 is received, when the reply consumer isn't yet recovered
                        Assert.NotEqual(406, a.ShutdownReason.ReplyCode);
                    }
                }
                done.Wait(doneSpan);
            }
        }

        [Fact]
        public void TestThatCancelledConsumerDoesNotReappearOnRecovery()
        {
            string q = _channel.QueueDeclare(GenerateQueueName(), false, false, false, null).QueueName;
            int n = 1024;

            for (int i = 0; i < n; i++)
            {
                var cons = new EventingBasicConsumer(_channel);
                string tag = _channel.BasicConsume(q, true, cons);
                _channel.BasicCancel(tag);
            }
            CloseAndWaitForRecovery();
            Assert.True(_channel.IsOpen);
            AssertConsumerCount(q, 0);
        }

        [Fact]
        public void TestThatDeletedExchangeBindingsDontReappearOnRecovery()
        {
            string q = _channel.QueueDeclare("", false, false, false, null).QueueName;
            string x1 = "amq.fanout";
            string x2 = GenerateExchangeName();

            _channel.ExchangeDeclare(x2, "fanout");
            _channel.ExchangeBind(x1, x2, "");
            _channel.QueueBind(q, x1, "");
            _channel.ExchangeUnbind(x1, x2, "", null);

            try
            {
                CloseAndWaitForRecovery();
                Assert.True(_channel.IsOpen);
                _channel.BasicPublish(x2, "", _encoding.GetBytes("msg"));
                AssertMessageCount(q, 0);
            }
            finally
            {
                WithTemporaryChannel(m =>
                {
                    m.ExchangeDelete(x2);
                    m.QueueDelete(q);
                });
            }
        }

        [Fact]
        public void TestThatDeletedExchangesDontReappearOnRecovery()
        {
            string x = GenerateExchangeName();
            _channel.ExchangeDeclare(x, "fanout");
            _channel.ExchangeDelete(x);

            try
            {
                CloseAndWaitForRecovery();
                Assert.True(_channel.IsOpen);
                _channel.ExchangeDeclarePassive(x);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException e)
            {
                // expected
                AssertShutdownError(e.ShutdownReason, 404);
            }
        }

        [Fact]
        public void TestThatDeletedQueueBindingsDontReappearOnRecovery()
        {
            string q = _channel.QueueDeclare("", false, false, false, null).QueueName;
            string x1 = "amq.fanout";
            string x2 = GenerateExchangeName();

            _channel.ExchangeDeclare(x2, "fanout");
            _channel.ExchangeBind(x1, x2, "");
            _channel.QueueBind(q, x1, "");
            _channel.QueueUnbind(q, x1, "", null);

            try
            {
                CloseAndWaitForRecovery();
                Assert.True(_channel.IsOpen);
                _channel.BasicPublish(x2, "", _encoding.GetBytes("msg"));
                AssertMessageCount(q, 0);
            }
            finally
            {
                WithTemporaryChannel(m =>
                {
                    m.ExchangeDelete(x2);
                    m.QueueDelete(q);
                });
            }
        }

        [Fact]
        public void TestThatDeletedQueuesDontReappearOnRecovery()
        {
            string q = "dotnet-client.recovery.q1";
            _channel.QueueDeclare(q, false, false, false, null);
            _channel.QueueDelete(q);

            try
            {
                CloseAndWaitForRecovery();
                Assert.True(_channel.IsOpen);
                _channel.QueueDeclarePassive(q);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException e)
            {
                // expected
                AssertShutdownError(e.ShutdownReason, 404);
            }
        }

        [Fact]
        public void TestUnblockedListenersRecovery()
        {
            var latch = new ManualResetEventSlim(false);
            _conn.ConnectionUnblocked += (source, ea) => latch.Set();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();

            Block();
            Unblock();
            Wait(latch, "connection unblocked");
        }

        [Fact]
        public void TestTopologyRecoveryQueueFilter()
        {
            var filter = new TopologyRecoveryFilter
            {
                QueueFilter = queue => !queue.Name.Contains("filtered")
            };
            var latch = new ManualResetEventSlim(false);
            AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryFilter(filter);
            conn.RecoverySucceeded += (source, ea) => latch.Set();
            IChannel ch = conn.CreateChannel();

            var queueToRecover = "recovered.queue";
            var queueToIgnore = "filtered.queue";
            ch.QueueDeclare(queueToRecover, false, false, false, null);
            ch.QueueDeclare(queueToIgnore, false, false, false, null);

            _channel.QueueDelete(queueToRecover);
            _channel.QueueDelete(queueToIgnore);

            try
            {
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
            finally
            {
                conn.Abort();
            }
        }

        [Fact]
        public void TestTopologyRecoveryExchangeFilter()
        {
            var filter = new TopologyRecoveryFilter
            {
                ExchangeFilter = exchange => exchange.Type == "topic" && !exchange.Name.Contains("filtered")
            };
            var latch = new ManualResetEventSlim(false);
            AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryFilter(filter);
            conn.RecoverySucceeded += (source, ea) => latch.Set();
            IChannel ch = conn.CreateChannel();

            var exchangeToRecover = "recovered.exchange";
            var exchangeToIgnore = "filtered.exchange";
            ch.ExchangeDeclare(exchangeToRecover, "topic", false, true);
            ch.ExchangeDeclare(exchangeToIgnore, "direct", false, true);

            _channel.ExchangeDelete(exchangeToRecover);
            _channel.ExchangeDelete(exchangeToIgnore);

            try
            {
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
            finally
            {
                conn.Abort();
            }
        }

        [Fact]
        public void TestTopologyRecoveryBindingFilter()
        {
            var filter = new TopologyRecoveryFilter
            {
                BindingFilter = binding => !binding.RoutingKey.Contains("filtered")
            };
            var latch = new ManualResetEventSlim(false);
            AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryFilter(filter);
            conn.RecoverySucceeded += (source, ea) => latch.Set();
            IChannel ch = conn.CreateChannel();

            var exchange = "topology.recovery.exchange";
            var queueWithRecoveredBinding = "topology.recovery.queue.1";
            var queueWithIgnoredBinding = "topology.recovery.queue.2";
            var bindingToRecover = "recovered.binding";
            var bindingToIgnore = "filtered.binding";

            ch.ExchangeDeclare(exchange, "direct");
            ch.QueueDeclare(queueWithRecoveredBinding, false, false, false, null);
            ch.QueueDeclare(queueWithIgnoredBinding, false, false, false, null);
            ch.QueueBind(queueWithRecoveredBinding, exchange, bindingToRecover);
            ch.QueueBind(queueWithIgnoredBinding, exchange, bindingToIgnore);
            ch.QueuePurge(queueWithRecoveredBinding);
            ch.QueuePurge(queueWithIgnoredBinding);

            _channel.QueueUnbind(queueWithRecoveredBinding, exchange, bindingToRecover);
            _channel.QueueUnbind(queueWithIgnoredBinding, exchange, bindingToIgnore);

            try
            {
                CloseAndWaitForRecovery(conn);
                Wait(latch, "recovery succeeded");

                Assert.True(ch.IsOpen);
                Assert.True(SendAndConsumeMessage(_conn, queueWithRecoveredBinding, exchange, bindingToRecover));
                Assert.False(SendAndConsumeMessage(_conn, queueWithIgnoredBinding, exchange, bindingToIgnore));
            }
            finally
            {
                conn.Abort();
            }
        }

        [Fact]
        public void TestTopologyRecoveryDefaultFilterRecoversAllEntities()
        {
            var filter = new TopologyRecoveryFilter();
            var latch = new ManualResetEventSlim(false);
            AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryFilter(filter);
            conn.RecoverySucceeded += (source, ea) => latch.Set();
            IChannel ch = conn.CreateChannel();
            ch.ConfirmSelect();

            var exchange = "topology.recovery.exchange";
            var queue1 = "topology.recovery.queue.1";
            var queue2 = "topology.recovery.queue.2";
            var binding1 = "recovered.binding";
            var binding2 = "filtered.binding";

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

            try
            {
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
            finally
            {
                conn.Abort();
            }
        }

        [Fact]
        public void TestTopologyRecoveryQueueExceptionHandler()
        {
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
                    using (var channel = connection.CreateChannel())
                    {
                        channel.QueueDeclare(rq.Name, false, false, false, changedQueueArguments);
                    }
                }
            };
            var latch = new ManualResetEventSlim(false);
            AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryExceptionHandler(exceptionHandler);
            conn.RecoverySucceeded += (source, ea) => latch.Set();
            IChannel ch = conn.CreateChannel();

            var queueToRecoverWithException = "recovery.exception.queue";
            var queueToRecoverSuccessfully = "successfully.recovered.queue";
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
                //Cleanup
                _channel.QueueDelete(queueToRecoverWithException);

                conn.Abort();
            }
        }

        [Fact]
        public void TestTopologyRecoveryExchangeExceptionHandler()
        {
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
                    using (var channel = connection.CreateChannel())
                    {
                        channel.ExchangeDeclare(re.Name, "topic", false, false);
                    }
                }
            };
            var latch = new ManualResetEventSlim(false);
            AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryExceptionHandler(exceptionHandler);
            conn.RecoverySucceeded += (source, ea) => latch.Set();
            IChannel ch = conn.CreateChannel();

            var exchangeToRecoverWithException = "recovery.exception.exchange";
            var exchangeToRecoverSuccessfully = "successfully.recovered.exchange";
            ch.ExchangeDeclare(exchangeToRecoverWithException, "direct", false, false);
            ch.ExchangeDeclare(exchangeToRecoverSuccessfully, "direct", false, false);

            _channel.ExchangeDelete(exchangeToRecoverSuccessfully);
            _channel.ExchangeDelete(exchangeToRecoverWithException);
            _channel.ExchangeDeclare(exchangeToRecoverWithException, "topic", false, false);

            try
            {
                CloseAndWaitForRecovery(conn);
                Wait(latch, "recovery succeeded");

                Assert.True(ch.IsOpen);
                AssertExchangeRecovery(ch, exchangeToRecoverSuccessfully);
                AssertExchangeRecovery(ch, exchangeToRecoverWithException);
            }
            finally
            {
                //Cleanup
                _channel.ExchangeDelete(exchangeToRecoverWithException);

                conn.Abort();
            }
        }

        [Fact]
        public void TestTopologyRecoveryBindingExceptionHandler()
        {
            var exchange = "topology.recovery.exchange";
            var queueWithExceptionBinding = "recovery.exception.queue";
            var bindingToRecoverWithException = "recovery.exception.binding";

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
                    using (var channel = connection.CreateChannel())
                    {
                        channel.QueueDeclare(queueWithExceptionBinding, false, false, false, null);
                        channel.QueueBind(queueWithExceptionBinding, exchange, bindingToRecoverWithException);
                    }
                }
            };
            var latch = new ManualResetEventSlim(false);
            AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryExceptionHandler(exceptionHandler);
            conn.RecoverySucceeded += (source, ea) => latch.Set();
            IChannel ch = conn.CreateChannel();

            var queueWithRecoveredBinding = "successfully.recovered.queue";
            var bindingToRecoverSuccessfully = "successfully.recovered.binding";

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

            try
            {
                CloseAndWaitForRecovery(conn);
                Wait(latch, "recovery succeeded");

                Assert.True(ch.IsOpen);
                Assert.True(SendAndConsumeMessage(conn, queueWithRecoveredBinding, exchange, bindingToRecoverSuccessfully));
                Assert.True(SendAndConsumeMessage(conn, queueWithExceptionBinding, exchange, bindingToRecoverWithException));
            }
            finally
            {
                conn.Abort();
            }
        }

        [Fact]
        public void TestTopologyRecoveryConsumerExceptionHandler()
        {
            var queueWithExceptionConsumer = "recovery.exception.queue";

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
                    using (var channel = connection.CreateChannel())
                    {
                        channel.QueueDeclare(queueWithExceptionConsumer, false, false, false, null);
                    }

                    // So topology recovery runs again. This time he missing queue should exist, making
                    // it possible to recover the consumer successfully.
                    throw ex;
                }
            };
            var latch = new ManualResetEventSlim(false);
            AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryExceptionHandler(exceptionHandler);
            conn.RecoverySucceeded += (source, ea) => latch.Set();
            IChannel ch = conn.CreateChannel();
            ch.ConfirmSelect();

            _channel.QueueDeclare(queueWithExceptionConsumer, false, false, false, null);
            _channel.QueuePurge(queueWithExceptionConsumer);

            var recoverLatch = new ManualResetEventSlim(false);
            var consumerToRecover = new EventingBasicConsumer(ch);
            consumerToRecover.Received += (source, ea) => recoverLatch.Set();
            ch.BasicConsume(queueWithExceptionConsumer, true, "exception.consumer", consumerToRecover);

            _channel.QueueDelete(queueWithExceptionConsumer);

            try
            {
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
            finally
            {
                conn.Abort();
            }
        }
    }
}
