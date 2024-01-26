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
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.SequentialIntegration
{
    public class TestConnectionBlocked : SequentialIntegrationFixture
    {
        public TestConnectionBlocked(ITestOutputHelper output) : base(output)
        {
        }

        public override async Task DisposeAsync()
        {
            await UnblockAsync();
            await base.DisposeAsync();
        }

        [Fact]
        public async Task TestConnectionBlockedNotification()
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _conn.ConnectionBlocked += (object sender, ConnectionBlockedEventArgs args) =>
            {
                UnblockAsync();
            };

            _conn.ConnectionUnblocked += (object sender, EventArgs ea) =>
            {
                tcs.SetResult(true);
            };

            await BlockAsync(_channel);
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.True(await tcs.Task, "Unblock notification not received.");
        }

        [Fact]
        public async Task TestDisposeOnBlockedConnectionDoesNotHang()
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            await BlockAsync(_channel);

            Task disposeTask = Task.Run(async () =>
            {
                await _conn.AbortAsync();
                _conn.Dispose();
                _conn = null;
                tcs.SetResult(true);
            });

            Task anyTask = Task.WhenAny(tcs.Task, disposeTask);
            await anyTask.WaitAsync(LongWaitSpan);
            bool disposeSuccess = await tcs.Task;
            Assert.True(disposeSuccess, "Dispose must have finished within 20 seconds after starting");
        }
    }
}
