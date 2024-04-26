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

using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration.ConnectionRecovery
{
    public class TestRecoveryWithDeletedEntities : TestConnectionRecoveryBase
    {
        public TestRecoveryWithDeletedEntities(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestThatDeletedExchangeBindingsDontReappearOnRecovery()
        {
            string q = (await _channel.QueueDeclareAsync("", false, false, false)).QueueName;
            string x1 = "amq.fanout";
            string x2 = GenerateExchangeName();

            await _channel.ExchangeDeclareAsync(x2, "fanout");
            await _channel.ExchangeBindAsync(x1, x2, "");
            await _channel.QueueBindAsync(q, x1, "");
            await _channel.ExchangeUnbindAsync(x1, x2, "");

            try
            {
                await CloseAndWaitForRecoveryAsync();
                Assert.True(_channel.IsOpen);
                await _channel.BasicPublishAsync(x2, "", _encoding.GetBytes("msg"));
                await AssertMessageCountAsync(q, 0);
            }
            finally
            {
                await WithTemporaryChannelAsync(async ch =>
                {
                    await ch.ExchangeDeleteAsync(x2);
                    await ch.QueueDeleteAsync(q);
                });
            }
        }

        [Fact]
        public async Task TestThatDeletedExchangesDontReappearOnRecovery()
        {
            string x = GenerateExchangeName();
            await _channel.ExchangeDeclareAsync(x, "fanout");
            await _channel.ExchangeDeleteAsync(x);

            try
            {
                await CloseAndWaitForRecoveryAsync();
                Assert.True(_channel.IsOpen);
                await _channel.ExchangeDeclarePassiveAsync(x);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException e)
            {
                // expected
                AssertShutdownError(e.ShutdownReason, 404);
            }
        }

        [Fact]
        public async Task TestThatDeletedQueueBindingsDontReappearOnRecovery()
        {
            string q = (await _channel.QueueDeclareAsync("", false, false, false)).QueueName;
            string x1 = "amq.fanout";
            string x2 = GenerateExchangeName();

            await _channel.ExchangeDeclareAsync(x2, "fanout");
            await _channel.ExchangeBindAsync(x1, x2, "");
            await _channel.QueueBindAsync(q, x1, "");
            await _channel.QueueUnbindAsync(q, x1, "", null);

            try
            {
                await CloseAndWaitForRecoveryAsync();
                Assert.True(_channel.IsOpen);
                await _channel.BasicPublishAsync(x2, "", _encoding.GetBytes("msg"));
                await AssertMessageCountAsync(q, 0);
            }
            finally
            {
                await WithTemporaryChannelAsync(async ch =>
                {
                    await ch.ExchangeDeleteAsync(x2);
                    await ch.QueueDeleteAsync(q);
                });
            }
        }

        [Fact]
        public async Task TestThatDeletedQueuesDontReappearOnRecovery()
        {
            string q = "dotnet-client.recovery.q1";
            await _channel.QueueDeclareAsync(q, false, false, false);
            await _channel.QueueDeleteAsync(q);

            try
            {
                await CloseAndWaitForRecoveryAsync();
                Assert.True(_channel.IsOpen);
                await _channel.QueueDeclarePassiveAsync(q);
                Assert.Fail("Expected an exception");
            }
            catch (OperationInterruptedException e)
            {
                // expected
                AssertShutdownError(e.ShutdownReason, 404);
            }
        }
    }
}
