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
using RabbitMQ.Client.Exceptions;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestNoWait : IntegrationFixture
    {
        public TestNoWait(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestQueueDeclareNoWait()
        {
            QueueName q = GenerateQueueName();
            await _channel.QueueDeclareAsync(q, false, true, false, noWait: true, arguments: null);
            await _channel.QueueDeclarePassiveAsync(q);
        }

        [Fact]
        public Task TestQueueDeclareServerNamedNoWait()
        {
            return Assert.ThrowsAsync<InvalidOperationException>(() =>
            {
                return _channel.QueueDeclareAsync(QueueName.Empty, false, true, false, noWait: true);
            });
        }

        [Fact]
        public async Task TestQueueBindNoWait()
        {
            QueueName q = GenerateQueueName();
            await _channel.QueueDeclareAsync(q, false, true, false, noWait: true);
            await _channel.QueueBindAsync(q, ExchangeName.AmqFanout, RoutingKey.Empty, noWait: true);
        }

        [Fact]
        public async Task TestQueueDeleteNoWait()
        {
            QueueName q = GenerateQueueName();
            await _channel.QueueDeclareAsync(q, false, true, false, noWait: true);
            await _channel.QueueDeleteAsync(q, false, false, noWait: true);
            await Assert.ThrowsAsync<OperationInterruptedException>(() =>
            {
                return _channel.QueueDeclarePassiveAsync(q);
            });
        }

        [Fact]
        public async Task TestExchangeDeclareNoWaitAsync()
        {
            ExchangeName x = GenerateExchangeName();
            try
            {
                await _channel.ExchangeDeclareAsync(x, ExchangeType.Fanout, false, true);
                await _channel.ExchangeDeclarePassiveAsync(x);
            }
            finally
            {
                await _channel.ExchangeDeleteAsync(x);
            }
        }

        [Fact]
        public async Task TestExchangeBindNoWait()
        {
            ExchangeName x = GenerateExchangeName();
            try
            {
                await _channel.ExchangeDeclareAsync(x, ExchangeType.Fanout, false, true, noWait: true);
                await _channel.ExchangeBindAsync(x, ExchangeName.AmqFanout, RoutingKey.Empty, noWait: true);
            }
            finally
            {
                await _channel.ExchangeDeleteAsync(x);
            }
        }

        [Fact]
        public async Task TestExchangeUnbindNoWait()
        {
            ExchangeName x = GenerateExchangeName();
            try
            {
                await _channel.ExchangeDeclareAsync(x, ExchangeType.Fanout, false, true);
                await _channel.ExchangeBindAsync(x, ExchangeName.AmqFanout, RoutingKey.Empty, noWait: true);
                await _channel.ExchangeUnbindAsync(x, ExchangeName.AmqFanout, RoutingKey.Empty, noWait: true);
            }
            finally
            {
                await _channel.ExchangeDeleteAsync(x);
            }
        }

        [Fact]
        public async Task TestExchangeDeleteNoWait()
        {
            ExchangeName x = GenerateExchangeName();
            await _channel.ExchangeDeclareAsync(x, ExchangeType.Fanout, false, true,
                noWait: true, arguments: null);
            await _channel.ExchangeDeleteAsync(x, false, noWait: true);
        }
    }
}
