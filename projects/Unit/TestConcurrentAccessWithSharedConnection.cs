// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
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
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at https://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestConcurrentAccessWithSharedConnection : IntegrationFixture
    {

        internal const int Threads = 32;
        internal CountdownEvent _latch;
        internal TimeSpan _completionTimeout = TimeSpan.FromSeconds(90);

        [SetUp]
        public override async ValueTask Init()
        {
            await base.Init();
            ThreadPool.SetMinThreads(Threads, Threads);
            _latch = new CountdownEvent(Threads);
        }

        [TearDown]
        protected override void ReleaseResources()
        {
            _latch.Dispose();
        }

        [Test]
        public async ValueTask TestConcurrentConnectionsAndChannelsMessageIntegrity()
        {
            int messagesReceived = 0;
            int tasksToRun = 16;
            int itemsPerBatch = 5000;
            var random = new Random();
            SemaphoreSlim semaphoreSlim = new SemaphoreSlim(0, 1);
            var hasher = SHA1.Create();

            ValueTask AsyncListener_Received(object sender, BasicDeliverEventArgs @event)
            {
                byte[] confirmHash = default;
                lock (hasher)
                {
                    confirmHash = hasher.ComputeHash(@event.Body.ToArray());
                }

                Assert.AreEqual(@event.BasicProperties.Headers["shahash"] as byte[], confirmHash);
                if (Interlocked.Increment(ref messagesReceived) == tasksToRun * itemsPerBatch)
                {
                    semaphoreSlim.Release();
                }

                return default;
            }

            using (var conn = await ConnFactory.CreateConnection())
            using (var conn2 = await ConnFactory.CreateConnection())
            using (var conn3 = await ConnFactory.CreateConnection())
            using (IModel subscriber = await conn.CreateModel())
            using (IModel subscriber2 = await conn.CreateModel())
            using (IModel subscriber3 = await conn.CreateModel())
            {
                await subscriber.ExchangeDeclare("test", ExchangeType.Topic, false, false);
                await subscriber.QueueDeclare("testqueue", false, false, true);
                await subscriber.QueueBind("testqueue", "test", "myawesome.routing.key");

                var asyncListener = new AsyncEventingBasicConsumer(subscriber);
                asyncListener.Received += AsyncListener_Received;
                var asyncListener2 = new AsyncEventingBasicConsumer(subscriber2);
                asyncListener2.Received += AsyncListener_Received;
                var asyncListener3 = new AsyncEventingBasicConsumer(subscriber3);
                asyncListener3.Received += AsyncListener_Received;

                await subscriber.BasicConsume("testqueue", true, "testconsumer", asyncListener);
                await subscriber2.BasicConsume("testqueue", true, "testconsumer2", asyncListener2);
                await subscriber3.BasicConsume("testqueue", true, "testconsumer3", asyncListener3);

                Task[] tasks = new Task[tasksToRun];
                for (int i = 0; i < tasksToRun; i++)
                {
                    tasks[i] = Task.Run(async () =>
                    {
                        using (var conn = await ConnFactory.CreateConnection())
                        using (IModel publisher = await conn.CreateModel())
                        {
                            await publisher.ConfirmSelect();
                            for (int i = 0; i < itemsPerBatch; i++)
                            {
                                byte[] payload = new byte[random.Next(1, 4097)];
                                random.NextBytes(payload);
                                IBasicProperties properties = publisher.CreateBasicProperties();
                                properties.Headers = new Dictionary<string, object>();
                                lock (hasher)
                                {
                                    properties.Headers.Add("shahash", hasher.ComputeHash(payload));
                                }
                                await publisher.BasicPublish("test", "myawesome.routing.key", false, properties, payload);
                            }
                            await publisher.WaitForConfirmsOrDie();
                        }
                    });
                }

                await Task.WhenAll(tasks);
                await semaphoreSlim.WaitAsync().ConfigureAwait(false);
            }
        }

        [Test]
        public async ValueTask TestConcurrentChannelOpenAndPublishingWithBlankMessages()
        {
            await TestConcurrentChannelOpenAndPublishingWithBody(Encoding.ASCII.GetBytes(string.Empty), 30);
        }

        [Test]
        public ValueTask TestConcurrentChannelOpenAndPublishingCase1()
        {
            return TestConcurrentChannelOpenAndPublishingWithBodyOfSize(64);
        }

        [Test]
        public async ValueTask TestConcurrentChannelOpenAndPublishingCase2()
        {
            await TestConcurrentChannelOpenAndPublishingWithBodyOfSize(256);
        }

        [Test]
        public async ValueTask TestConcurrentChannelOpenAndPublishingCase3()
        {
            await TestConcurrentChannelOpenAndPublishingWithBodyOfSize(1024);
        }

        [Test]
        public async ValueTask TestConcurrentChannelOpenAndPublishingCase4()
        {
            await TestConcurrentChannelOpenAndPublishingWithBodyOfSize(8192);
        }

        [Test]
        public async ValueTask TestConcurrentChannelOpenAndPublishingCase5()
        {
            await TestConcurrentChannelOpenAndPublishingWithBodyOfSize(32768, 20);
        }

        [Test]
        public async ValueTask TestConcurrentChannelOpenAndPublishingCase6()
        {
            await TestConcurrentChannelOpenAndPublishingWithBodyOfSize(100000, 15);
        }

        [Test]
        public async ValueTask TestConcurrentChannelOpenAndPublishingCase7()
        {
            // surpasses default frame size
            await TestConcurrentChannelOpenAndPublishingWithBodyOfSize(131072, 12);
        }

        [Test]
        public async ValueTask TestConcurrentChannelOpenAndPublishingCase8()
        {
            await TestConcurrentChannelOpenAndPublishingWithBodyOfSize(270000, 10);
        }

        [Test]
        public async ValueTask TestConcurrentChannelOpenAndPublishingCase9()
        {
            await TestConcurrentChannelOpenAndPublishingWithBodyOfSize(540000, 6);
        }

        [Test]
        public async ValueTask TestConcurrentChannelOpenAndPublishingCase10()
        {
            await TestConcurrentChannelOpenAndPublishingWithBodyOfSize(1000000, 2);
        }

        [Test]
        public async ValueTask TestConcurrentChannelOpenAndPublishingCase11()
        {
            await TestConcurrentChannelOpenAndPublishingWithBodyOfSize(1500000, 1);
        }

        [Test]
        public async ValueTask TestConcurrentChannelOpenAndPublishingCase12()
        {
            await TestConcurrentChannelOpenAndPublishingWithBodyOfSize(128000000, 1);
        }

        [Test]
        public async ValueTask TestConcurrentChannelOpenCloseLoop()
        {
            await TestConcurrentChannelOperations(async (conn) =>
            {
                IModel ch = await conn.CreateModel();
                await ch.Close();
            }, 50);
        }

        internal ValueTask TestConcurrentChannelOpenAndPublishingWithBodyOfSize(int length)
        {
            return TestConcurrentChannelOpenAndPublishingWithBodyOfSize(length, 30);
        }

        internal ValueTask TestConcurrentChannelOpenAndPublishingWithBodyOfSize(int length, int iterations)
        {
            string s = "payload";
            if (length > s.Length)
            {
                s.PadRight(length);
            }

            return TestConcurrentChannelOpenAndPublishingWithBody(Encoding.ASCII.GetBytes(s), iterations);
        }

        internal ValueTask TestConcurrentChannelOpenAndPublishingWithBody(byte[] body, int iterations)
        {
            return TestConcurrentChannelOperations(async (conn) =>
            {
                // publishing on a shared channel is not supported
                // and would missing the point of this test anyway
                using (IModel ch = await Conn.CreateModel())
                {
                    await ch.ConfirmSelect();
                    foreach (int j in Enumerable.Range(0, 200))
                    {
                        await ch.BasicPublish(exchange: "", routingKey: "_______", basicProperties: ch.CreateBasicProperties(), body: body);
                    }

                    await ch.WaitForConfirms(TimeSpan.FromSeconds(40));
                }
            }, iterations);
        }

        internal ValueTask TestConcurrentChannelOperations(Func<IConnection, ValueTask> actions,
            int iterations)
        {
            return TestConcurrentChannelOperations(actions, iterations, _completionTimeout);
        }

        internal async ValueTask TestConcurrentChannelOperations(Func<IConnection, ValueTask> actions,
            int iterations, TimeSpan timeout)
        {
            Task[] tasks = Enumerable.Range(0, Threads).Select(async x =>
            {
                foreach (int j in Enumerable.Range(0, iterations))
                {
                    await actions(Conn).ConfigureAwait(false);
                }

                _latch.Signal();
            }).ToArray();

            try
            {
                await Task.WhenAll(tasks).TimeoutAfter(timeout);
            }
            catch(TimeoutException)
            {

            }

            Assert.IsTrue(_latch.IsSet);
            // incorrect frame interleaving in these tests will result
            // in an unrecoverable connection-level exception, thus
            // closing the connection
            Assert.IsTrue(Conn.IsOpen);
        }
    }
}
