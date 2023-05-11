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
    public class TestConnectionRecoveryWithoutSetup : TestConnectionRecoveryBase
    {
        public TestConnectionRecoveryWithoutSetup(ITestOutputHelper output) : base(output)
        {
        }

        protected override void SetUp()
        {
            Assert.Null(_connFactory);
            Assert.Null(_conn);
            Assert.Null(_channel);
        }

        [Fact]
        public void TestBasicConnectionRecoveryWithHostnameList()
        {
            using (AutorecoveringConnection c = CreateAutorecoveringConnection(new List<string> { "127.0.0.1", "localhost" }))
            {
                Assert.True(c.IsOpen);
                CloseAndWaitForRecovery(c);
                Assert.True(c.IsOpen);
            }
        }

        [Fact]
        public void TestBasicConnectionRecoveryWithHostnameListAndUnreachableHosts()
        {
            using (AutorecoveringConnection c = CreateAutorecoveringConnection(new List<string> { "191.72.44.22", "127.0.0.1", "localhost" }))
            {
                Assert.True(c.IsOpen);
                CloseAndWaitForRecovery(c);
                Assert.True(c.IsOpen);
            }
        }

        [Fact]
        public void TestBasicConnectionRecoveryWithEndpointList()
        {
            using (AutorecoveringConnection c = CreateAutorecoveringConnection(
                        new List<AmqpTcpEndpoint>
                        {
                            new AmqpTcpEndpoint("127.0.0.1"),
                            new AmqpTcpEndpoint("localhost")
                        }))
            {
                Assert.True(c.IsOpen);
                CloseAndWaitForRecovery(c);
                Assert.True(c.IsOpen);
            }
        }

        [Fact]
        public void TestBasicConnectionRecoveryWithEndpointListAndUnreachableHosts()
        {
            using (AutorecoveringConnection c = CreateAutorecoveringConnection(
                        new List<AmqpTcpEndpoint>
                        {
                            new AmqpTcpEndpoint("191.72.44.22"),
                            new AmqpTcpEndpoint("127.0.0.1"),
                            new AmqpTcpEndpoint("localhost")
                        }))
            {
                Assert.True(c.IsOpen);
                CloseAndWaitForRecovery(c);
                Assert.True(c.IsOpen);
            }
        }

        [Fact]
        public void TestConsumerWorkServiceRecovery()
        {
            using (AutorecoveringConnection c = CreateAutorecoveringConnection())
            {
                IChannel m = c.CreateChannel();
                string q = m.QueueDeclare("dotnet-client.recovery.consumer_work_pool1",
                    false, false, false, null).QueueName;
                var cons = new EventingBasicConsumer(m);
                m.BasicConsume(q, true, cons);
                AssertConsumerCount(m, q, 1);

                CloseAndWaitForRecovery(c);

                Assert.True(m.IsOpen);
                var latch = new ManualResetEventSlim(false);
                cons.Received += (s, args) => latch.Set();

                m.BasicPublish("", q, _encoding.GetBytes("msg"));
                Wait(latch, "received event");

                m.QueueDelete(q);
            }
        }

        [Fact]
        public void TestConsumerRecoveryOnClientNamedQueueWithOneRecovery()
        {
            string q0 = "dotnet-client.recovery.queue1";
            using (AutorecoveringConnection c = CreateAutorecoveringConnection())
            {
                IChannel m = c.CreateChannel();
                string q1 = m.QueueDeclare(q0, false, false, false, null).QueueName;
                Assert.Equal(q0, q1);

                var cons = new EventingBasicConsumer(m);
                m.BasicConsume(q1, true, cons);
                AssertConsumerCount(m, q1, 1);

                bool queueNameChangeAfterRecoveryCalled = false;

                c.QueueNameChangedAfterRecovery += (source, ea) => { queueNameChangeAfterRecoveryCalled = true; };

                CloseAndWaitForRecovery(c);
                AssertConsumerCount(m, q1, 1);
                Assert.False(queueNameChangeAfterRecoveryCalled);

                CloseAndWaitForRecovery(c);
                AssertConsumerCount(m, q1, 1);
                Assert.False(queueNameChangeAfterRecoveryCalled);

                CloseAndWaitForRecovery(c);
                AssertConsumerCount(m, q1, 1);
                Assert.False(queueNameChangeAfterRecoveryCalled);

                var latch = new ManualResetEventSlim(false);
                cons.Received += (s, args) => latch.Set();

                m.BasicPublish("", q1, _encoding.GetBytes("msg"));
                Wait(latch, "received event");

                m.QueueDelete(q1);
            }
        }

        [Fact]
        public void TestConsumerRecoveryWithServerNamedQueue()
        {
            // https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1238
            using (AutorecoveringConnection c = CreateAutorecoveringConnection())
            {
                IChannel ch = c.CreateChannel();
                RabbitMQ.Client.QueueDeclareOk queueDeclareResult = ch.QueueDeclare(queue: string.Empty, durable: false, exclusive: true, autoDelete: true, arguments: null);
                string qname = queueDeclareResult.QueueName;
                Assert.False(string.IsNullOrEmpty(qname));

                var cons = new EventingBasicConsumer(ch);
                ch.BasicConsume(string.Empty, true, cons);
                AssertConsumerCount(ch, qname, 1);

                bool queueNameBeforeIsEqual = false;
                bool queueNameChangeAfterRecoveryCalled = false;
                string qnameAfterRecovery = null;
                c.QueueNameChangedAfterRecovery += (source, ea) =>
                {
                    queueNameChangeAfterRecoveryCalled = true;
                    queueNameBeforeIsEqual = qname.Equals(ea.NameBefore);
                    qnameAfterRecovery = ea.NameAfter;
                };

                CloseAndWaitForRecovery(c);

                AssertConsumerCount(ch, qnameAfterRecovery, 1);
                Assert.True(queueNameChangeAfterRecoveryCalled);
                Assert.True(queueNameBeforeIsEqual);
            }
        }

        [Fact]
        public void TestCreateChannelOnClosedAutorecoveringConnectionDoesNotHang()
        {
            // we don't want this to recover quickly in this test
            AutorecoveringConnection c = CreateAutorecoveringConnection(TimeSpan.FromSeconds(20));

            try
            {
                c.Close();
                WaitForShutdown(c);
                Assert.False(c.IsOpen);
                c.CreateChannel();
                Assert.Fail("Expected an exception");
            }
            catch (AlreadyClosedException)
            {
                // expected
            }
            finally
            {
                StartRabbitMQ();
                if (c.IsOpen)
                {
                    c.Abort();
                }
            }
        }

        [Fact]
        public void TestTopologyRecoveryConsumerFilter()
        {
            var filter = new TopologyRecoveryFilter
            {
                ConsumerFilter = consumer => !consumer.ConsumerTag.Contains("filtered")
            };
            var latch = new ManualResetEventSlim(false);
            AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryFilter(filter);
            conn.RecoverySucceeded += (source, ea) => latch.Set();
            IChannel ch = conn.CreateChannel();
            ch.ConfirmSelect();

            var exchange = "topology.recovery.exchange";
            var queueWithRecoveredConsumer = "topology.recovery.queue.1";
            var queueWithIgnoredConsumer = "topology.recovery.queue.2";
            var binding1 = "recovered.binding.1";
            var binding2 = "recovered.binding.2";

            ch.ExchangeDeclare(exchange, "direct");
            ch.QueueDeclare(queueWithRecoveredConsumer, false, false, false, null);
            ch.QueueDeclare(queueWithIgnoredConsumer, false, false, false, null);
            ch.QueueBind(queueWithRecoveredConsumer, exchange, binding1);
            ch.QueueBind(queueWithIgnoredConsumer, exchange, binding2);
            ch.QueuePurge(queueWithRecoveredConsumer);
            ch.QueuePurge(queueWithIgnoredConsumer);

            var recoverLatch = new ManualResetEventSlim(false);
            var consumerToRecover = new EventingBasicConsumer(ch);
            consumerToRecover.Received += (source, ea) => recoverLatch.Set();
            ch.BasicConsume(queueWithRecoveredConsumer, true, "recovered.consumer", consumerToRecover);

            var ignoredLatch = new ManualResetEventSlim(false);
            var consumerToIgnore = new EventingBasicConsumer(ch);
            consumerToIgnore.Received += (source, ea) => ignoredLatch.Set();
            ch.BasicConsume(queueWithIgnoredConsumer, true, "filtered.consumer", consumerToIgnore);

            try
            {
                CloseAndWaitForRecovery(conn);
                Wait(latch, "recovery succeeded");

                Assert.True(ch.IsOpen);
                ch.BasicPublish(exchange, binding1, _encoding.GetBytes("test message"));
                ch.BasicPublish(exchange, binding2, _encoding.GetBytes("test message"));

                Assert.True(recoverLatch.Wait(TimeSpan.FromSeconds(5)));
                Assert.False(ignoredLatch.Wait(TimeSpan.FromSeconds(5)));

                ch.BasicConsume(queueWithIgnoredConsumer, true, "filtered.consumer", consumerToIgnore);

                try
                {
                    ch.BasicConsume(queueWithRecoveredConsumer, true, "recovered.consumer", consumerToRecover);
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

        [Fact]
        public void TestRecoveryWithTopologyDisabled()
        {
            AutorecoveringConnection conn = CreateAutorecoveringConnectionWithTopologyRecoveryDisabled();
            IChannel ch = conn.CreateChannel();
            string s = "dotnet-client.test.recovery.q2";
            ch.QueueDelete(s);
            ch.QueueDeclare(s, false, true, false, null);
            ch.QueueDeclarePassive(s);
            Assert.True(ch.IsOpen);

            try
            {
                CloseAndWaitForRecovery(conn);
                Assert.True(ch.IsOpen);
                ch.QueueDeclarePassive(s);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException)
            {
                // expected
            }
            finally
            {
                conn.Abort();
            }
        }
    }
}
