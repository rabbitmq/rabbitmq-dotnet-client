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


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;
using Xunit;
using Xunit.Abstractions;

namespace Test.SequentialIntegration
{
    public class TestTracingDefects : SequentialIntegrationFixture
    {
        public TestTracingDefects(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task ADeliverySpanIsNotAChildOfTheSpanThatCreatedTheChannel_GH2154()
        {
            using var appSource = new ActivitySource("Test.GH2154");
            using var appRecorder = new ActivityRecorder(appSource.Name, "create channel") { VerifyParent = false };
            using var deliverRecorder = new ActivityRecorder(RabbitMQActivitySource.SubscriberSourceName, "deliver")
            { VerifyParent = false };
            string queueName = GenerateQueueName();

            ConnectionFactory cf = CreateConnectionFactory();
            cf.TracingOptions = new ConnectionTracingOptions { UseRoutingKeyAsOperationName = false };
            await using IConnection conn = await cf.CreateConnectionAsync();
            IChannel ch;
            using (Activity creating = appSource.StartActivity("create channel"))
            {
                Assert.NotNull(creating);
                ch = await conn.CreateChannelAsync();
            }

            await using (ch)
            {
                await ch.QueueDeclareAsync(queueName, durable: false, exclusive: true, autoDelete: false);
                var consumer = new AsyncEventingBasicConsumer(ch);
                var receivedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                consumer.ReceivedAsync += (_, _) =>
                {
                    receivedTcs.TrySetResult(true);
                    return Task.CompletedTask;
                };
                await ch.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);
                // The publisher source has no listener here, so the message carries no trace context.
                await ch.BasicPublishAsync("", queueName, Encoding.UTF8.GetBytes("hi"));
                await receivedTcs.Task.WaitAsync(WaitSpan);
                Assert.True(await receivedTcs.Task);
            }

            Activity deliver = deliverRecorder.VerifyActivityRecordedOnce();
            Assert.Null(deliver.ParentId);
        }

        [Fact]
        public async Task AThrowingPropagatorDoesNotStopADeliveryOrAFetch_GH2154()
        {
            using var deliverRecorder = new ActivityRecorder(RabbitMQActivitySource.SubscriberSourceName, "deliver")
            { VerifyParent = false };
            string queueName = GenerateQueueName();
            ConnectionFactory cf = CreateConnectionFactory();
            cf.TracingOptions = new ConnectionTracingOptions { UseRoutingKeyAsOperationName = false, Propagator = new ThrowingPropagator() };
            await using IConnection conn = await cf.CreateConnectionAsync();
            await using IChannel ch = await conn.CreateChannelAsync();
            await ch.QueueDeclareAsync(queueName, durable: false, exclusive: true, autoDelete: false);

            await ch.BasicPublishAsync("", queueName, Encoding.UTF8.GetBytes("fetched"));
            BasicGetResult fetched = await ch.BasicGetAsync(queueName, autoAck: true);
            Assert.NotNull(fetched);

            var consumer = new AsyncEventingBasicConsumer(ch);
            var receivedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            consumer.ReceivedAsync += (_, _) =>
            {
                receivedTcs.TrySetResult(true);
                return Task.CompletedTask;
            };
            await ch.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);
            await ch.BasicPublishAsync("", queueName, Encoding.UTF8.GetBytes("delivered"));
            await receivedTcs.Task.WaitAsync(WaitSpan);
            Assert.True(await receivedTcs.Task);
            deliverRecorder.VerifyActivityRecordedOnce();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AnExtractedParentIsRemote_GH2154(bool connectionPropagator)
        {
            using var publishRecorder = new ActivityRecorder(RabbitMQActivitySource.PublisherSourceName, "publish")
            { VerifyParent = false };
            using var deliverRecorder = new ActivityRecorder(RabbitMQActivitySource.SubscriberSourceName, "deliver")
            { VerifyParent = false };
            string queueName = GenerateQueueName();
            ConnectionFactory cf = CreateConnectionFactory();
            cf.TracingOptions = new ConnectionTracingOptions
            {
                UseRoutingKeyAsOperationName = false,
                UsePublisherAsParent = true,
                Propagator = connectionPropagator ? DistributedContextPropagator.CreateDefaultPropagator() : null,
            };
            await using IConnection conn = await cf.CreateConnectionAsync();
            await using IChannel ch = await conn.CreateChannelAsync();
            await ch.QueueDeclareAsync(queueName, durable: false, exclusive: true, autoDelete: false);
            var consumer = new AsyncEventingBasicConsumer(ch);
            var receivedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            consumer.ReceivedAsync += (_, _) =>
            {
                receivedTcs.TrySetResult(true);
                return Task.CompletedTask;
            };
            await ch.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);
            await ch.BasicPublishAsync("", queueName, Encoding.UTF8.GetBytes("hi"));
            await receivedTcs.Task.WaitAsync(WaitSpan);
            Assert.True(await receivedTcs.Task);

