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
            _queueName = $"{nameof(TestConnectionRecovery)}-{Guid.NewGuid()}";
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

        // https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1086
        [Fact]
        public async Task TestChannelAfterDispose_GH1086()
        {
            TaskCompletionSource<bool> sawChannelShutdownTcs = new TaskCompletionSource<bool>();

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

            RestartServerAndWaitForRecovery();

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

            _channel.Dispose();
            Assert.True(_channel.IsClosed);

            await publishTask;
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
            _channel.ExchangeDelete(x);
            _channel.ExchangeDeclare(exchange: x, type: "fanout");
            string q = _channel.QueueDeclare(queue: "", durable: false, exclusive: false, autoDelete: true, arguments: null).QueueName;
            string nameBefore = q;
            string nameAfter = null;
            var latch = new ManualResetEventSlim(false);
            ((AutorecoveringConnection)_conn).QueueNameChangedAfterRecovery += (source, ea) =>
            {
                nameBefore = ea.NameBefore;
                nameAfter = ea.NameAfter;
                latch.Set();
            };
            _channel.QueueBind(queue: nameBefore, exchange: x, routingKey: "");
            RestartServerAndWaitForRecovery();
            Wait(latch, "queue name change after recovery");
            Assert.True(_channel.IsOpen);
            Assert.NotEqual(nameBefore, nameAfter);
            _channel.ConfirmSelect();
            _channel.ExchangeDeclare(exchange: x, type: "fanout");
            _channel.BasicPublish(exchange: x, routingKey: "", body: _encoding.GetBytes("msg"));
            WaitForConfirms(_channel);
            RabbitMQ.Client.QueueDeclareOk ok = _channel.QueueDeclarePassive(nameAfter);
            Assert.Equal(1u, ok.MessageCount);
            _channel.QueueDelete(q);
            _channel.ExchangeDelete(x);
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
            connection.QueueNameChangedAfterRecovery += (source, ea) => { nameAfter = ea.NameAfter; };

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
    }
}
