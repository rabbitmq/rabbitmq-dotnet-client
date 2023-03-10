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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using RabbitMQ.Client.Impl;

using Xunit;
using Xunit.Abstractions;

namespace RabbitMQ.Client.Unit
{

    public class TestPublisherConfirms : IntegrationFixture
    {
        private const string QueueName = "RabbitMQ.Client.Unit.TestPublisherConfirms";
        private readonly byte[] _body;

        public TestPublisherConfirms(ITestOutputHelper output) : base(output)
        {
            var rnd = new Random();
            _body = new byte[4096];
            rnd.NextBytes(_body);

        }

        [Fact]
        public void TestWaitForConfirmsWithoutTimeout()
        {
            TestWaitForConfirms(200, (ch) =>
            {
                Assert.True(ch.WaitForConfirmsAsync().GetAwaiter().GetResult());
            });
        }

        [Fact]
        public void TestWaitForConfirmsWithTimeout()
        {
            TestWaitForConfirms(200, (ch) =>
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4)))
                {
                    Assert.True(ch.WaitForConfirmsAsync(cts.Token).GetAwaiter().GetResult());
                }
            });
        }

        [Fact]
        public void TestWaitForConfirmsWithTimeout_AllMessagesAcked_WaitingHasTimedout_ReturnTrue()
        {
            TestWaitForConfirms(10000, (ch) =>
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1)))
                {
                    Assert.Throws<TaskCanceledException>(() => ch.WaitForConfirmsAsync(cts.Token).GetAwaiter().GetResult());
                }
            });
        }

        [Fact]
        public void TestWaitForConfirmsWithTimeout_MessageNacked_WaitingHasTimedout_ReturnFalse()
        {
            TestWaitForConfirms(2000, (ch) =>
            {
                IChannel actualModel = ((AutorecoveringModel)ch).InnerChannel;
                actualModel
                    .GetType()
                    .GetMethod("HandleAckNack", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(actualModel, new object[] { 10UL, false, true });

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4)))
                {
                    Assert.False(ch.WaitForConfirmsAsync(cts.Token).GetAwaiter().GetResult());
                }
            });
        }

        [Fact]
        public async Task TestWaitForConfirmsWithEvents()
        {
            using (IChannel ch = _conn.CreateModel())
            {
                ch.ConfirmSelect();

                ch.QueueDeclare(QueueName);
                int n = 200;
                // number of event handler invocations
                int c = 0;

                ch.BasicAcks += (_, args) =>
                {
                    Interlocked.Increment(ref c);
                };
                try
                {
                    for (int i = 0; i < n; i++)
                    {
                        ch.BasicPublish("", QueueName, _encoding.GetBytes("msg"));
                    }
                    await ch.WaitForConfirmsAsync().ConfigureAwait(false);

                    // Note: number of event invocations is not guaranteed
                    // to be equal to N because acks can be batched,
                    // so we primarily care about event handlers being invoked
                    // in this test
                    Assert.True(c >= 1);
                }
                finally
                {
                    ch.QueueDelete(QueueName);
                }
            }
        }

        protected void TestWaitForConfirms(int numberOfMessagesToPublish, Action<IChannel> fn)
        {
            using (IChannel ch = _conn.CreateModel())
            {
                ch.ConfirmSelect();
                ch.QueueDeclare(QueueName);

                for (int i = 0; i < numberOfMessagesToPublish; i++)
                {
                    ch.BasicPublish("", QueueName, _body);
                }

                try
                {
                    fn(ch);
                }
                finally
                {
                    ch.QueueDelete(QueueName);
                }
            }
        }
    }
}
