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

using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.SequentialIntegration
{
    // The conformance behaviour the other tracing tests cannot see: they publish to the default
    // exchange with a routing key equal to the queue name, which collapses the destination to one
    // component. Issue #1980.
    public class TestSemanticConventions : SequentialIntegrationFixture
    {
        private const string RoutingKey = "warning";

        public TestSemanticConventions(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task PublishAndFetchComposeTheDestinationFromEveryPart_GH1980()
        {
            string exchangeName = GenerateExchangeName();
            string queueName = GenerateQueueName();

            // ActivityRecorder matches the whole span name, so seeing it once pins the name too.
            using var publishRecorder = new ActivityRecorder(RabbitMQActivitySource.PublisherSourceName,
                $"publish {exchangeName}:{RoutingKey}")
            { VerifyParent = false };
            using var fetchRecorder = new ActivityRecorder(RabbitMQActivitySource.SubscriberSourceName,
                $"fetch {exchangeName}:{RoutingKey}:{queueName}")
            { VerifyParent = false };

            ConnectionFactory cf = CreateConnectionFactory();
            cf.TracingOptions = new ConnectionTracingOptions { UseRoutingKeyAsOperationName = true };
            await using IConnection conn = await cf.CreateConnectionAsync();
            await using IChannel ch = await conn.CreateChannelAsync();

            await ch.ExchangeDeclareAsync(exchangeName, ExchangeType.Direct, durable: false, autoDelete: false);
            await ch.QueueDeclareAsync(queueName);
            await ch.QueueBindAsync(queueName, exchangeName, RoutingKey);

            await ch.BasicPublishAsync(exchangeName, RoutingKey, true, Encoding.UTF8.GetBytes("hi"));
            BasicGetResult result = await ch.BasicGetAsync(queueName, autoAck: true);
            Assert.NotNull(result);

            // {exchange}:{routing key}: the bare exchange made two publishes under one routing
            // key indistinguishable.
            Activity publish = publishRecorder.VerifyActivityRecordedOnce();
            Assert.Equal(ActivityKind.Producer, publish.Kind);
            publish.HasTag(RabbitMQActivitySource.MessagingDestination, $"{exchangeName}:{RoutingKey}");
            publish.HasTag(RabbitMQActivitySource.MessagingDestinationRoutingKey, RoutingKey);

            // {exchange}:{routing key}:{queue}, and Client because the convention maps receive
            // to CLIENT. No other test asserts the receive side's destination.
            Activity fetch = fetchRecorder.VerifyActivityRecordedOnce();
            Assert.Equal(ActivityKind.Client, fetch.Kind);
            fetch.HasTag(RabbitMQActivitySource.MessagingDestination,
                $"{exchangeName}:{RoutingKey}:{queueName}");
            fetch.HasTag(RabbitMQActivitySource.MessagingOperationName,
                RabbitMQActivitySource.MessagingOperationNameBasicGet);
            fetch.HasTag(RabbitMQActivitySource.RabbitMQMessageReceived, true);

            // The registry name. The old messaging.rabbitmq.delivery_tag matched nothing in it, so
            // anything keyed on the old name was already reading no value.
            fetch.HasTag(RabbitMQActivitySource.RabbitMQDeliveryTag, result.DeliveryTag);
            fetch.HasNoTag("messaging.rabbitmq.delivery_tag");

            await ch.QueueDeleteAsync(queueName);
            await ch.ExchangeDeleteAsync(exchangeName);
        }

        [Fact]
        public async Task DeliveryComposesTheTwoPartDestination_GH1980()
        {
            string exchangeName = GenerateExchangeName();
            string queueName = GenerateQueueName();

            // The one shape no other test sees: for a delivery the old and new destination logic
            // agree whenever the exchange is empty, which is every other test here.
            using var deliverRecorder = new ActivityRecorder(RabbitMQActivitySource.SubscriberSourceName,
                $"deliver {exchangeName}:{RoutingKey}")
            { VerifyParent = false };

            ConnectionFactory cf = CreateConnectionFactory();
            cf.TracingOptions = new ConnectionTracingOptions { UseRoutingKeyAsOperationName = true };
            await using IConnection conn = await cf.CreateConnectionAsync();
            await using IChannel ch = await conn.CreateChannelAsync();

            await ch.ExchangeDeclareAsync(exchangeName, ExchangeType.Direct, durable: false, autoDelete: false);
            await ch.QueueDeclareAsync(queueName);
            await ch.QueueBindAsync(queueName, exchangeName, RoutingKey);

            var consumer = new AsyncEventingBasicConsumer(ch);
            var receivedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            consumer.ReceivedAsync += (_, _) =>
            {
                receivedTcs.TrySetResult(true);
                return Task.CompletedTask;
            };

            string consumerTag = await ch.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);
            await ch.BasicPublishAsync(exchangeName, RoutingKey, true, Encoding.UTF8.GetBytes("hi"));
            /*
             * Two statements, not Assert.True(await ....WaitAsync(WaitSpan)). On net472 there is no
             * BCL Task<T>.WaitAsync, so the call binds to the client's own non-generic
             * TaskExtensions.WaitAsync polyfill, the bool is lost, and Assert.True gets a void -
             * CS1503, on the one target framework only build-win32 compiles.
             */
            await receivedTcs.Task.WaitAsync(WaitSpan);
            Assert.True(await receivedTcs.Task);
            await ch.BasicCancelAsync(consumerTag);

            Activity deliver = deliverRecorder.VerifyActivityRecordedOnce();
            Assert.Equal(ActivityKind.Consumer, deliver.Kind);
            deliver.HasTag(RabbitMQActivitySource.MessagingDestination, $"{exchangeName}:{RoutingKey}");
            deliver.HasTag(RabbitMQActivitySource.MessagingDestinationRoutingKey, RoutingKey);
            deliver.HasTag(RabbitMQActivitySource.MessagingOperationName,
                RabbitMQActivitySource.MessagingOperationNameBasicDeliver);

            // The registry name, on the delivery path as well as on the fetch path.
            deliver.HasTag(RabbitMQActivitySource.RabbitMQDeliveryTag, 1UL);
            deliver.HasNoTag("messaging.rabbitmq.delivery_tag");

            // No queue is available to a delivery, so the destination stops at two parts and the
            // three-part form must not appear. Carrying the queue here is #2055.
            Assert.DoesNotContain($":{queueName}",
                (string)deliver.GetTagItem(RabbitMQActivitySource.MessagingDestination));

            await ch.QueueDeleteAsync(queueName);
            await ch.ExchangeDeleteAsync(exchangeName);
        }

        [Fact]
        public async Task AnEmptyFetchReportsTheQueueAndNotAmqDefault_GH1980()
        {
            string queueName = GenerateQueueName();

            using var fetchRecorder = new ActivityRecorder(RabbitMQActivitySource.SubscriberSourceName,
                $"fetch {queueName}")
            { VerifyParent = false };

            ConnectionFactory cf = CreateConnectionFactory();
            cf.TracingOptions = new ConnectionTracingOptions { UseRoutingKeyAsOperationName = true };
            await using IConnection conn = await cf.CreateConnectionAsync();
            await using IChannel ch = await conn.CreateChannelAsync();

            await ch.QueueDeclareAsync(queueName);
            Assert.Null(await ch.BasicGetAsync(queueName, autoAck: true));

            // The old span was "fetch (empty) {queue}", Consumer, and claimed amq.default while
            // the queue was in scope.
            Activity fetch = fetchRecorder.VerifyActivityRecordedOnce();
            Assert.Equal(ActivityKind.Client, fetch.Kind);
            fetch.HasTag(RabbitMQActivitySource.MessagingOperationName,
                RabbitMQActivitySource.MessagingOperationNameBasicGet);
            fetch.HasTag(RabbitMQActivitySource.MessagingDestination, queueName);
            fetch.HasTag(RabbitMQActivitySource.RabbitMQMessageReceived, false);

            // An empty fetch has no message, so nothing message-level is emitted for one.
            fetch.HasNoTag(RabbitMQActivitySource.RabbitMQDeliveryTag);
            fetch.HasNoTag(RabbitMQActivitySource.MessagingDestinationRoutingKey);

            await ch.QueueDeleteAsync(queueName);
        }

        [Fact]
        public async Task AnEmptyRoutingKeyIsOmittedRatherThanEmitted_GH1980()
        {
            string exchangeName = GenerateExchangeName();

            // messaging.rabbitmq.destination.routing_key is conditionally required "if not empty",
            // and the destination is the exchange alone when the routing key contributes nothing.
            using var publishRecorder = new ActivityRecorder(RabbitMQActivitySource.PublisherSourceName,
                $"publish {exchangeName}")
            { VerifyParent = false };

            ConnectionFactory cf = CreateConnectionFactory();
            cf.TracingOptions = new ConnectionTracingOptions { UseRoutingKeyAsOperationName = true };
            await using IConnection conn = await cf.CreateConnectionAsync();
            await using IChannel ch = await conn.CreateChannelAsync();

            await ch.ExchangeDeclareAsync(exchangeName, ExchangeType.Fanout, durable: false, autoDelete: false);
            await ch.BasicPublishAsync(exchangeName, string.Empty, false, Encoding.UTF8.GetBytes("hi"));

            Activity publish = publishRecorder.VerifyActivityRecordedOnce();
            publish.HasTag(RabbitMQActivitySource.MessagingDestination, exchangeName);
            publish.HasNoTag(RabbitMQActivitySource.MessagingDestinationRoutingKey);

            await ch.ExchangeDeleteAsync(exchangeName);
        }
    }
}
