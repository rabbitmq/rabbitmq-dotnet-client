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
            ExchangeName x = GenerateExchangeName();
            await DeclareNonDurableExchangeAsync(_channel, x);
            await CloseAndWaitForRecoveryAsync();
            await AssertExchangeRecoveryAsync(_channel, x);
            await _channel.ExchangeDeleteAsync(x);
        }

        [Fact]
        public async Task TestExchangeToExchangeBindingRecovery()
        {
            QueueDeclareOk q = await _channel.QueueDeclareAsync(QueueName.Empty, false, false, false);

            ExchangeName ex_source = GenerateExchangeName();
            ExchangeName ex_destination = GenerateExchangeName();

            await _channel.ExchangeDeclareAsync(ex_source, ExchangeType.Fanout);
            await _channel.ExchangeDeclareAsync(ex_destination, ExchangeType.Fanout);

            await _channel.ExchangeBindAsync(destination: ex_destination, source: ex_source, RoutingKey.Empty);
            await _channel.QueueBindAsync(q, ex_destination, RoutingKey.Empty);

            try
            {
                await CloseAndWaitForRecoveryAsync();
                Assert.True(_channel.IsOpen);
                await _channel.ConfirmSelectAsync();
                await _channel.BasicPublishAsync(ex_source, RoutingKey.Empty, _encoding.GetBytes("msg"), mandatory: true);
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
