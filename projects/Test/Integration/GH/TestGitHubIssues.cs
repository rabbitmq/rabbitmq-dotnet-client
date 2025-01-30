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

#nullable enable

namespace Test.Integration.GH
{
    public class TestGitHubIssues : IntegrationFixture
    {
        public TestGitHubIssues(ITestOutputHelper output) : base(output)
        {
        }

        public override Task InitializeAsync()
        {
            // NB: nothing to do here since each test creates its own factory,
            // connections and channels
            Assert.Null(_connFactory);
            Assert.Null(_conn);
            Assert.Null(_channel);
            return Task.CompletedTask;
        }

        [Fact]
        public async Task TestBasicConsumeCancellation_GH1750()
        {
            /*
             * Note:
             * Testing that the task is actually canceled requires a hacked RabbitMQ server.
             * Modify deps/rabbit/src/rabbit_channel.erl, handle_cast for basic.consume_ok
             * Before send/2, add timer:sleep(1000), then `make run-broker`
             *
             * The _output line at the end of the test will print TaskCanceledException
             */
            Assert.Null(_connFactory);
            Assert.Null(_conn);
            Assert.Null(_channel);

            _connFactory = CreateConnectionFactory();
            _connFactory.NetworkRecoveryInterval = TimeSpan.FromMilliseconds(250);
            _connFactory.AutomaticRecoveryEnabled = true;
            _connFactory.TopologyRecoveryEnabled = true;

            _conn = await _connFactory.CreateConnectionAsync();
            _channel = await _conn.CreateChannelAsync();

            QueueDeclareOk q = await _channel.QueueDeclareAsync();

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += (o, a) =>
            {
                return Task.CompletedTask;
            };

            bool sawConnectionShutdown = false;
            _conn.ConnectionShutdownAsync += (o, ea) =>
            {
                sawConnectionShutdown = true;
                return Task.CompletedTask;
            };

            try
            {
                // Note: use this to test timeout via the passed-in RPC token
                /*
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(5));
                await _channel.BasicConsumeAsync(q.QueueName, true, consumer, cts.Token);
                */

                // Note: use these to test timeout of the continuation RPC operation
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                _channel.ContinuationTimeout = TimeSpan.FromMilliseconds(5);
                await _channel.BasicConsumeAsync(q.QueueName, true, consumer, cts.Token);
            }
            catch (Exception ex)
            {
                _output.WriteLine("ex: {0}", ex);
            }

            await Task.Delay(500);

            Assert.False(sawConnectionShutdown);
        }

        [Fact]
        public async Task TestHeartbeatTimeoutValue_GH1756()
        {
            var connectionFactory = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                RequestedHeartbeat = TimeSpan.Zero,
            };

            _conn = await connectionFactory.CreateConnectionAsync("some-name");

            Assert.True(_conn.Heartbeat != default);
        }
    }
}