            Activity deliver = deliverRecorder.VerifyActivityRecordedOnce();
            Assert.Equal(publishRecorder.VerifyActivityRecordedOnce().SpanId, deliver.ParentSpanId);
            // A context read from a message came from another process; ParentBased samplers depend on this.
            Assert.True(deliver.HasRemoteParent);
            Assert.True(deliver.Links.Single().Context.IsRemote);
        }

        [Fact]
        public async Task TheProcessWidePropagatorSeesAMessageWithoutTraceHeaders_GH2154()
        {
            DistributedContextPropagator original = DistributedContextPropagator.Current;
            var counting = new CountingPropagator(original);
            DistributedContextPropagator.Current = counting;
            try
            {
                using var deliverRecorder = new ActivityRecorder(RabbitMQActivitySource.SubscriberSourceName, "deliver")
                { VerifyParent = false };
                string queueName = GenerateQueueName();
                ConnectionFactory cf = CreateConnectionFactory();
                cf.TracingOptions = new ConnectionTracingOptions { UseRoutingKeyAsOperationName = false };
                await using IConnection conn = await cf.CreateConnectionAsync();
                await using IChannel ch = await conn.CreateChannelAsync();
                await ch.QueueDeclareAsync(queueName, durable: false, exclusive: true, autoDelete: false);
                // No publisher listener, so the message has no headers at all.
                await ch.BasicPublishAsync("", queueName, Encoding.UTF8.GetBytes("hi"));
                BasicGetResult fetched = await ch.BasicGetAsync(queueName, autoAck: true);
                Assert.NotNull(fetched);
                Assert.Null(fetched.BasicProperties.Headers);

                Assert.Equal(1, counting.Extractions);
            }
            finally
            {
                DistributedContextPropagator.Current = original;
            }
        }

        private sealed class ThrowingPropagator : DistributedContextPropagator
        {
            public override IReadOnlyCollection<string> Fields { get; } = new[] { "traceparent" };

            public override void Inject(Activity activity, object carrier, PropagatorSetterCallback setter)
            {
            }

            public override void ExtractTraceIdAndState(object carrier, PropagatorGetterCallback getter,
                out string traceId, out string traceState)
                => throw new InvalidOperationException("propagator bug");

            public override IEnumerable<KeyValuePair<string, string>> ExtractBaggage(object carrier, PropagatorGetterCallback getter)
                => throw new InvalidOperationException("propagator bug");
        }

        private sealed class CountingPropagator : DistributedContextPropagator
        {
            private readonly DistributedContextPropagator _inner;

            public CountingPropagator(DistributedContextPropagator inner) => _inner = inner;

            public int Extractions { get; private set; }

            public override IReadOnlyCollection<string> Fields => _inner.Fields;

            public override void Inject(Activity activity, object carrier, PropagatorSetterCallback setter)
                => _inner.Inject(activity, carrier, setter);

            public override void ExtractTraceIdAndState(object carrier, PropagatorGetterCallback getter,
                out string traceId, out string traceState)
            {
                Extractions++;
                _inner.ExtractTraceIdAndState(carrier, getter, out traceId, out traceState);
            }

            public override IEnumerable<KeyValuePair<string, string>> ExtractBaggage(object carrier, PropagatorGetterCallback getter)
                => _inner.ExtractBaggage(carrier, getter);
        }

#pragma warning disable CS0618 // the process-wide options are the only ones a running publish can see change
        [Fact]
        public async Task APublishSpanCarriesBothSizeAttributesOrNeither_GH2154()
        {
            using var scope = new TracingConfigurationScope();
            var options = new RabbitMQTracingOptions { IncludeMessageSizes = true, UseRoutingKeyAsOperationName = false };
            RabbitMQActivitySource.TracingOptions = options;

            var finished = new List<Activity>();
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == RabbitMQActivitySource.PublisherSourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                // Flips the setting after the publish span resolved it and before its frames are written.
                ActivityStarted = _ => options.IncludeMessageSizes = false,
                ActivityStopped = finished.Add,
            };
            ActivitySource.AddActivityListener(listener);

            await using IConnection conn = await CreateConnectionFactory().CreateConnectionAsync();
            await using IChannel ch = await conn.CreateChannelAsync();
            await ch.BasicPublishAsync("", GenerateQueueName(), Encoding.UTF8.GetBytes("hi"));

            Activity publish = Assert.Single(finished, a => a.OperationName == "publish");
            publish.HasTag(RabbitMQActivitySource.MessagingBodySize, 2);
            publish.HasTag(RabbitMQActivitySource.MessagingEnvelopeSize);
        }
#pragma warning restore CS0618
    }
}
