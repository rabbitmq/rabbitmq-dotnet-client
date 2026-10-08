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
using Xunit;
using Xunit.Abstractions;

namespace Test.SequentialIntegration
{
    /*
     * rabbitmq/rabbitmq-dotnet-client#1980.
     *
     * The conformance behaviour that TestActivitySource and TestOpenTelemetry cannot see. Those
     * publish to the default exchange with a routing key equal to the queue name, which collapses
     * the destination to a single component - so the exchange, the queue and their ordering never
     * reach an assertion, and dropping the exchange from the destination entirely would leave both
     * suites green. These use a named exchange with a routing key distinct from the queue name so
     * each component is distinguishable, and they cover the empty basic.get, which nothing
     * exercised at all.
     *
     * Configuration goes through ConnectionFactory.TracingOptions rather than the deprecated
     * process-wide statics (#1981), so these tests need no CS0618 suppression and are unaffected by
     * whatever the statics happen to hold.
     */
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

            /*
             * ActivityRecorder matches on the exact span name, so a recorder that sees its activity
             * once has pinned the name as well as the tags. VerifyParent is off because a fetch
             * span's parent depends on propagation, which is not what these assert.
             */
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

            /*
             * Producer: {exchange}:{routing key}. Previously the destination was the bare exchange
             * and the span name was the bare routing key, so neither carried both components and
             * two publishes to different exchanges under one routing key shared a span name.
             */
            Activity publish = publishRecorder.VerifyActivityRecordedOnce();
            Assert.Equal(ActivityKind.Producer, publish.Kind);
            publish.HasTag(RabbitMQActivitySource.MessagingDestination, $"{exchangeName}:{RoutingKey}");
            publish.HasTag(RabbitMQActivitySource.MessagingDestinationRoutingKey, RoutingKey);

            /*
             * Consumer: {exchange}:{routing key}:{queue}, in that order, and Client rather than
             * Consumer because the convention maps receive to CLIENT. Asserting the receive side's
             * destination at all is new; the existing helpers only ever asserted the producer's.
             */
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

            /*
             * Four things nothing covered before. The span was named "fetch (empty) {queue}", was
             * ActivityKind.Consumer, reported the operation name "fetch (empty)" - encoding an
             * outcome into messaging.operation.name - and claimed a destination of "amq.default"
             * while the queue was in scope and already in the span name.
             */
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
