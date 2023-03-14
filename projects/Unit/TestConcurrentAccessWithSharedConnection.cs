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
using System.Threading;
using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

namespace RabbitMQ.Client.Unit
{

    public class TestConcurrentAccessWithSharedConnection : IntegrationFixture
    {
        internal const int Threads = 32;
        internal CountdownEvent _latch;
        internal TimeSpan _completionTimeout = TimeSpan.FromSeconds(90);

        public TestConcurrentAccessWithSharedConnection(ITestOutputHelper output) : base(output)
        {
        }

        protected override void SetUp()
        {
            base.SetUp();
            ThreadPool.SetMinThreads(Threads, Threads);
            _latch = new CountdownEvent(Threads);
        }

        public override void Dispose()
        {
            base.Dispose();
            _latch.Dispose();
        }

        [Fact]
        public void TestConcurrentChannelOpenAndPublishingWithBlankMessages()
        {
            TestConcurrentChannelOpenAndPublishingWithBody(Array.Empty<byte>(), 30);
        }

        [Fact]
        public void TestConcurrentChannelOpenAndPublishingSize64()
        {
            TestConcurrentChannelOpenAndPublishingWithBodyOfSize(64);
        }

        [Fact]
        public void TestConcurrentChannelOpenAndPublishingSize256()
        {
            TestConcurrentChannelOpenAndPublishingWithBodyOfSize(256);
        }

        [Fact]
        public void TestConcurrentChannelOpenAndPublishingSize1024()
        {
            TestConcurrentChannelOpenAndPublishingWithBodyOfSize(1024);
        }

        [Fact]
        public Task TestConcurrentChannelOpenAndPublishingWithBlankMessagesAsync()
        {
            return TestConcurrentChannelOpenAndPublishingWithBodyAsync(Array.Empty<byte>(), 30);
        }

        [Fact]
        public Task TestConcurrentChannelOpenAndPublishingSize64Async()
        {
            return TestConcurrentChannelOpenAndPublishingWithBodyOfSizeAsync(64);
        }

        [Fact]
        public Task TestConcurrentChannelOpenAndPublishingSize256Async()
        {
            return TestConcurrentChannelOpenAndPublishingWithBodyOfSizeAsync(256);
        }

        [Fact]
        public Task TestConcurrentChannelOpenAndPublishingSize1024Async()
        {
            return TestConcurrentChannelOpenAndPublishingWithBodyOfSizeAsync(1024);
        }

        [Fact]
        public void TestConcurrentChannelOpenCloseLoop()
        {
            TestConcurrentChannelOperations((conn) =>
            {
                IChannel ch = conn.CreateChannel();
                ch.Close();
            }, 50);
        }

        internal void TestConcurrentChannelOpenAndPublishingWithBodyOfSize(int length, int iterations = 30)
        {
            TestConcurrentChannelOpenAndPublishingWithBody(new byte[length], iterations);
        }

        internal Task TestConcurrentChannelOpenAndPublishingWithBodyOfSizeAsync(int length, int iterations = 30)
        {
            return TestConcurrentChannelOpenAndPublishingWithBodyAsync(new byte[length], iterations);
        }

        internal void TestConcurrentChannelOpenAndPublishingWithBody(byte[] body, int iterations)
        {
            TestConcurrentChannelOperations((conn) =>
            {
                // publishing on a shared channel is not supported
                // and would missing the point of this test anyway
                IChannel ch = _conn.CreateChannel();
                ch.ConfirmSelect();
                for (int j = 0; j < 200; j++)
                {
                    ch.BasicPublish("", "_______", body);
                }
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(40));
                ch.WaitForConfirmsAsync(cts.Token).GetAwaiter().GetResult();
            }, iterations);
        }

        internal Task TestConcurrentChannelOpenAndPublishingWithBodyAsync(byte[] body, int iterations)
        {
            return TestConcurrentChannelOperationsAsync(async (conn) =>
            {
                // publishing on a shared channel is not supported
                // and would missing the point of this test anyway
                IChannel ch = _conn.CreateChannel();
                ch.ConfirmSelect();
                for (int j = 0; j < 200; j++)
                {
                    await ch.BasicPublishAsync("", "_______", body).ConfigureAwait(false);
                }
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(40));
                await ch.WaitForConfirmsAsync(cts.Token).ConfigureAwait(false);
            }, iterations);
        }

        internal void TestConcurrentChannelOperations(Action<IConnection> actions,
            int iterations)
        {
            TestConcurrentChannelOperations(actions, iterations, _completionTimeout);
        }

        internal Task TestConcurrentChannelOperationsAsync(Func<IConnection, Task> actions, int iterations)
        {
            return TestConcurrentChannelOperationsAsync(actions, iterations, _completionTimeout);
        }

        internal void TestConcurrentChannelOperations(Action<IConnection> actions,
            int iterations, TimeSpan timeout)
        {
            _ = Enumerable.Range(0, Threads).Select(x =>
            {
                return Task.Run(() =>
                {
                    for (int j = 0; j < iterations; j++)
                    {
                        actions(_conn);
                    }

                    _latch.Signal();
                });
            }).ToArray();

            Assert.True(_latch.Wait(timeout));
            // incorrect frame interleaving in these tests will result
            // in an unrecoverable connection-level exception, thus
            // closing the connection
            Assert.True(_conn.IsOpen);
        }

        internal async Task TestConcurrentChannelOperationsAsync(Func<IConnection, Task> actions,
            int iterations, TimeSpan timeout)
        {
            await Task.WhenAll(Enumerable.Range(0, Threads).Select(x =>
            {
                return Task.Run(() =>
                {
                    for (int j = 0; j < iterations; j++)
                    {
                        actions(_conn);
                    }

                    _latch.Signal();
                });
            }).ToArray());

            Assert.True(_latch.Wait(timeout));
            // incorrect frame interleaving in these tests will result
            // in an unrecoverable connection-level exception, thus
            // closing the connection
            Assert.True(_conn.IsOpen);
        }
    }
}
