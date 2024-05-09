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
            string q = GenerateQueueName();
            await _channel.QueueDeclareAsync(q, false, true, false, noWait: true, arguments: null);
            await _channel.QueueDeclarePassiveAsync(q);
        }

        [Fact]
        public Task TestQueueDeclareServerNamedNoWait()
        {
            return Assert.ThrowsAsync<InvalidOperationException>(() =>
            {
                return _channel.QueueDeclareAsync("", false, true, false, noWait: true);
            });
        }

        [Fact]
        public async Task TestQueueBindNoWait()
        {
            string q = GenerateQueueName();
            await _channel.QueueDeclareAsync(q, false, true, false, noWait: true);
            await _channel.QueueBindAsync(q, "amq.fanout", "", noWait: true);
        }

        [Fact]
        public async Task TestQueueDeleteNoWait()
        {
            string q = GenerateQueueName();
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
            string x = GenerateExchangeName();
            try
            {
                await _channel.ExchangeDeclareAsync(x, "fanout", false, true);
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
            string x = GenerateExchangeName();
            try
            {
                await _channel.ExchangeDeclareAsync(x, "fanout", false, true, noWait: true);
                await _channel.ExchangeBindAsync(x, "amq.fanout", "", noWait: true);
            }
            finally
            {
                await _channel.ExchangeDeleteAsync(x);
            }
        }

        [Fact]
        public async Task TestExchangeUnbindNoWait()
        {
            string x = GenerateExchangeName();
            try
            {
                await _channel.ExchangeDeclareAsync(x, "fanout", false, true);
                await _channel.ExchangeBindAsync(x, "amq.fanout", "", noWait: true);
                await _channel.ExchangeUnbindAsync(x, "amq.fanout", "", noWait: true);
            }
            finally
            {
                await _channel.ExchangeDeleteAsync(x);
            }
        }

        [Fact]
        public async Task TestExchangeDeleteNoWait()
        {
            string x = GenerateExchangeName();
            await _channel.ExchangeDeclareAsync(x, "fanout", false, true,
                noWait: true, arguments: null);
            await _channel.ExchangeDeleteAsync(x, false, noWait: true);
        }
    }
}
