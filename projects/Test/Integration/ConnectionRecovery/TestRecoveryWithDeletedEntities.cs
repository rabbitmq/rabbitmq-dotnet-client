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

using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
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

            string ex_source = GenerateExchangeName();
            string ex_destination = GenerateExchangeName();

            await _channel.ExchangeDeclareAsync(ex_source, ExchangeType.Fanout);
            await _channel.ExchangeDeclareAsync(ex_destination, ExchangeType.Fanout);

            await _channel.ExchangeBindAsync(destination: ex_destination, source: ex_source, "");
            await _channel.QueueBindAsync(q, ex_destination, "");
            await _channel.ExchangeUnbindAsync(ex_destination, ex_source, "");

            try
            {
                await CloseAndWaitForRecoveryAsync();
                Assert.True(_channel.IsOpen);
                await _channel.BasicPublishAsync(ex_source, "", _encoding.GetBytes("msg"));
                await AssertMessageCountAsync(q, 0);
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

            string ex_source = GenerateExchangeName();
            string ex_destination = GenerateExchangeName();

            await _channel.ExchangeDeclareAsync(ex_source, ExchangeType.Fanout);
            await _channel.ExchangeDeclareAsync(ex_destination, ExchangeType.Fanout);

            await _channel.ExchangeBindAsync(destination: ex_destination, source: ex_source, routingKey: "");
            await _channel.QueueBindAsync(q, ex_destination, "");
            await _channel.QueueUnbindAsync(q, ex_destination, "");

            try
            {
                await CloseAndWaitForRecoveryAsync();
                Assert.True(_channel.IsOpen);
                await _channel.BasicPublishAsync(ex_source, "", _encoding.GetBytes("msg"));
                await AssertMessageCountAsync(q, 0);
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

        [Fact]
        public async Task TestAutoDeleteQueueBindingsRemovedWhenConsumerCancelled()
        {
            // See rabbitmq/rabbitmq-dotnet-client#1905.
            //
            // When the last consumer on an auto-delete queue is cancelled, the queue
            // and its bindings must be removed from recorded topology so that recovery
            // does not try to restore them.
            string exchangeName = GenerateExchangeName();
            await _channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Direct, autoDelete: true);

            var queueDeclareOk = await _channel.QueueDeclareAsync("", false, false, autoDelete: true);
            string queueName = queueDeclareOk.QueueName;
            await _channel.QueueBindAsync(queueName, exchangeName, routingKey: "key");

            var autorecoveringConn = (AutorecoveringConnection)_conn;
            Assert.Equal(1, autorecoveringConn.RecordedExchangesCount);
            Assert.Equal(1, autorecoveringConn.RecordedQueuesCount);
            Assert.Equal(1, autorecoveringConn.RecordedBindingsCount);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            string consumerTag = await _channel.BasicConsumeAsync(queueName, true, consumer);
            await _channel.BasicCancelAsync(consumerTag);

            Assert.Equal(0, autorecoveringConn.RecordedExchangesCount);
            Assert.Equal(0, autorecoveringConn.RecordedQueuesCount);
            Assert.Equal(0, autorecoveringConn.RecordedBindingsCount);

            await CloseAndWaitForRecoveryAsync();
            Assert.True(_channel.IsOpen);
        }

        [Fact]
        public async Task TestAutoDeleteQueueBindingsRemovedWhenChannelClosed()
        {
            // See rabbitmq/rabbitmq-dotnet-client#1905.
            //
            // Same as above but uses channel closure as the trigger.
            string exchangeName = GenerateExchangeName();
            var autorecoveringConn = (AutorecoveringConnection)_conn;

            IChannel ch = await _conn.CreateChannelAsync(_createChannelOptions);
            await using (ch.ConfigureAwait(false))
            {
                await ch.ExchangeDeclareAsync(exchangeName, ExchangeType.Direct, autoDelete: true);

                var queueDeclareOk = await ch.QueueDeclareAsync("", false, false, autoDelete: true);
                string queueName = queueDeclareOk.QueueName;
                await ch.QueueBindAsync(queueName, exchangeName, routingKey: "key");

                Assert.Equal(1, autorecoveringConn.RecordedExchangesCount);
                Assert.Equal(1, autorecoveringConn.RecordedQueuesCount);
                Assert.Equal(1, autorecoveringConn.RecordedBindingsCount);

                var consumer = new AsyncEventingBasicConsumer(ch);
                await ch.BasicConsumeAsync(queueName, true, consumer);
                await ch.CloseAsync();
            }

            Assert.Equal(0, autorecoveringConn.RecordedExchangesCount);
            Assert.Equal(0, autorecoveringConn.RecordedQueuesCount);
            Assert.Equal(0, autorecoveringConn.RecordedBindingsCount);

            await CloseAndWaitForRecoveryAsync();
            Assert.True(_channel.IsOpen);
        }
    }
}
