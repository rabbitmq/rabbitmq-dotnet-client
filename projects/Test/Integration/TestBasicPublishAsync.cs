// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestBasicPublishAsync : IntegrationFixture
    {
        public TestBasicPublishAsync(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestQueuePurgeAsync()
        {
            const int messageCount = 1024;

            var publishSyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            QueueDeclareOk q = await _channel.QueueDeclareAsync(string.Empty, false, false, true);

            var publishTask = Task.Run(async () =>
            {
                byte[] body = GetRandomBody(512);
                for (int i = 0; i < messageCount; i++)
                {
                    await _channel.BasicPublishAsync(string.Empty, q, body);
                }
                publishSyncSource.SetResult(true);
            });

            Assert.True(await publishSyncSource.Task);
            Assert.Equal((uint)messageCount, await _channel.QueuePurgeAsync(q));
        }

        [Fact]
        public async Task TestBasicReturnAsync()
        {
            string routingKey = Guid.NewGuid().ToString();
            try
            {
                await _channel.BasicPublishAsync(exchange: string.Empty, routingKey: routingKey,
                    mandatory: true, body: GetRandomBody());
            }
            catch (PublishReturnException prex)
            {
                Assert.True(prex.IsReturn);
                Assert.NotNull(prex.Exchange);
                Assert.Equal(string.Empty, prex.Exchange);
                Assert.NotNull(prex.RoutingKey);
                Assert.Equal(routingKey, prex.RoutingKey);
                Assert.NotEqual(0, prex.ReplyCode);
                Assert.NotNull(prex.ReplyText);
                Assert.Equal("NO_ROUTE", prex.ReplyText);
                
            }
        }
    }
}
