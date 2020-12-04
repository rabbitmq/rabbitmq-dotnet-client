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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;
using RabbitMQ.Client.client.impl.Channel;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestConcurrentAccessWithSharedConnection : IntegrationFixture
    {
        internal const int Threads = 32;
        internal CountdownEvent _latch;
        internal TimeSpan _completionTimeout = TimeSpan.FromSeconds(90);

        [SetUp]
        public override async Task Init()
        {
            await base.Init().ConfigureAwait(false);
            ThreadPool.SetMinThreads(Threads, Threads);
            _latch = new CountdownEvent(Threads);
        }

        [TearDown]
        protected override void ReleaseResources()
        {
            _latch.Dispose();
        }

        [Test]
        public void TestConcurrentChannelOpenAndPublishingWithBlankMessages()
        {
            TestConcurrentChannelOpenAndPublishingWithBody(Encoding.ASCII.GetBytes(string.Empty), 30);
        }

        [Test]
        public void TestConcurrentChannelOpenAndPublishingCase1()
        {
            TestConcurrentChannelOpenAndPublishingWithBodyOfSize(64);
        }

        [Test]
        public void TestConcurrentChannelOpenAndPublishingCase2()
        {
            TestConcurrentChannelOpenAndPublishingWithBodyOfSize(256);
        }

        [Test]
        public void TestConcurrentChannelOpenAndPublishingCase3()
        {
            TestConcurrentChannelOpenAndPublishingWithBodyOfSize(1024);
        }

        [Test]
        public void TestConcurrentChannelOpenAndPublishingCase4()
        {
            TestConcurrentChannelOpenAndPublishingWithBodyOfSize(8192);
        }

        [Test]
        public void TestConcurrentChannelOpenAndPublishingCase5()
        {
            TestConcurrentChannelOpenAndPublishingWithBodyOfSize(32768, 20);
        }

        [Test]
        public void TestConcurrentChannelOpenAndPublishingCase6()
        {
            TestConcurrentChannelOpenAndPublishingWithBodyOfSize(100000, 15);
        }

        [Test]
        public void TestConcurrentChannelOpenAndPublishingCase7()
        {
            // surpasses default frame size
            TestConcurrentChannelOpenAndPublishingWithBodyOfSize(131072, 12);
        }

        [Test]
        public void TestConcurrentChannelOpenAndPublishingCase8()
        {
            TestConcurrentChannelOpenAndPublishingWithBodyOfSize(270000, 10);
        }

        [Test]
        public void TestConcurrentChannelOpenAndPublishingCase9()
        {
            TestConcurrentChannelOpenAndPublishingWithBodyOfSize(540000, 6);
        }

        [Test]
        public void TestConcurrentChannelOpenAndPublishingCase10()
        {
            TestConcurrentChannelOpenAndPublishingWithBodyOfSize(1000000, 2);
        }

        [Test]
        public void TestConcurrentChannelOpenAndPublishingCase11()
        {
            TestConcurrentChannelOpenAndPublishingWithBodyOfSize(1500000, 1);
        }

        [Test]
        public void TestConcurrentChannelOpenAndPublishingCase12()
        {
            TestConcurrentChannelOpenAndPublishingWithBodyOfSize(128000000, 1);
        }

        [Test]
        public void TestConcurrentChannelOpenCloseLoop()
        {
            TestConcurrentChannelOperations(async conn =>
            {
                IChannel channel = await conn.CreateChannelAsync().ConfigureAwait(false);
                await channel.CloseAsync().ConfigureAwait(false);
            }, 50);
        }

        internal void TestConcurrentChannelOpenAndPublishingWithBodyOfSize(int length, int iterations = 30)
        {
            string s = "payload";
            if (length > s.Length)
            {
                s.PadRight(length);
            }

            TestConcurrentChannelOpenAndPublishingWithBody(Encoding.ASCII.GetBytes(s), iterations);
        }

        internal void TestConcurrentChannelOpenAndPublishingWithBody(byte[] body, int iterations)
        {
            TestConcurrentChannelOperations(async conn =>
            {
                // publishing on a shared channel is not supported
                // and would missing the point of this test anyway
                IChannel ch = await _conn.CreateChannelAsync().ConfigureAwait(false);
                await ch.ActivatePublishTagsAsync().ConfigureAwait(false);
                for (int i = 0; i < 200; i++)
                {
                    await ch.PublishMessageAsync("", "_______", null, body).ConfigureAwait(false);
                }

                ch.WaitForConfirms(TimeSpan.FromSeconds(40));
            }, iterations);
        }

        internal void TestConcurrentChannelOperations(Func<IConnection, Task> actions, int iterations)
        {
            TestConcurrentChannelOperations(actions, iterations, _completionTimeout);
        }

        internal void TestConcurrentChannelOperations(Func<IConnection, Task> actions, int iterations, TimeSpan timeout)
        {
            Task[] tasks = Enumerable.Range(0, Threads).Select(x =>
            {
                return Task.Run(async () =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        await actions(_conn).ConfigureAwait(false);
                    }

                    _latch.Signal();
                });
            }).ToArray();

            Assert.IsTrue(_latch.Wait(timeout));
            // incorrect frame interleaving in these tests will result
            // in an unrecoverable connection-level exception, thus
            // closing the connection
            Assert.IsTrue(_conn.IsOpen);
        }
    }
}
