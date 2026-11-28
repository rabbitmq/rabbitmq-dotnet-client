// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------


using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration.ConnectionRecovery
{
    public class TestBindingRecoveryWithArguments : TestConnectionRecoveryBase
    {
        public TestBindingRecoveryWithArguments(ITestOutputHelper output) : base(output)
        {
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task UnbindingWithEqualArgumentsForgetsTheBinding_GH2147(bool queueBinding)
        {
            var conn = (AutorecoveringConnection)_conn;
            string exchange = GenerateExchangeName();
            string destination = queueBinding ? GenerateQueueName() : GenerateExchangeName();
            await _channel.ExchangeDeclareAsync(exchange, ExchangeType.Direct, durable: false, autoDelete: false);
            try
            {
                if (queueBinding)
                {
                    await _channel.QueueDeclareAsync(destination, durable: false, exclusive: true, autoDelete: false);
                }
                else
                {
                    await _channel.ExchangeDeclareAsync(destination, ExchangeType.Direct, durable: false, autoDelete: false);
                }

                int before = conn.RecordedBindingsCount;

                // Equal arguments built separately, in a different key order, as an application unbinding later would.
                await BindAsync(queueBinding, destination, exchange, new Dictionary<string, object> { ["k1"] = "v", ["k2"] = 1 });
                await BindAsync(queueBinding, destination, exchange, new Dictionary<string, object> { ["k2"] = 1, ["k1"] = "v" });
                Assert.Equal(before + 1, conn.RecordedBindingsCount);

                await UnbindAsync(queueBinding, destination, exchange, new Dictionary<string, object> { ["k2"] = 1, ["k1"] = "v" });
                Assert.Equal(before, conn.RecordedBindingsCount);

                if (queueBinding)
                {
                    await CloseAndWaitForRecoveryAsync();
                    // A binding still recorded after the unbind would be replayed by recovery and route this message.
                    await using IChannel confirming = await _conn.CreateChannelAsync(
                        new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true));
                    await confirming.BasicPublishAsync(exchange, "rk", new byte[] { 1 });
                    Assert.Null(await confirming.BasicGetAsync(destination, autoAck: true));
                }
            }
            finally
            {
                if (!queueBinding)
                {
                    await _channel.ExchangeDeleteAsync(destination);
                }

                await _channel.ExchangeDeleteAsync(exchange);
            }
        }

        [Fact]
        public async Task UnbindingTheLastBindingOfAnAutoDeleteExchangeForgetsTheBindingsIntoIt_GH2147()
        {
            var conn = (AutorecoveringConnection)_conn;
            string upstream = GenerateExchangeName();
            string autoDelete = GenerateExchangeName();
            string queue = GenerateQueueName();
            await _channel.ExchangeDeclareAsync(upstream, ExchangeType.Topic, durable: false, autoDelete: false);
            try
            {
                await _channel.ExchangeDeclareAsync(autoDelete, ExchangeType.Headers, durable: false, autoDelete: true);
                await _channel.QueueDeclareAsync(queue, durable: false, exclusive: true, autoDelete: false);
                await _channel.ExchangeBindAsync(autoDelete, upstream, "#");
                await _channel.QueueBindAsync(queue, autoDelete, "", new Dictionary<string, object> { ["x-match"] = "all", ["t"] = "a" });
                int exchangesBefore = conn.RecordedExchangesCount;
                int bindingsBefore = conn.RecordedBindingsCount;

                // The broker now deletes the auto-delete exchange, and with it the binding from upstream.
                await _channel.QueueUnbindAsync(queue, autoDelete, "", new Dictionary<string, object> { ["t"] = "a", ["x-match"] = "all" });

                Assert.Equal(exchangesBefore - 1, conn.RecordedExchangesCount);
                Assert.Equal(bindingsBefore - 2, conn.RecordedBindingsCount);
                // A recorded binding into the deleted exchange would make every recovery attempt fail with NOT_FOUND.
                await CloseAndWaitForRecoveryAsync();
                Assert.True(_channel.IsOpen);
            }
            finally
            {
                await _channel.ExchangeDeleteAsync(upstream);
            }
        }

        private Task BindAsync(bool queueBinding, string destination, string exchange, IDictionary<string, object> arguments)
            => queueBinding
                ? _channel.QueueBindAsync(destination, exchange, "rk", arguments)
                : _channel.ExchangeBindAsync(destination, exchange, "rk", arguments);

        private Task UnbindAsync(bool queueBinding, string destination, string exchange, IDictionary<string, object> arguments)
            => queueBinding
                ? _channel.QueueUnbindAsync(destination, exchange, "rk", arguments)
                : _channel.ExchangeUnbindAsync(destination, exchange, "rk", arguments);
    }
}
