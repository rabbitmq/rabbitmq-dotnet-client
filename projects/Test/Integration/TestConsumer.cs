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
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestConsumer : IntegrationFixture
    {
        private readonly byte[] _body = GetRandomBody(64);
        private readonly ShutdownEventArgs _closeArgs = new ShutdownEventArgs(ShutdownInitiator.Application, Constants.ReplySuccess, "normal shutdown");

        public TestConsumer(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void TestBasicRoundtrip()
        {
            QueueDeclareOk q = _channel.QueueDeclare();
            _channel.BasicPublish("", q.QueueName, _body);
            var consumer = new EventingBasicConsumer(_channel);
            var are = new AutoResetEvent(false);
            consumer.Received += (o, a) =>
                {
                    are.Set();
                };
            string tag = _channel.BasicConsume(q.QueueName, true, consumer);
            // ensure we get a delivery
            bool waitRes = are.WaitOne(2000);
            Assert.True(waitRes);
            // unsubscribe and ensure no further deliveries
            _channel.BasicCancel(tag);
            _channel.BasicPublish("", q.QueueName, _body);
            bool waitResFalse = are.WaitOne(2000);
            Assert.False(waitResFalse);
        }

        [Fact]
        public void TestBasicRoundtripNoWait()
        {
            QueueDeclareOk q = _channel.QueueDeclare();
            _channel.BasicPublish("", q.QueueName, _body);
            var consumer = new EventingBasicConsumer(_channel);
            var are = new AutoResetEvent(false);
            consumer.Received += (o, a) =>
                {
                    are.Set();
                };
            string tag = _channel.BasicConsume(q.QueueName, true, consumer);
            // ensure we get a delivery
            bool waitRes = are.WaitOne(2000);
            Assert.True(waitRes);
            // unsubscribe and ensure no further deliveries
            _channel.BasicCancelNoWait(tag);
            _channel.BasicPublish("", q.QueueName, _body);
            bool waitResFalse = are.WaitOne(2000);
            Assert.False(waitResFalse);
        }

        [Fact]
        public void ConcurrentEventingTestForReceived()
        {
            const int NumberOfThreads = 4;
            const int NumberOfRegistrations = 5000;

            var called = new byte[NumberOfThreads * NumberOfRegistrations];

            QueueDeclareOk q = _channel.QueueDeclare();
            var consumer = new EventingBasicConsumer(_channel);
            _channel.BasicConsume(q.QueueName, true, consumer);
            var countdownEvent = new CountdownEvent(NumberOfThreads);
            for (int i = 0; i < NumberOfThreads; i++)
            {
                int threadIndex = i;
                Task.Run(() =>
                {
                    int start = threadIndex * NumberOfRegistrations;
                    for (int j = start; j < start + NumberOfRegistrations; j++)
                    {
                        int receivedIndex = j;
                        consumer.Received += (sender, eventArgs) =>
                        {
                            called[receivedIndex] = 1;
                        };
                    }
                    countdownEvent.Signal();
                });
            }

            countdownEvent.Wait();

            // Add last receiver
            var are = new AutoResetEvent(false);
            consumer.Received += (o, a) =>
            {
                are.Set();
            };

            // Send message
            _channel.BasicPublish("", q.QueueName, ReadOnlyMemory<byte>.Empty);
            are.WaitOne(TimingFixture.TestTimeout);

            // Check received messages
            Assert.Equal(-1, called.AsSpan().IndexOf((byte)0));
        }
    }
}
