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
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestConfirmSelect : IntegrationFixture
    {
        public TestConfirmSelect(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestConfirmSelectIdempotency()
        {
            ValueTask PublishAsync()
            {
                return _channel.BasicPublishAsync(exchange: "",
                    routingKey: Guid.NewGuid().ToString(), _encoding.GetBytes("message"));
            }

            await _channel.ConfirmSelectAsync();
            Assert.Equal(1ul, _channel.NextPublishSeqNo);
            await PublishAsync();
            Assert.Equal(2ul, _channel.NextPublishSeqNo);
            await PublishAsync();
            Assert.Equal(3ul, _channel.NextPublishSeqNo);

            await _channel.ConfirmSelectAsync();
            await PublishAsync();
            Assert.Equal(4ul, _channel.NextPublishSeqNo);
            await PublishAsync();
            Assert.Equal(5ul, _channel.NextPublishSeqNo);
            await PublishAsync();
            Assert.Equal(6ul, _channel.NextPublishSeqNo);
        }

        [Theory]
        [InlineData(255)]
        [InlineData(256)]
        public async Task TestDeliveryTagDiverged_GH1043(ushort correlationIdLength)
        {
            byte[] body = GetRandomBody(16);

            await _channel.ExchangeDeclareAsync("sample", "fanout", autoDelete: true);
            // _channel.BasicAcks += (s, e) => _output.WriteLine("Acked {0}", e.DeliveryTag);
            await _channel.ConfirmSelectAsync();

            var properties = new BasicProperties();
            // _output.WriteLine("Client delivery tag {0}", _channel.NextPublishSeqNo);
            await _channel.BasicPublishAsync(exchange: "sample", routingKey: string.Empty, properties, body);
            await _channel.WaitForConfirmsOrDieAsync();

            try
            {
                properties = new BasicProperties
                {
                    CorrelationId = new string('o', correlationIdLength)
                };
                // _output.WriteLine("Client delivery tag {0}", _channel.NextPublishSeqNo);
                await _channel.BasicPublishAsync("sample", string.Empty, properties, body);
                await _channel.WaitForConfirmsOrDieAsync();
            }
            catch
            {
                // _output.WriteLine("Error when trying to publish with long string: {0}", e.Message);
            }

            properties = new BasicProperties();
            // _output.WriteLine("Client delivery tag {0}", _channel.NextPublishSeqNo);
            await _channel.BasicPublishAsync("sample", string.Empty, properties, body);
            await _channel.WaitForConfirmsOrDieAsync();
            // _output.WriteLine("I'm done...");
        }
    }
}
