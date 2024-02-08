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
using System.Threading.Tasks;
using RabbitMQ.Client;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestConcurrentAccessWithSharedConnection : IntegrationFixture
    {
        public TestConcurrentAccessWithSharedConnection(ITestOutputHelper output)
            : base(output)
        {
        }

        public override async Task InitializeAsync()
        {
            _connFactory = CreateConnectionFactory();
            _conn = await _connFactory.CreateConnectionAsync();
            // NB: not creating _channel because this test suite doesn't use it.
            Assert.Null(_channel);
        }

        [Fact]
        public async Task TestConcurrentChannelOpenCloseLoop()
        {
            await TestConcurrentChannelOperationsAsync(async (conn) =>
            {
                using (IChannel ch = await conn.CreateChannelAsync())
                {
                    await ch.CloseAsync();
                }
            }, 50);
        }

        private async Task TestConcurrentChannelOperationsAsync(Func<IConnection, Task> action, int iterations)
        {
            var tasks = new List<Task>();
            for (int i = 0; i < _processorCount; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    for (int j = 0; j < iterations; j++)
                    {
                        await action(_conn);
                    }
                }));
            }

            Task whenTask = Task.WhenAll(tasks);
            await whenTask.WaitAsync(LongWaitSpan);
            Assert.True(whenTask.IsCompleted);
            Assert.False(whenTask.IsCanceled);
            Assert.False(whenTask.IsFaulted);

            // incorrect frame interleaving in these tests will result
            // in an unrecoverable connection-level exception, thus
            // closing the connection
            Assert.True(_conn.IsOpen);
        }
    }
}
