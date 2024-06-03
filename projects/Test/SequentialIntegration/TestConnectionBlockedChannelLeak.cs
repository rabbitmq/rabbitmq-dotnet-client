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
            _connFactory = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                ClientProvidedName = _testDisplayName,
                ContinuationTimeout = TimeSpan.FromSeconds(2)
            };
            _conn = await _connFactory.CreateConnectionAsync();
            _channel = await _conn.CreateChannelAsync();
        }

        public override async Task DisposeAsync()
        {
            await UnblockAsync();
            await base.DisposeAsync();
        }

        [Fact]
        public async Task TestConnectionBlockedChannelLeak_GH1573()
        {
            string exchangeName = GenerateExchangeName();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var cts = new CancellationTokenSource(WaitSpan);
            using CancellationTokenRegistration ctr = cts.Token.Register(() =>
            {
                tcs.TrySetCanceled();
            });

            _conn.ConnectionBlocked += (object sender, ConnectionBlockedEventArgs args) =>
            {
                UnblockAsync();
            };

            _conn.ConnectionUnblocked += (object sender, EventArgs ea) =>
            {
                tcs.SetResult(true);
            };

            await BlockAsync(_channel);

            using (IChannel publishChannel = await _conn.CreateChannelAsync())
            {
                await publishChannel.ExchangeDeclareAsync(exchangeName, ExchangeType.Direct, autoDelete: true);
                await publishChannel.BasicPublishAsync(exchangeName, exchangeName, GetRandomBody(), mandatory: true);
                await publishChannel.CloseAsync();
            }

            var channels = new List<IChannel>();
            for (int i = 1; i <= 5; i++)
            {
                IChannel c = await _conn.CreateChannelAsync();
                channels.Add(c);
            }

            /*
             * Note:
             * This wait probably isn't necessary, if the above CreateChannelAsync
             * calls were to timeout, we'd get exceptions on the await
             */
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Note: debugging
            // var rmq = new RabbitMQCtl(_output);
            // string output = await rmq.ExecRabbitMQCtlAsync("list_channels");
            // _output.WriteLine("CHANNELS 0: {0}", output);

            await UnblockAsync();

            // output = await rmq.ExecRabbitMQCtlAsync("list_channels");
            // _output.WriteLine("CHANNELS 1: {0}", output);

            Assert.True(await tcs.Task, "Unblock notification not received.");
        }
    }
}
