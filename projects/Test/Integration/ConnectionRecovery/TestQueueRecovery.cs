// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing.Impl;
using Xunit;
using Xunit.Abstractions;
using QueueDeclareOk = RabbitMQ.Client.QueueDeclareOk;

namespace Test.Integration.ConnectionRecovery
{
    public class TestQueueRecovery : TestConnectionRecoveryBase
    {
        public TestQueueRecovery(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestQueueRecoveryWithManyQueues()
        {
            var qs = new List<string>();
            int n = 1024;
            for (int i = 0; i < n; i++)
            {
                QueueDeclareOk q = await _channel.QueueDeclareAsync(GenerateQueueName(), false, false, false);
                qs.Add(q.QueueName);
            }
            await CloseAndWaitForRecoveryAsync();
            Assert.True(_channel.IsOpen);
            foreach (string q in qs)
            {
                await AssertQueueRecoveryAsync(_channel, q, false);
                await _channel.QueueDeleteAsync(q);
            }
        }

        [Fact]
        public async Task TestServerNamedQueueRecovery()
        {
            string q = (await _channel.QueueDeclareAsync("", false, false, false)).QueueName;
            string x = "amq.fanout";
            await _channel.QueueBindAsync(q, x, "");

            string nameBefore = q;
            string nameAfter = null;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var connection = (AutorecoveringConnection)_conn;
            connection.RecoverySucceededAsync += (source, ea) =>
            {
                tcs.SetResult(true);
                return Task.CompletedTask;
            };
            connection.QueueNameChangedAfterRecoveryAsync += (source, ea) =>
            {
                nameAfter = ea.NameAfter;
                return Task.CompletedTask;
            };

            await CloseAndWaitForRecoveryAsync();
            await WaitAsync(tcs, "recovery succeeded");

            Assert.NotNull(nameAfter);
            Assert.StartsWith("amq.", nameBefore);
            Assert.StartsWith("amq.", nameAfter);
            Assert.NotEqual(nameBefore, nameAfter);

            await _channel.QueueDeclarePassiveAsync(nameAfter);
        }
    }
}
