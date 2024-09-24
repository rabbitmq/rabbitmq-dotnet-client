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
using QueueDeclareOk = RabbitMQ.Client.QueueDeclareOk;

namespace Test.Integration.ConnectionRecovery
{
    public class TestConnectionRecovery : TestConnectionRecoveryBase
    {
        public TestConnectionRecovery(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task TestBindingRecovery_GH1035()
        {
            const string routingKey = "unused";
            byte[] body = GetRandomBody();

            var receivedMessageSemaphore = new SemaphoreSlim(0, 1);

            Task MessageReceived(object sender, BasicDeliverEventArgs e)
            {
                receivedMessageSemaphore.Release();
                return Task.CompletedTask;
            }

            var guid = Guid.NewGuid();
            string exchangeName = $"ex-gh-1035-{guid}";
            string queueName = $"q-gh-1035-{guid}";

            await _channel.ExchangeDeclareAsync(exchange: exchangeName,
                type: "fanout", durable: false, autoDelete: true,
                arguments: null);

            QueueDeclareOk q0 = await _channel.QueueDeclareAsync(queue: queueName, exclusive: true);
            Assert.Equal(queueName, q0);

            await _channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: routingKey);

            await _channel.CloseAsync();
            await _channel.DisposeAsync();
            _channel = null;

            _channel = await _conn.CreateChannelAsync();

            // NB: add this for debugging
            // AddCallbackHandlers();

            await _channel.ExchangeDeclareAsync(exchange: exchangeName,
                type: "fanout", durable: false, autoDelete: true,
                arguments: null);

            QueueDeclareOk q1 = await _channel.QueueDeclareAsync(queue: queueName, exclusive: true);
            Assert.Equal(queueName, q1.QueueName);

            await _channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: routingKey);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += MessageReceived;
            await _channel.BasicConsumeAsync(queueName, true, consumer);

            await using (IChannel pubCh = await _conn.CreateChannelAsync())
            {
                await pubCh.BasicPublishAsync(exchange: exchangeName, routingKey: routingKey, body: body);
                await pubCh.CloseAsync();
            }

            Assert.True(await receivedMessageSemaphore.WaitAsync(WaitSpan));

            await CloseAndWaitForRecoveryAsync();

            await using (IChannel pubCh = await _conn.CreateChannelAsync())
            {
                await pubCh.BasicPublishAsync(exchange: exchangeName, routingKey: "unused", body: body);
                await pubCh.CloseAsync();
            }

            Assert.True(await receivedMessageSemaphore.WaitAsync(WaitSpan));
        }
    }
}
