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

using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.SequentialIntegration
{
    public class TestConnectionBlockedChannelLeak : SequentialIntegrationFixture
    {
        public TestConnectionBlockedChannelLeak(ITestOutputHelper output) : base(output)
        {
        }

        public override async Task InitializeAsync()
        {
            await UnblockAsync();
        }

        public override async Task DisposeAsync()
        {
            await UnblockAsync();
            await base.DisposeAsync();
        }

        [Fact]
        public async Task TestConnectionBlockedChannelLeak_GH1573()
        {
            await BlockAsync();

            var connectionBlockedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var connectionUnblockedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var cts = new CancellationTokenSource(WaitSpan);
            using CancellationTokenRegistration ctr = cts.Token.Register(() =>
            {
                connectionBlockedTcs.TrySetCanceled();
                connectionUnblockedTcs.TrySetCanceled();
            });

            _connFactory = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                ClientProvidedName = _testDisplayName,
                ContinuationTimeout = TimeSpan.FromSeconds(2)
            };
            _conn = await _connFactory.CreateConnectionAsync();
            _channel = await _conn.CreateChannelAsync();

            string exchangeName = GenerateExchangeName();

            _conn.ConnectionBlocked += (object sender, ConnectionBlockedEventArgs args) =>
            {
                connectionBlockedTcs.SetResult(true);
            };

            _conn.ConnectionUnblocked += (object sender, EventArgs ea) =>
            {
                connectionUnblockedTcs.SetResult(true);
            };

            async Task ExchangeDeclareAndPublish()
            {
                using (IChannel publishChannel = await _conn.CreateChannelAsync())
                {
                    await publishChannel.ExchangeDeclareAsync(exchangeName, ExchangeType.Direct, autoDelete: true);
                    await publishChannel.BasicPublishAsync(exchangeName, exchangeName, true, GetRandomBody());
                    await publishChannel.CloseAsync();
                }
            }
            await Assert.ThrowsAnyAsync<OperationCanceledException>(ExchangeDeclareAndPublish);

            for (int i = 1; i <= 5; i++)
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _conn.CreateChannelAsync());
            }

            // Note: debugging
            // var rmq = new RabbitMQCtl(_output);
            // string output = await rmq.ExecRabbitMQCtlAsync("list_channels");
            // _output.WriteLine("CHANNELS 0: {0}", output);

            await UnblockAsync();

            // output = await rmq.ExecRabbitMQCtlAsync("list_channels");
            // _output.WriteLine("CHANNELS 1: {0}", output);

            Assert.True(await connectionBlockedTcs.Task, "Blocked notification not received.");
            Assert.True(await connectionUnblockedTcs.Task, "Unblocked notification not received.");
        }
    }
}
