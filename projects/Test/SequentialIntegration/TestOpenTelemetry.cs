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
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Test.SequentialIntegration
{
    public class TestOpenTelemetry : SequentialIntegrationFixture
    {
        public TestOpenTelemetry(ITestOutputHelper output) : base(output)
        {
            Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
            {
                new TraceContextPropagator(), new BaggagePropagator()
            }));
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

        [Fact]
        public void TestDefaultTracingOptions()
        {
            using var tracer = Sdk.CreateTracerProviderBuilder()
                .AddRabbitMQInstrumentation()
                .Build();

            // Reads the deprecated statics deliberately: this asserts the process-wide default
            // AddRabbitMQInstrumentation installs, which is the only place it is observable.
#pragma warning disable CS0618
            Assert.True(RabbitMQActivitySource.UseRoutingKeyAsOperationName);
            Assert.True(RabbitMQActivitySource.TracingOptions.UseRoutingKeyAsOperationName);
            Assert.True(RabbitMQActivitySource.TracingOptions.UsePublisherAsParent);
#pragma warning restore CS0618
        }

        [Fact]
        public async Task TestConnectionFactoryTracingOptionsAreUsedPerConnection_GH1981()
        {
            /*
             * rabbitmq/rabbitmq-dotnet-client#1981
             *
             * Tracing configuration set on a ConnectionFactory is captured by the
             * connections it creates and used for their spans, without disturbing the
             * process-wide statics. Here the factory turns UseRoutingKeyAsOperationName
             * off while the global default stays on, so a publish span produced by a
             * connection from this factory is named "publish" (no routing key appended),
             * and the static default is unchanged.
             */
            var exportedItems = new List<Activity>();
            ConnectionFactory cf = CreateConnectionFactory();

            // Use the opposite of whatever the process-wide default currently is, so the assertion
            // proves the connection used its factory's value rather than the global one, independent
            // of any global state a prior test left behind.
#pragma warning disable CS0618 // reading the deprecated default is the point of the comparison
            bool globalBefore = RabbitMQActivitySource.TracingOptions.UseRoutingKeyAsOperationName;
#pragma warning restore CS0618
            bool factoryValue = !globalBefore;

            using TracerProvider tracer = Sdk.CreateTracerProviderBuilder()
                .AddRabbitMQInstrumentation(cf, options => options.UseRoutingKeyAsOperationName = factoryValue)
                .AddInMemoryExporter(exportedItems)
                .Build();

            string queueName;
            await using (IConnection conn = await cf.CreateConnectionAsync())
            await using (IChannel ch = await conn.CreateChannelAsync(_createChannelOptions))
            {
                queueName = (await ch.QueueDeclareAsync()).QueueName;
                await ch.BasicPublishAsync(string.Empty, queueName, Encoding.UTF8.GetBytes("hi"));
            }

            tracer.ForceFlush(5000);

            Activity publish = Assert.Single(exportedItems,
                a => a.OperationName == "publish" || a.OperationName.StartsWith("publish ", StringComparison.Ordinal));
            string expected = factoryValue ? $"publish {queueName}" : "publish";
            Assert.Equal(expected, publish.OperationName);

            // The per-connection path must not touch the process-wide default.
#pragma warning disable CS0618
            Assert.Equal(globalBefore, RabbitMQActivitySource.TracingOptions.UseRoutingKeyAsOperationName);
#pragma warning restore CS0618
        }

        [Fact]
        public async Task TestUseOpenTelemetryTracingConfiguresAFactoryWithoutATracerProviderBuilder_GH1981()
        {
            /*
             * rabbitmq/rabbitmq-dotnet-client#1981
             *
             * The dependency-injection shape, raised in review of PR #2009: the factory is
             * configured where it is built, with no TracerProviderBuilder in scope, and the
             * builder separately subscribes to the sources. AddRabbitMQInstrumentation(builder,
             * factory, ...) cannot serve this case, because the factory instance does not exist
             * at the point WithTracing configures the builder.
             *
             * Three contracts, and each assertion is chosen so that only one of them can satisfy
             * it. (1) The factory's span-shaping options beat the process-wide default: the
             * default is asserted to be true immediately after the provider is built, and the
             * factory is given false, so a span named plain "publish" can only have come from
             * the factory. (2) The factory's delegates are the ones the publish path calls:
             * `configure` wraps the injector it is handed with one that stamps a marker header,
             * and that wrapper exists nowhere else, so the marker arriving proves the factory's
             * injector ran - and, since `configure` could only wrap a delegate that was already
             * installed, that the OpenTelemetry delegates are applied before `configure` runs.
             * (3) The wrapped delegate really is OpenTelemetry's: baggage round-trips, which the
             * client's built-in propagation does not do. Note that (3) alone would also pass on
             * the process-wide default, since that has the same delegates; it is (2) that pins
             * the ownership.
             */
            const string markerHeader = "x-gh1981-marker";
            string marker = Guid.NewGuid().ToString();
            string baggageGuid = Guid.NewGuid().ToString();

            var exportedItems = new List<Activity>();
            using TracerProvider tracer = Sdk.CreateTracerProviderBuilder()
                .AddRabbitMQInstrumentation()
                .AddInMemoryExporter(exportedItems)
                .Build();

            // The overload with no configure action leaves the process-wide default at its own
            // defaults. Asserted rather than assumed, because the span-name check below is only
            // meaningful if the global value differs from the factory's.
#pragma warning disable CS0618
            Assert.True(RabbitMQActivitySource.TracingOptions.UseRoutingKeyAsOperationName);
#pragma warning restore CS0618

            ConnectionFactory cf = CreateConnectionFactory();
            ConnectionFactory returned = cf.UseOpenTelemetryTracing(options =>
            {
                options.UseRoutingKeyAsOperationName = false;
                var openTelemetryInjector = options.ContextInjector;
                options.ContextInjector = (activity, headers) =>
                {
                    openTelemetryInjector(activity, headers);
                    headers[markerHeader] = Encoding.UTF8.GetBytes(marker);
                };
            });
            Assert.Same(cf, returned);

            string receivedMarker = null;
            string receivedBaggage = null;
            var receivedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            Baggage.SetBaggage("TestItem", baggageGuid);
            try
            {
                await using (IConnection conn = await cf.CreateConnectionAsync())
                await using (IChannel ch = await conn.CreateChannelAsync(_createChannelOptions))
                {
                    string queueName = (await ch.QueueDeclareAsync()).QueueName;

                    var consumer = new AsyncEventingBasicConsumer(ch);
                    consumer.ReceivedAsync += (_, ea) =>
                    {
                        // Read what is needed here: the headers are only valid for the duration
                        // of the callback.
                        IDictionary<string, object> headers = ea.BasicProperties.Headers;
                        if (headers != null && headers.TryGetValue(markerHeader, out object value) &&
                            value is byte[] bytes)
                        {
                            receivedMarker = Encoding.UTF8.GetString(bytes);
                        }

                        receivedBaggage = Baggage.GetBaggage("TestItem");
                        receivedTcs.TrySetResult(true);
                        return Task.CompletedTask;
                    };

                    await ch.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);
                    await ch.BasicPublishAsync(string.Empty, queueName, Encoding.UTF8.GetBytes("hi"));

                    await receivedTcs.Task.WaitAsync(WaitSpan);
                }
            }
            finally
            {
                Baggage.ClearBaggage();
            }

            tracer.ForceFlush(5000);

            Assert.Equal(marker, receivedMarker);
            Assert.Equal(baggageGuid, receivedBaggage);

            Activity publish = Assert.Single(exportedItems,
                a => a.OperationName == "publish" || a.OperationName.StartsWith("publish ", StringComparison.Ordinal));
            Assert.Equal("publish", publish.OperationName);

            // Configuring the factory must not disturb the process-wide default.
#pragma warning disable CS0618
            Assert.True(RabbitMQActivitySource.TracingOptions.UseRoutingKeyAsOperationName);
#pragma warning restore CS0618
        }

        [Fact]
        public void TestProcessWideConfigureCanReplaceThePropagationDelegates()
        {
            /*
             * The builder-only overload applies the OpenTelemetry delegates before running
             * `configure`, so a caller can wrap or replace them - the same contract
             * UseOpenTelemetryTracing offers. Applying them afterwards would silently discard
             * whatever `configure` set, because assigning RabbitMQActivitySource.TracingOptions
             * copies only the span-shaping flags out of the instance it is handed; the delegates
             * live in separate slots. This contract is new in 7.3.0: before the propagation
             * delegates moved onto RabbitMQTracingOptions they could not be reached through
             * `configure` at all.
             */
            using var scope = new ProcessWideTracingScope();

            var customInjector = new Action<Activity, IDictionary<string, object>>((_, _) => { });
            var customExtractor = new Func<IReadOnlyBasicProperties, ActivityContext>(_ => default);

            using TracerProvider tracer = Sdk.CreateTracerProviderBuilder()
                .AddRabbitMQInstrumentation(options =>
                {
                    options.ContextInjector = customInjector;
                    options.ContextExtractor = customExtractor;
                })
                .Build();

#pragma warning disable CS0618
            Assert.Same(customInjector, RabbitMQActivitySource.ContextInjector);
            Assert.Same(customExtractor, RabbitMQActivitySource.ContextExtractor);
#pragma warning restore CS0618
        }

        /*
         * Saves and restores everything the deprecated statics hold. The flags alone are not
         * enough here: a test that installs its own propagation delegates would otherwise leave
         * them in place for the rest of the process, and this class and TestActivitySource both
         * depend on the OpenTelemetry delegates being the process-wide default.
         */
        private sealed class ProcessWideTracingScope : IDisposable
        {
            private readonly bool _useRoutingKeyAsOperationName;
            private readonly bool _usePublisherAsParent;
            private readonly Action<Activity, IDictionary<string, object>> _contextInjector;
            private readonly Func<IReadOnlyBasicProperties, ActivityContext> _contextExtractor;

#pragma warning disable CS0618
            public ProcessWideTracingScope()
            {
                _useRoutingKeyAsOperationName = RabbitMQActivitySource.TracingOptions.UseRoutingKeyAsOperationName;
                _usePublisherAsParent = RabbitMQActivitySource.TracingOptions.UsePublisherAsParent;
                _contextInjector = RabbitMQActivitySource.ContextInjector;
                _contextExtractor = RabbitMQActivitySource.ContextExtractor;
            }

            public void Dispose()
            {
                RabbitMQActivitySource.TracingOptions.UseRoutingKeyAsOperationName = _useRoutingKeyAsOperationName;
                RabbitMQActivitySource.TracingOptions.UsePublisherAsParent = _usePublisherAsParent;
                RabbitMQActivitySource.ContextInjector = _contextInjector;
                RabbitMQActivitySource.ContextExtractor = _contextExtractor;
            }
#pragma warning restore CS0618
        }

        [Fact]
        public void TestContextExtractorHandlesPropertiesWithNoHeaders_GH1967()
        {
            /*
             * rabbitmq/rabbitmq-dotnet-client#1967
             *
             * OpenTelemetryContextExtractor passed props.Headers straight to the
             * propagator, so a message published with no headers at all called the
             * getter once per propagator field with a null carrier. It worked only
             * because the getter's blanket catch swallowed the resulting
             * NullReferenceException.
             *
             * This pins two observable contracts. First, no headers extracts to no
             * context without throwing - that half would also have passed before the
             * fix, because swallowing the NRE reached the same result, and it protects
             * the outcome if someone later narrows or removes that catch. Second, a
             * header-less extract resets ambient baggage: Baggage.Current is AsyncLocal
             * and the dispatcher reuses one async flow across deliveries, so without the
             * reset a header-less message would inherit the previous message's baggage.
             * That half fails on the pre-fix early return, which skipped the reset.
             */
            using var tracer = Sdk.CreateTracerProviderBuilder()
                .AddRabbitMQInstrumentation()
                .Build();

            var propsWithNoHeaders = new BasicProperties();
            Assert.Null(propsWithNoHeaders.Headers);

            Baggage.SetBaggage("TestItem", "should-be-cleared");
            Assert.Equal("should-be-cleared", Baggage.GetBaggage("TestItem"));

            try
            {
                // The extractor under test is the one AddRabbitMQInstrumentation installed as the
                // process-wide default, so the deprecated static is how the test reaches it.
#pragma warning disable CS0618
                ActivityContext extracted = RabbitMQActivitySource.ContextExtractor(propsWithNoHeaders);
#pragma warning restore CS0618

                Assert.Equal(default, extracted);
                Assert.Null(Baggage.GetBaggage("TestItem"));
            }
            finally
            {
                Baggage.ClearBaggage();
            }
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public async Task TestPublisherAndConsumerActivityTagsAsync(bool useRoutingKeyAsOperationName, bool usePublisherAsParent)
        {
            var exportedItems = new List<Activity>();
            using var tracer = Sdk.CreateTracerProviderBuilder()
                .AddRabbitMQInstrumentation(options =>
                {
                    options.UseRoutingKeyAsOperationName = useRoutingKeyAsOperationName;
                    options.UsePublisherAsParent = usePublisherAsParent;
                })
                .AddInMemoryExporter(exportedItems)
                .Build();
            string baggageGuid = Guid.NewGuid().ToString();
            Baggage.SetBaggage("TestItem", baggageGuid);
            Assert.Equal(baggageGuid, Baggage.GetBaggage("TestItem"));

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
                string baggageItem = Baggage.GetBaggage("TestItem");
                if (baggageItem == baggageGuid)
                {
                    consumerReceivedTcs.SetResult(true);
                }
                else
                {
                    consumerReceivedTcs.SetException(
                        EqualException.ForMismatchedStrings(baggageGuid, baggageItem, 0, 0));
                }

                return Task.CompletedTask;
            };

            string consumerTag = await _channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);
            await _channel.BasicPublishAsync("", q.QueueName, true, sendBody);
            Baggage.ClearBaggage();
            Assert.Null(Baggage.GetBaggage("TestItem"));

            await consumerReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(await consumerReceivedTcs.Task);

            await _channel.BasicCancelAsync(consumerTag);
            await Task.Delay(500);
            AssertActivityData(useRoutingKeyAsOperationName, usePublisherAsParent, queueName, exportedItems, true);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public async Task TestPublisherWithPublicationAddressAndConsumerActivityTagsAsync(bool useRoutingKeyAsOperationName, bool usePublisherAsParent)
        {
            var exportedItems = new List<Activity>();
            using var tracer = Sdk.CreateTracerProviderBuilder()
                .AddRabbitMQInstrumentation(options =>
                {
                    options.UseRoutingKeyAsOperationName = useRoutingKeyAsOperationName;
                    options.UsePublisherAsParent = usePublisherAsParent;
                })
                .AddInMemoryExporter(exportedItems)
                .Build();
            string baggageGuid = Guid.NewGuid().ToString();
            Baggage.SetBaggage("TestItem", baggageGuid);
            Assert.Equal(baggageGuid, Baggage.GetBaggage("TestItem"));

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
                string baggageItem = Baggage.GetBaggage("TestItem");
                if (baggageItem == baggageGuid)
                {
                    consumerReceivedTcs.SetResult(true);
                }
                else
                {
                    consumerReceivedTcs.SetException(
                        EqualException.ForMismatchedStrings(baggageGuid, baggageItem, 0, 0));
                }

                return Task.CompletedTask;
            };

            string consumerTag = await _channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);
            var publicationAddress = new PublicationAddress(ExchangeType.Direct, "", queueName);
            await _channel.BasicPublishAsync(publicationAddress, new BasicProperties(), sendBody);
            Baggage.ClearBaggage();
            Assert.Null(Baggage.GetBaggage("TestItem"));

            await consumerReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(await consumerReceivedTcs.Task);

            await _channel.BasicCancelAsync(consumerTag);
            await Task.Delay(500);
            AssertActivityData(useRoutingKeyAsOperationName, usePublisherAsParent, queueName, exportedItems, true);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public async Task TestPublisherWithCachedStringsAndConsumerActivityTagsAsync(bool useRoutingKeyAsOperationName, bool usePublisherAsParent)
        {
            var exportedItems = new List<Activity>();
            using var tracer = Sdk.CreateTracerProviderBuilder()
                .AddRabbitMQInstrumentation(options =>
                {
                    options.UseRoutingKeyAsOperationName = useRoutingKeyAsOperationName;
                    options.UsePublisherAsParent = usePublisherAsParent;
                })
                .AddInMemoryExporter(exportedItems)
                .Build();
            string baggageGuid = Guid.NewGuid().ToString();
            Baggage.SetBaggage("TestItem", baggageGuid);
            Assert.Equal(baggageGuid, Baggage.GetBaggage("TestItem"));

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
                string baggageItem = Baggage.GetBaggage("TestItem");
                if (baggageItem == baggageGuid)
                {
                    consumerReceivedTcs.SetResult(true);
                }
                else
                {
                    consumerReceivedTcs.SetException(
                        EqualException.ForMismatchedStrings(baggageGuid, baggageItem, 0, 0));
                }

                return Task.CompletedTask;
            };

            string consumerTag = await _channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);
            CachedString exchange = new CachedString("");
            CachedString routingKey = new CachedString(queueName);
            await _channel.BasicPublishAsync(exchange, routingKey, sendBody);
            Baggage.ClearBaggage();
            Assert.Null(Baggage.GetBaggage("TestItem"));

            await consumerReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(await consumerReceivedTcs.Task);

            await _channel.BasicCancelAsync(consumerTag);
            await Task.Delay(500);
            AssertActivityData(useRoutingKeyAsOperationName, usePublisherAsParent, queueName, exportedItems, true);
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
            var exportedItems = new List<Activity>();
            using var tracer = Sdk.CreateTracerProviderBuilder()
                .AddRabbitMQInstrumentation(options =>
                {
                    options.UseRoutingKeyAsOperationName = useRoutingKeyAsOperationName;
                    options.UsePublisherAsParent = usePublisherAsParent;
                })
                .AddInMemoryExporter(exportedItems)
                .Build();
            string baggageGuid = Guid.NewGuid().ToString();
            Baggage.SetBaggage("TestItem", baggageGuid);
            Assert.Equal(baggageGuid, Baggage.GetBaggage("TestItem"));
            await Task.Delay(500);
            string queue = $"queue-{Guid.NewGuid()}";
            const string msg = "for basic.get";

            var basicProps = useMessageId ? new BasicProperties() { MessageId = Guid.NewGuid().ToString() } : new BasicProperties();

            try
            {
                await _channel.QueueDeclareAsync(queue, false, true, false, null);
                await _channel.BasicPublishAsync("", queue, true, basicProps, Encoding.UTF8.GetBytes(msg));
                Baggage.ClearBaggage();
                Assert.Null(Baggage.GetBaggage("TestItem"));
                QueueDeclareOk ok = await _channel.QueueDeclarePassiveAsync(queue);
                Assert.Equal(1u, ok.MessageCount);
                BasicGetResult res = await _channel.BasicGetAsync(queue, true);
                Assert.Equal(msg, Encoding.UTF8.GetString(res.Body.ToArray()));
                ok = await _channel.QueueDeclarePassiveAsync(queue);
                Assert.Equal(0u, ok.MessageCount);
                await Task.Delay(500);
                AssertActivityData(useRoutingKeyAsOperationName, usePublisherAsParent, queue, exportedItems, false, basicProps.MessageId);
            }
            finally
            {
                await _channel.QueueDeleteAsync(queue);
            }
        }

        private void AssertActivityData(bool useRoutingKeyAsOperationName, bool usePublisherAsParent, string queueName,
            List<Activity> activityList, bool isDeliver = false, string messageId = null)
        {
            string childName = isDeliver ? "deliver" : "fetch";
            string childType = isDeliver ? "process" : "receive";
            Activity[] activities = activityList.ToArray();
            Assert.NotEmpty(activities);
            foreach (var item in activities)
            {
                _output.WriteLine(
                    $"{item.Context.TraceId}: {item.OperationName}");
                _output.WriteLine($"  Tags: {string.Join(", ", item.Tags.Select(x => $"{x.Key}: {x.Value}"))}");
                _output.WriteLine($"  Links: {string.Join(", ", item.Links.Select(x => $"{x.Context.TraceId}"))}");
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
            AssertStringTagEquals(receiveActivity, RabbitMQActivitySource.MessagingOperationType, childType);
            AssertStringTagEquals(receiveActivity, RabbitMQActivitySource.MessagingOperationName, childName);
            AssertStringTagEquals(sendActivity, RabbitMQActivitySource.MessagingOperationType, "send");
            AssertStringTagEquals(sendActivity, RabbitMQActivitySource.MessagingOperationName, "publish");

            if (messageId is not null)
            {
                AssertStringTagEquals(sendActivity, RabbitMQActivitySource.MessageId, messageId);
                AssertStringTagEquals(receiveActivity, RabbitMQActivitySource.MessageId, messageId);
            }
        }
    }
}
