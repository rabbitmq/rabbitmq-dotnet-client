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
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestMessageIntegrity
    {
        [ThreadStatic]
        private static SHA1 _hasher = SHA1.Create();

        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        public void SingleThreaded(int messagesToSend)
        {
            _hasher = _hasher ?? SHA1.Create();
            Random _randomizer = new Random();
            ManualResetEventSlim allMessagesReceived = new ManualResetEventSlim();
            int messagesReceived = 0;
            var connFactory = new ConnectionFactory()
            {
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                AutomaticRecoveryEnabled = false,
                DispatchConsumersAsync = true
            };

            using (IConnection conn = connFactory.CreateConnection())
            using (IConnection subConn = connFactory.CreateConnection())
            {
                using (IModel subModel = subConn.CreateModel())
                {
                    string exchange = $"SingleThreaderTest_{Guid.NewGuid()}";
                    string queueName = $"SingleThreaderTest_{Guid.NewGuid()}";
                    var subscriber = new AsyncEventingBasicConsumer(subModel);
                    subscriber.Received += Subscriber_Received;
                    subModel.ExchangeDeclare(exchange, ExchangeType.Topic, false, true);
                    subModel.QueueDeclare(queueName, false, false, true);
                    subModel.QueueBind(queueName, exchange, "");
                    subModel.BasicConsume(queueName, true, subscriber);

                    using (IModel model = conn.CreateModel())
                    {
                        for (int i = 0; i < messagesToSend; i++)
                        {
                            byte[] messagePayload = new byte[_randomizer.Next(64, 512)];
                            _randomizer.NextBytes(messagePayload);
                            byte[] hash = _hasher.ComputeHash(messagePayload);
                            IBasicProperties props = model.CreateBasicProperties();
                            props.Headers = new Dictionary<string, object>();
                            props.Headers.Add("hash", hash);
                            model.BasicPublish(exchange, "", props, messagePayload);
                        }
                    }

                    allMessagesReceived.Wait();
                }
            }

            Task Subscriber_Received(object sender, BasicDeliverEventArgs @event)
            {
                _hasher = _hasher ?? SHA1.Create();
                byte[] hash = @event.BasicProperties.Headers["hash"] as byte[];
                Assert.IsTrue(hash.AsSpan().SequenceEqual(_hasher.ComputeHash(@event.Body.ToArray())));
                if (messagesToSend == Interlocked.Increment(ref messagesReceived))
                {
                    allMessagesReceived.Set();
                }

                return Task.CompletedTask;
            }
        }



        [TestCase(100, 2)]
        [TestCase(100, 4)]
        [TestCase(100, 8)]
        [TestCase(100, 16)]
        [TestCase(1000, 2)]
        [TestCase(1000, 4)]
        [TestCase(1000, 8)]
        [TestCase(1000, 16)]
        [TestCase(10000, 2)]
        [TestCase(10000, 4)]
        [TestCase(10000, 8)]
        [TestCase(10000, 16)]
        public void MultiThreaded(int messagesToSend, int threads)
        {
            Random _randomizer = new Random();
            ThreadPool.SetMinThreads(16 * Environment.ProcessorCount, 16 * Environment.ProcessorCount);
            ManualResetEventSlim allMessagesReceived = new ManualResetEventSlim();
            int messagesReceived = 0;
            var connFactory = new ConnectionFactory()
            {
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                AutomaticRecoveryEnabled = false,
                DispatchConsumersAsync = true
            };

            using (IConnection conn = connFactory.CreateConnection())
            using (IConnection subConn = connFactory.CreateConnection())
            using (IModel setupModel = conn.CreateModel())
            {
                string exchange = $"MultiThreadedTest_{Guid.NewGuid()}";
                string queueName = $"MultiThreadedTest_{Guid.NewGuid()}";

                setupModel.ExchangeDeclare(exchange, ExchangeType.Topic, false, true);
                setupModel.QueueDeclare(queueName, false, false, true);
                setupModel.QueueBind(queueName, exchange, "");

                List<Task> consumerTasks = new List<Task>();
                List<Task> publisherTasks = new List<Task>();

                for (int i = 0; i < threads; i++)
                {
                    consumerTasks.Add(Task.Run(() =>
                    {
                        using (IModel subModel = subConn.CreateModel())
                        {
                            var subscriber = new AsyncEventingBasicConsumer(subModel);
                            subscriber.Received += Subscriber_Received;
                            subModel.BasicConsume(queueName, true, subscriber);
                            allMessagesReceived.Wait();
                        }
                    }));

                    publisherTasks.Add(Task.Run(() =>
                    {
                        using (IModel model = conn.CreateModel())
                        {
                            _hasher = _hasher ?? SHA1.Create();
                            for (int x = 0; x < messagesToSend; x++)
                            {
                                byte[] messagePayload = new byte[_randomizer.Next(64, 512)];
                                _randomizer.NextBytes(messagePayload);
                                byte[] hash = _hasher.ComputeHash(messagePayload);
                                IBasicProperties props = model.CreateBasicProperties();
                                props.Headers = new Dictionary<string, object>();
                                props.Headers.Add("hash", hash);
                                model.BasicPublish(exchange, "", props, messagePayload);
                            }
                        }
                    }));
                }
                allMessagesReceived.Wait();
            }

            Task Subscriber_Received(object sender, BasicDeliverEventArgs @event)
            {
                _hasher = _hasher ?? SHA1.Create();
                byte[] hash = @event.BasicProperties.Headers["hash"] as byte[];
                Assert.IsTrue(hash.AsSpan().SequenceEqual(_hasher.ComputeHash(@event.Body.ToArray())));
                if ((messagesToSend * threads) == Interlocked.Increment(ref messagesReceived))
                {
                    allMessagesReceived.Set();
                }

                return Task.CompletedTask;
            }
        }
    }
}
