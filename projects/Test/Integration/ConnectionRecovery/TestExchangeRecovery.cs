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

using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration.ConnectionRecovery
{
    public class TestExchangeRecovery : TestConnectionRecoveryBase
    {
        public TestExchangeRecovery(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestExchangeRecoveryTest()
        {
            // TODO
            // Hack for rabbitmq/rabbitmq-dotnet-client#1682
            AutorecoveringChannel ach = (AutorecoveringChannel)_channel;
            await ach.ConfirmSelectAsync(publisherConfirmationTrackingEnabled: true);

            string x = "dotnet-client.test.recovery.x1";
            await DeclareNonDurableExchangeAsync(_channel, x);
            await CloseAndWaitForRecoveryAsync();
            await AssertExchangeRecoveryAsync(_channel, x);
            await _channel.ExchangeDeleteAsync(x);
        }

        [Fact]
        public async Task TestExchangeToExchangeBindingRecovery()
        {
            // TODO
            // Hack for rabbitmq/rabbitmq-dotnet-client#1682
            AutorecoveringChannel ach = (AutorecoveringChannel)_channel;
            await ach.ConfirmSelectAsync(publisherConfirmationTrackingEnabled: true);

            string q = (await _channel.QueueDeclareAsync("", false, false, false)).QueueName;

            string ex_source = GenerateExchangeName();
            string ex_destination = GenerateExchangeName();

            await _channel.ExchangeDeclareAsync(ex_source, ExchangeType.Fanout);
            await _channel.ExchangeDeclareAsync(ex_destination, ExchangeType.Fanout);

            await _channel.ExchangeBindAsync(destination: ex_destination, source: ex_source, "");
            await _channel.QueueBindAsync(q, ex_destination, "");

            try
            {
                await CloseAndWaitForRecoveryAsync();
                Assert.True(_channel.IsOpen);
                await _channel.BasicPublishAsync(ex_source, "", body: _encoding.GetBytes("msg"), mandatory: true);
                await _channel.WaitForConfirmsOrDieAsync();
                await AssertMessageCountAsync(q, 1);
            }
            finally
            {
                await WithTemporaryChannelAsync(async ch =>
                {
                    await ch.ExchangeDeleteAsync(ex_source);
                    await ch.ExchangeDeleteAsync(ex_destination);
                    await ch.QueueDeleteAsync(q);
                });
            }
        }
    }
}
