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
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing.Impl;
using Xunit;
using Xunit.Abstractions;
using QueueDeclareOk = RabbitMQ.Client.QueueDeclareOk;

namespace Test.Integration.ConnectionRecovery
{
    public class TestDeclaration : TestConnectionRecoveryBase
    {
        public TestDeclaration(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestDeclarationOfManyAutoDeleteExchangesWithTransientExchangesThatAreDeleted()
        {
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 3; i++)
            {
                string x1 = $"source-{Guid.NewGuid()}";
                await _channel.ExchangeDeclareAsync(x1, "fanout", false, true);

                string x2 = $"destination-{Guid.NewGuid()}";
                await _channel.ExchangeDeclareAsync(x2, "fanout", false, false);

                await _channel.ExchangeBindAsync(x2, x1, "");
                await _channel.ExchangeDeleteAsync(x2);
            }
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
        }

        [Fact]
        public async Task TestDeclarationOfManyAutoDeleteExchangesWithTransientExchangesThatAreUnbound()
        {
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string x1 = $"source-{Guid.NewGuid()}";
                await _channel.ExchangeDeclareAsync(x1, "fanout", false, true);
                string x2 = $"destination-{Guid.NewGuid()}";
                await _channel.ExchangeDeclareAsync(x2, "fanout", false, false);
                await _channel.ExchangeBindAsync(x2, x1, "");
                await _channel.ExchangeUnbindAsync(x2, x1, "");
                await _channel.ExchangeDeleteAsync(x2);
            }
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
        }

        [Fact]
        public async Task TestDeclarationOfManyAutoDeleteExchangesWithTransientQueuesThatAreDeleted()
        {
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string x = Guid.NewGuid().ToString();
                await _channel.ExchangeDeclareAsync(x, "fanout", false, true);
                QueueDeclareOk q = await _channel.QueueDeclareAsync();
                await _channel.QueueBindAsync(q, x, "");
                await _channel.QueueDeleteAsync(q);
            }
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
        }

        [Fact]
        public async Task TestDeclarationOfManyAutoDeleteExchangesWithTransientQueuesThatAreUnbound()
        {
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string x = Guid.NewGuid().ToString();
                await _channel.ExchangeDeclareAsync(x, "fanout", false, true);
                QueueDeclareOk q = await _channel.QueueDeclareAsync();
                await _channel.QueueBindAsync(q, x, "");
                await _channel.QueueUnbindAsync(q, x, "", null);
            }
            AssertRecordedExchanges((AutorecoveringConnection)_conn, 0);
        }

        [Fact]
        public async Task TestDeclarationOfManyAutoDeleteQueuesWithTransientConsumer()
        {
            AssertRecordedQueues((AutorecoveringConnection)_conn, 0);
            for (int i = 0; i < 1000; i++)
            {
                string q = Guid.NewGuid().ToString();
                await _channel.QueueDeclareAsync(q, false, false, true);
                var dummy = new AsyncEventingBasicConsumer(_channel);
                string tag = await _channel.BasicConsumeAsync(q, true, dummy);
                await _channel.BasicCancelAsync(tag);
            }
            AssertRecordedQueues((AutorecoveringConnection)_conn, 0);
        }
    }
}
