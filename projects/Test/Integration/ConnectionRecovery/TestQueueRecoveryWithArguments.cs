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

using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration.ConnectionRecovery
{
    public class TestQueueRecoveryWithArguments : TestConnectionRecoveryBase
    {
        public TestQueueRecoveryWithArguments(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestQueueRecoveryWithDlxArgument_RabbitMQUsers_hk5pJ4cKF0c()
        {
            string tdiWaitExchangeName = GenerateExchangeName();
            string tdiRetryExchangeName = GenerateExchangeName();
            string testRetryQueueName = GenerateQueueName();
            string testQueueName = GenerateQueueName();

            await _channel.ExchangeDeclareAsync(exchange: tdiWaitExchangeName,
                type: ExchangeType.Topic, durable: true, autoDelete: false, arguments: null);
            await _channel.ExchangeDeclareAsync(exchange: tdiRetryExchangeName,
                type: ExchangeType.Topic, durable: true, autoDelete: false, arguments: null);

            var arguments = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", "tdi.retry.exchange" },
                { "x-dead-letter-routing-key", "QueueTest" }
            };

            await _channel.QueueDeclareAsync(testRetryQueueName, durable: false, exclusive: false, autoDelete: false, arguments);

            arguments["x-dead-letter-exchange"] = "tdi.wait.exchange";
            arguments["x-dead-letter-routing-key"] = "QueueTest";

            await _channel.QueueDeclareAsync(testQueueName, durable: false, exclusive: false, autoDelete: false, arguments);

            arguments.Remove("x-dead-letter-exchange");
            arguments.Remove("x-dead-letter-routing-key");

            await _channel.QueueBindAsync(testRetryQueueName, tdiWaitExchangeName, testQueueName);

            await _channel.QueueBindAsync(testQueueName, tdiRetryExchangeName, testQueueName);

            var consumerAsync = new AsyncEventingBasicConsumer(_channel);
            await _channel.BasicConsumeAsync(queue: testQueueName, autoAck: false, consumer: consumerAsync);

            await CloseAndWaitForRecoveryAsync();

            QueueDeclareOk q0 = await _channel.QueueDeclarePassiveAsync(testRetryQueueName);
            Assert.Equal(testRetryQueueName, q0.QueueName);

            QueueDeclareOk q1 = await _channel.QueueDeclarePassiveAsync(testQueueName);
            Assert.Equal(testQueueName, q1.QueueName);
        }
    }
}
