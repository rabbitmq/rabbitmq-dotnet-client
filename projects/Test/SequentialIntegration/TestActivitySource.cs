// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.SequentialIntegration
{
    public class TestActivitySource : SequentialIntegrationFixture
    {
        public TestActivitySource(ITestOutputHelper output) : base(output)
        {
        }

        void AssertStringTagEquals(Activity activity, string name, string expected)
        {
            string tag = activity.GetTagItem(name) as string;
            Assert.NotNull(tag);
            Assert.Equal(expected, tag);
        }

        void AssertStringTagStartsWith(Activity activity, string name, string expected)
        {
            string tag = activity.GetTagItem(name) as string;
            Assert.NotNull(tag);
            Assert.StartsWith(expected, tag);
        }

        void AssertStringTagNotNullOrEmpty(Activity activity, string name)
        {
            string tag = activity.GetTagItem(name) as string;
            Assert.NotNull(tag);
            Assert.False(string.IsNullOrEmpty(tag));
        }

        void AssertIntTagGreaterThanZero(Activity activity, string name)
        {
            Assert.True(activity.GetTagItem(name) is int result && result > 0);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public async Task TestPublisherAndConsumerActivityTagsAsync(bool useRoutingKeyAsOperationName, bool usePublisherAsParent)
        {
            RabbitMQActivitySource.UseRoutingKeyAsOperationName = useRoutingKeyAsOperationName;
            RabbitMQActivitySource.TracingOptions.UsePublisherAsParent = usePublisherAsParent;
            var activities = new List<Activity>();
            using ActivityListener activityListener = StartActivityListener(activities);
            await Task.Delay(500);

            string queueName = $"{Guid.NewGuid()}";
            QueueDeclareOk q = await _channel.QueueDeclareAsync(queueName);
            byte[] sendBody = Encoding.UTF8.GetBytes("hi");
            byte[] consumeBody = null;
            var consumer = new AsyncEventingBasicConsumer(_channel);
            var consumerReceivedTcs =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            consumer.ReceivedAsync += (o, a) =>
            {
                consumeBody = a.Body.ToArray();
                consumerReceivedTcs.SetResult(true);
                return Task.CompletedTask;
            };

            string consumerTag = await _channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);
            await _channel.BasicPublishAsync("", q.QueueName, true, sendBody);

            await consumerReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(await consumerReceivedTcs.Task);

            await _channel.BasicCancelAsync(consumerTag);
            await Task.Delay(500);
            AssertActivityData(useRoutingKeyAsOperationName, usePublisherAsParent, queueName, activities, true);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public async Task TestPublisherWithCachedStringsAndConsumerActivityTagsAsync(bool useRoutingKeyAsOperationName, bool usePublisherAsParent)
        {
            RabbitMQActivitySource.UseRoutingKeyAsOperationName = useRoutingKeyAsOperationName;
            RabbitMQActivitySource.TracingOptions.UsePublisherAsParent = usePublisherAsParent;
            var activities = new List<Activity>();
            using ActivityListener activityListener = StartActivityListener(activities);
            await Task.Delay(500);

            string queueName = $"{Guid.NewGuid()}";
            QueueDeclareOk q = await _channel.QueueDeclareAsync(queueName);
            byte[] sendBody = Encoding.UTF8.GetBytes("hi");
            byte[] consumeBody = null;
            var consumer = new AsyncEventingBasicConsumer(_channel);
            var consumerReceivedTcs =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            consumer.ReceivedAsync += (o, a) =>
            {
                consumeBody = a.Body.ToArray();
                consumerReceivedTcs.SetResult(true);
                return Task.CompletedTask;
            };

            string consumerTag = await _channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);
            CachedString exchange = new CachedString("");
            CachedString routingKey = new CachedString(q.QueueName);
            await _channel.BasicPublishAsync(exchange, routingKey, true, sendBody);

            await consumerReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(await consumerReceivedTcs.Task);

            await _channel.BasicCancelAsync(consumerTag);
            await Task.Delay(500);
            AssertActivityData(useRoutingKeyAsOperationName, usePublisherAsParent, queueName, activities, true);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public async Task TestPublisherWithPublicationAddressAndConsumerActivityTagsAsync(bool useRoutingKeyAsOperationName, bool usePublisherAsParent)
        {
            RabbitMQActivitySource.UseRoutingKeyAsOperationName = useRoutingKeyAsOperationName;
            RabbitMQActivitySource.TracingOptions.UsePublisherAsParent = usePublisherAsParent;
            var activities = new List<Activity>();
            using ActivityListener activityListener = StartActivityListener(activities);
            await Task.Delay(500);

            string queueName = $"{Guid.NewGuid()}";
            QueueDeclareOk q = await _channel.QueueDeclareAsync(queueName);
            byte[] sendBody = Encoding.UTF8.GetBytes("hi");
            byte[] consumeBody = null;
            var consumer = new AsyncEventingBasicConsumer(_channel);
            var consumerReceivedTcs =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            consumer.ReceivedAsync += (o, a) =>
            {
                consumeBody = a.Body.ToArray();
                consumerReceivedTcs.SetResult(true);
                return Task.CompletedTask;
            };

            string consumerTag = await _channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);
            var publicationAddress = new PublicationAddress(ExchangeType.Direct, "", q.QueueName);
            await _channel.BasicPublishAsync(publicationAddress, new BasicProperties(), sendBody);

            await consumerReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(await consumerReceivedTcs.Task);

            await _channel.BasicCancelAsync(consumerTag);
            await Task.Delay(500);
            AssertActivityData(useRoutingKeyAsOperationName, usePublisherAsParent, queueName, activities, true);
        }

        [Theory]
        [InlineData(true, true, true)]
        [InlineData(true, true, false)]
        [InlineData(true, false, true)]
        [InlineData(true, false, false)]
        [InlineData(false, true, true)]
        [InlineData(false, true, false)]
        [InlineData(false, false, true)]
        [InlineData(false, false, false)]
        public async Task TestPublisherAndBasicGetActivityTagsAsync(bool useRoutingKeyAsOperationName, bool usePublisherAsParent, bool useMessageId)
        {
            RabbitMQActivitySource.UseRoutingKeyAsOperationName = useRoutingKeyAsOperationName;
            RabbitMQActivitySource.TracingOptions.UsePublisherAsParent = usePublisherAsParent;
            var activities = new List<Activity>();
            using ActivityListener activityListener = StartActivityListener(activities);
            await Task.Delay(500);
            string queue = $"queue-{Guid.NewGuid()}";
            const string msg = "for basic.get";

            var basicProps = useMessageId ? new BasicProperties() { MessageId = Guid.NewGuid().ToString() } : new BasicProperties();

            try
            {
                await _channel.QueueDeclareAsync(queue, false, false, false, null);
                await _channel.BasicPublishAsync("", queue, true, basicProps, Encoding.UTF8.GetBytes(msg));
                QueueDeclareOk ok = await _channel.QueueDeclarePassiveAsync(queue);
                Assert.Equal(1u, ok.MessageCount);
                BasicGetResult res = await _channel.BasicGetAsync(queue, true);
                Assert.Equal(msg, Encoding.UTF8.GetString(res.Body.ToArray()));
                ok = await _channel.QueueDeclarePassiveAsync(queue);
                Assert.Equal(0u, ok.MessageCount);
                await Task.Delay(500);
                AssertActivityData(useRoutingKeyAsOperationName, usePublisherAsParent, queue, activities, false, basicProps.MessageId);
            }
            finally
            {
                await _channel.QueueDeleteAsync(queue);
            }
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public async Task TestPublisherWithCachedStringsAndBasicGetActivityTagsAsync(bool useRoutingKeyAsOperationName, bool usePublisherAsParent)
        {
            RabbitMQActivitySource.UseRoutingKeyAsOperationName = useRoutingKeyAsOperationName;
            RabbitMQActivitySource.TracingOptions.UsePublisherAsParent = usePublisherAsParent;
            var activities = new List<Activity>();
            using ActivityListener activityListener = StartActivityListener(activities);
            await Task.Delay(500);
            string queue = $"queue-{Guid.NewGuid()}";
            const string msg = "for basic.get";

            try
            {
                CachedString exchange = new CachedString("");
                CachedString routingKey = new CachedString(queue);
                await _channel.QueueDeclareAsync(queue, false, false, false, null);
                await _channel.BasicPublishAsync(exchange, routingKey, true, Encoding.UTF8.GetBytes(msg));
                QueueDeclareOk ok = await _channel.QueueDeclarePassiveAsync(queue);
                Assert.Equal(1u, ok.MessageCount);
                BasicGetResult res = await _channel.BasicGetAsync(queue, true);
                Assert.Equal(msg, Encoding.UTF8.GetString(res.Body.ToArray()));
                ok = await _channel.QueueDeclarePassiveAsync(queue);
                Assert.Equal(0u, ok.MessageCount);
                await Task.Delay(500);
                AssertActivityData(useRoutingKeyAsOperationName, usePublisherAsParent, queue, activities, false);
            }
            finally
            {
                await _channel.QueueDeleteAsync(queue);
            }
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public async Task TestPublisherWithPublicationAddressAndBasicGetActivityTagsAsync(bool useRoutingKeyAsOperationName, bool usePublisherAsParent)
        {
            RabbitMQActivitySource.UseRoutingKeyAsOperationName = useRoutingKeyAsOperationName;
            RabbitMQActivitySource.TracingOptions.UsePublisherAsParent = usePublisherAsParent;
            var activities = new List<Activity>();
            using ActivityListener activityListener = StartActivityListener(activities);
            await Task.Delay(500);
            string queue = $"queue-{Guid.NewGuid()}";
            const string msg = "for basic.get";

            try
            {
                var publicationAddress = new PublicationAddress(ExchangeType.Direct, "", queue);
                await _channel.QueueDeclareAsync(queue, false, false, false, null);
                await _channel.BasicPublishAsync(publicationAddress, new BasicProperties(),
                    Encoding.UTF8.GetBytes(msg));
                QueueDeclareOk ok = await _channel.QueueDeclarePassiveAsync(queue);
                Assert.Equal(1u, ok.MessageCount);
                BasicGetResult res = await _channel.BasicGetAsync(queue, true);
                Assert.Equal(msg, Encoding.UTF8.GetString(res.Body.ToArray()));
                ok = await _channel.QueueDeclarePassiveAsync(queue);
                Assert.Equal(0u, ok.MessageCount);
                await Task.Delay(500);
                AssertActivityData(useRoutingKeyAsOperationName, usePublisherAsParent, queue, activities, false);
            }
            finally
            {
                await _channel.QueueDeleteAsync(queue);
            }
        }

        private static ActivityListener StartActivityListener(List<Activity> activities)
        {
            ActivityListener activityListener = new ActivityListener();
            activityListener.Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded;
            activityListener.SampleUsingParentId = (ref ActivityCreationOptions<string> _) =>
                ActivitySamplingResult.AllDataAndRecorded;
            activityListener.ShouldListenTo =
                activitySource => activitySource.Name.StartsWith("RabbitMQ.Client.");
            activityListener.ActivityStarted = activities.Add;
            ActivitySource.AddActivityListener(activityListener);
            return activityListener;
        }

        private void AssertActivityData(bool useRoutingKeyAsOperationName, bool usePublisherAsParent, string queueName,
            List<Activity> activityList, bool isDeliver = false, string messageId = null)
        {
            string childName = isDeliver ? "deliver" : "fetch";
            Activity[] activities = activityList.ToArray();
            Assert.NotEmpty(activities);

            if (IsVerbose)
            {
                foreach (Activity item in activities)
                {
                    _output.WriteLine(
                        $"{item.Context.TraceId}: {item.OperationName}");
                    _output.WriteLine($"  Tags: {string.Join(", ", item.Tags.Select(x => $"{x.Key}: {x.Value}"))}");
                    _output.WriteLine($"  Links: {string.Join(", ", item.Links.Select(x => $"{x.Context.TraceId}"))}");
                }
            }

            Activity sendActivity = activities.First(x =>
                x.OperationName == (useRoutingKeyAsOperationName ? $"publish {queueName}" : "publish") &&
                x.GetTagItem(RabbitMQActivitySource.MessagingDestinationRoutingKey) is string routingKeyTag &&
                routingKeyTag == $"{queueName}");
            Activity receiveActivity = activities.Single(x =>
                x.OperationName == (useRoutingKeyAsOperationName ? $"{childName} {queueName}" : childName));
            Assert.Equal(ActivityKind.Producer, sendActivity.Kind);
            Assert.Equal(ActivityKind.Consumer, receiveActivity.Kind);
            Assert.Equal(sendActivity.TraceId, receiveActivity.Links.Single().Context.TraceId);
            if (usePublisherAsParent)
            {
                Assert.Equal(sendActivity.Id, receiveActivity.ParentId);
                Assert.Equal(sendActivity.TraceId, receiveActivity.TraceId);
            }
            else
            {
                Assert.Null(receiveActivity.ParentId);
                Assert.NotEqual(sendActivity.TraceId, receiveActivity.TraceId);
            }
            AssertStringTagNotNullOrEmpty(sendActivity, "network.peer.address");
            AssertStringTagNotNullOrEmpty(sendActivity, "network.local.address");
            AssertStringTagNotNullOrEmpty(sendActivity, "server.address");
            AssertStringTagNotNullOrEmpty(sendActivity, "client.address");
            AssertIntTagGreaterThanZero(sendActivity, "network.peer.port");
            AssertIntTagGreaterThanZero(sendActivity, "network.local.port");
            AssertIntTagGreaterThanZero(sendActivity, "server.port");
            AssertIntTagGreaterThanZero(sendActivity, "client.port");
            AssertStringTagStartsWith(sendActivity, "network.type", "ipv");
            AssertStringTagEquals(sendActivity, RabbitMQActivitySource.MessagingSystem, "rabbitmq");
            AssertStringTagEquals(sendActivity, RabbitMQActivitySource.ProtocolName, "amqp");
            AssertStringTagEquals(sendActivity, RabbitMQActivitySource.ProtocolVersion, "0.9.1");
            AssertStringTagEquals(sendActivity, RabbitMQActivitySource.MessagingDestination, "amq.default");
            AssertStringTagEquals(sendActivity, RabbitMQActivitySource.MessagingDestinationRoutingKey, queueName);
            AssertIntTagGreaterThanZero(sendActivity, RabbitMQActivitySource.MessagingEnvelopeSize);
            AssertIntTagGreaterThanZero(sendActivity, RabbitMQActivitySource.MessagingBodySize);
            AssertIntTagGreaterThanZero(receiveActivity, RabbitMQActivitySource.MessagingBodySize);

            if (messageId is not null)
            {
                AssertStringTagEquals(sendActivity, RabbitMQActivitySource.MessageId, messageId);
                AssertStringTagEquals(receiveActivity, RabbitMQActivitySource.MessageId, messageId);
            }
        }
    }
}
