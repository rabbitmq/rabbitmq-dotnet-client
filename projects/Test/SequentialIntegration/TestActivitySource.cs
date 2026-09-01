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
using System.Threading;
using System.Threading.Tasks;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
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
            using var tracingOptions = new TracingOptionsScope();
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
            using var tracingOptions = new TracingOptionsScope();
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
            using var tracingOptions = new TracingOptionsScope();
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
            using var tracingOptions = new TracingOptionsScope();
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
                await _channel.QueueDeclareAsync(queue, false, true, false, null);
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
            using var tracingOptions = new TracingOptionsScope();
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
                await _channel.QueueDeclareAsync(queue, false, true, false, null);
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
            using var tracingOptions = new TracingOptionsScope();
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
                await _channel.QueueDeclareAsync(queue, false, true, false, null);
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

        /// <summary>
        /// Scopes UseRoutingKeyAsOperationName to false, restoring it on dispose.
        /// </summary>
        /// <remarks>
        /// It defaults to true, which appends the routing key to the span name. The tests
        /// below match spans by name via <see cref="ActivityRecorder"/>, so they need the
        /// plain name. Restoring on dispose keeps this from becoming one more test that
        /// leaves process-global tracing state mutated for whatever runs next - see the
        /// public-API discussion on rabbitmq/rabbitmq-dotnet-client#1967.
        /// </remarks>
        private sealed class PlainOperationNames : IDisposable
        {
            private readonly bool _previous;

            public PlainOperationNames()
            {
                _previous = RabbitMQActivitySource.UseRoutingKeyAsOperationName;
                RabbitMQActivitySource.UseRoutingKeyAsOperationName = false;
            }

            public void Dispose() => RabbitMQActivitySource.UseRoutingKeyAsOperationName = _previous;
        }

        /// <summary>
        /// Captures UseRoutingKeyAsOperationName and UsePublisherAsParent on construction
        /// and restores both on dispose. The parameterized tracing-tag tests set these
        /// process-global options to their theory inputs; without restoring them the last
        /// case leaves them mutated for whatever test runs next in the same process - see
        /// the public-API discussion on rabbitmq/rabbitmq-dotnet-client#1967.
        /// </summary>
        private sealed class TracingOptionsScope : IDisposable
        {
            private readonly bool _useRoutingKeyAsOperationName;
            private readonly bool _usePublisherAsParent;

            public TracingOptionsScope()
            {
                _useRoutingKeyAsOperationName = RabbitMQActivitySource.UseRoutingKeyAsOperationName;
                _usePublisherAsParent = RabbitMQActivitySource.TracingOptions.UsePublisherAsParent;
            }

            public void Dispose()
            {
                RabbitMQActivitySource.UseRoutingKeyAsOperationName = _useRoutingKeyAsOperationName;
                RabbitMQActivitySource.TracingOptions.UsePublisherAsParent = _usePublisherAsParent;
            }
        }

        [Fact]
        public async Task TestPublishFailureIsRecordedOnTheSendActivity_GH1967()
        {
            /*
             * rabbitmq/rabbitmq-dotnet-client#1967
             *
             * The `using Activity? sendActivity` in BasicPublishCoreAsync used to be
             * scoped to the inner try, so both the catch and the confirmation await in
             * the finally ran after the span was disposed. Publish failures were
             * therefore never recorded: the span ended status=Unset with no exception
             * event, which every tracing backend reads as a successful publish.
             *
             * A mandatory publish to an exchange with no matching binding is the
             * cleanest trigger, and it specifically covers the finally path, since
             * PublishException surfaces from the confirmation await rather than from
             * the send itself.
             */
            using var plainNames = new PlainOperationNames();

            using ActivityRecorder publishRecorder =
                new(RabbitMQActivitySource.PublisherSourceName, "publish");
            publishRecorder.VerifyParent = false;

            string exchange = $"exchange-{Guid.NewGuid()}";
            await _channel.ExchangeDeclareAsync(exchange, ExchangeType.Direct, autoDelete: true);

            try
            {
                await Assert.ThrowsAsync<PublishReturnException>(() =>
                    _channel.BasicPublishAsync(exchange, "no-such-routing-key", mandatory: true,
                        Encoding.UTF8.GetBytes("unroutable")).AsTask());

                Activity publishActivity = publishRecorder.VerifyActivityRecordedOnce();
                publishActivity.RecordsFailure(typeof(PublishReturnException));
            }
            finally
            {
                await _channel.ExchangeDeleteAsync(exchange);
            }
        }

        [Fact]
        public async Task TestPublishFailureIsRecordedOnceWhenHandledByConfirmations_GH1967()
        {
            /*
             * rabbitmq/rabbitmq-dotnet-client#1967
             *
             * Recording the failure in both the inner catch and the one in the finally
             * used to double-record it on this path. Publishing on a closed connection
             * throws from the send, the inner catch hands the exception to the
             * confirmation task (so the publish counts as handled and does not
             * rethrow there), and awaiting that task in the finally re-raises the same
             * instance - one failure, recorded twice.
             *
             * Publisher confirmations *and* tracking are both required to reproduce:
             * without tracking there is no task to store the exception on, so it is
             * never handled and never resurfaces.
             */
            using var plainNames = new PlainOperationNames();

            using ActivityRecorder publishRecorder =
                new(RabbitMQActivitySource.PublisherSourceName, "publish");
            publishRecorder.VerifyParent = false;

            ConnectionFactory cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = false;

            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);

            await using IConnection conn = await cf.CreateConnectionAsync();
            await using IChannel ch = await conn.CreateChannelAsync(channelOptions);

            await conn.CloseAsync();

            await Assert.ThrowsAsync<AlreadyClosedException>(() =>
                ch.BasicPublishAsync("", "no-such-queue", true,
                    Encoding.UTF8.GetBytes("after close")).AsTask());

            Activity publishActivity = publishRecorder.VerifyActivityRecordedOnce();
            publishActivity.RecordsFailure(typeof(AlreadyClosedException));
        }

        [Fact]
        public async Task TestCallerCancellationIsNotRecordedAsPublishFailure_GH1967()
        {
            /*
             * rabbitmq/rabbitmq-dotnet-client#1967
             *
             * Once publish failures are recorded on the send activity, a publish the
             * caller cancels is not a failure of the publish and must not be recorded
             * as one. Without the guard the cancelled publish ended status=Error with a
             * TaskCanceledException event, so an app cancelling its own publishes traced
             * as a stream of publish errors.
             *
             * With confirmations enabled the publish parks awaiting the broker's
             * confirmation, which BasicPublishCoreAsync awaits in its finally - after the
             * send activity has been created. The test publishes while blocked (the
             * broker then stops reading the socket, so the mandatory publish parks in
             * that await instead of being returned as unroutable) and waits for the
             * connection.blocked notification to confirm the publish has parked before
             * cancelling the token, which throws OperationCanceledException from the
             * await. That is the window the finally's cancellation guard covers; removing
             * it fails this test with an Error status on the span.
             */
            using var plainNames = new PlainOperationNames();

            using ActivityRecorder publishRecorder =
                new(RabbitMQActivitySource.PublisherSourceName, "publish");
            publishRecorder.VerifyParent = false;

            using var cts = new CancellationTokenSource();

            var connectionBlockedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task OnConnectionBlockedAsync(object sender, ConnectionBlockedEventArgs args)
            {
                connectionBlockedTcs.TrySetResult(true);
                return Task.CompletedTask;
            }
            _conn.ConnectionBlockedAsync += OnConnectionBlockedAsync;

            try
            {
                await BlockAsync();

                // Publish while blocked. The broker has stopped reading the socket, so the
                // mandatory publish parks in the confirmation await rather than being
                // returned as unroutable, and it is the publish that makes the broker send
                // the connection.blocked notification.
                ValueTask publishTask = _channel.BasicPublishAsync("", "no-such-queue", true,
                    Encoding.UTF8.GetBytes("cancel me"), cts.Token);

                /*
                 * Wait for the blocked notification to confirm the publish has parked
                 * before cancelling. Relying on BlockAsync's fixed settle time alone let a
                 * slow CI run process the publish before the memory alarm engaged,
                 * returning the unroutable mandatory message and surfacing a
                 * PublishReturnException instead of the OperationCanceledException this
                 * test asserts.
                 */
                await connectionBlockedTcs.Task.WaitAsync(WaitSpan);

                // The publish is now parked in the confirmation await with its span open.
                cts.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publishTask.AsTask());
            }
            finally
            {
                _conn.ConnectionBlockedAsync -= OnConnectionBlockedAsync;
                await UnblockAsync();
            }

            Activity publishActivity = publishRecorder.VerifyActivityRecordedOnce();
            Assert.NotEqual(ActivityStatusCode.Error, publishActivity.Status);
            Assert.DoesNotContain(publishActivity.Events, e => e.Name == "exception");
            publishActivity.HasNoTag("error.type");
        }

        [Fact]
        public async Task TestConsumerFailureIsRecordedOnTheDeliverActivity_GH1967()
        {
            /*
             * rabbitmq/rabbitmq-dotnet-client#1967
             *
             * Same defect on the consume side: AsyncConsumerDispatcher's reporting
             * catch sits outside the deliver activity's `using`, so a consumer callback
             * that threw was reported via CallbackExceptionAsync but left the deliver
             * span status=Unset with no exception event. A consumer failing on every
             * message traced as completely healthy.
             */
            using var plainNames = new PlainOperationNames();

            using ActivityRecorder deliverRecorder =
                new(RabbitMQActivitySource.SubscriberSourceName, "deliver");
            deliverRecorder.VerifyParent = false;

            string queue = $"queue-{Guid.NewGuid()}";
            await _channel.QueueDeclareAsync(queue, false, true, false, null);

            var callbackExceptionTcs =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _channel.CallbackExceptionAsync += (_, _) =>
            {
                callbackExceptionTcs.TrySetResult(true);
                return Task.CompletedTask;
            };

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += (_, _) =>
                throw new InvalidOperationException("consumer callback failed on purpose");

            await _channel.BasicConsumeAsync(queue, autoAck: true, consumer: consumer);
            await _channel.BasicPublishAsync("", queue, true, Encoding.UTF8.GetBytes("hi"));

            await callbackExceptionTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Activity deliverActivity = deliverRecorder.VerifyActivityRecordedOnce();
            deliverActivity.RecordsFailure(typeof(InvalidOperationException));
        }

        [Fact]
        public async Task TestAmqpOperationsDoNotTagAnUnrelatedAmbientActivity_GH1967()
        {
            /*
             * rabbitmq/rabbitmq-dotnet-client#1967
             *
             * SessionBase.TransmitAsync and Connection.WriteAsync tag whatever
             * Activity.Current happens to be, because the frame-writing path has no
             * reference to the publish activity it belongs to. With no ownership check,
             * any AMQP method issued inside an unrelated ambient activity stamped
             * messaging and network tags onto a span this library does not own - an
             * app's own span, or an ASP.NET request span, picking up server.address and
             * messaging.message.envelope.size from an incidental QueueDeclare.
             *
             * The publisher source must have listeners for this to reproduce, which is
             * what the recorder here provides.
             */
            using var plainNames = new PlainOperationNames();

            using ActivityRecorder publishRecorder =
                new(RabbitMQActivitySource.PublisherSourceName, "publish");
            publishRecorder.VerifyParent = false;

            using var appSource = new ActivitySource("TestApp.GH1967");
            using var appListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "TestApp.GH1967",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded
            };
            ActivitySource.AddActivityListener(appListener);

            string queue = $"queue-{Guid.NewGuid()}";

            // Kept alive past the AMQP calls so the parenting assertion at the end can
            // compare against it.
            using Activity appActivity = appSource.StartActivity("app-operation");
            Assert.NotNull(appActivity);
            Assert.Same(appActivity, Activity.Current);

            await _channel.QueueDeclareAsync(queue, false, true, false, null);
            await _channel.QueueDeclarePassiveAsync(queue);
            await _channel.BasicQosAsync(0, 1, false);

            /*
             * Every tag either path would have written. The network tags come from
             * Connection.WriteAsync, the envelope size from SessionBase.
             */
            appActivity.HasNoTag("messaging.message.envelope.size");
            appActivity.HasNoTag("messaging.system");
            appActivity.HasNoTag("network.type");
            appActivity.HasNoTag("server.address");
            appActivity.HasNoTag("server.port");
            appActivity.HasNoTag("network.peer.address");
            appActivity.HasNoTag("network.peer.port");
            appActivity.HasNoTag("client.address");
            appActivity.HasNoTag("client.port");
            appActivity.HasNoTag("network.local.address");
            appActivity.HasNoTag("network.local.port");

            /*
             * Publish inside the same ambient scope. The library's own publish span must
             * still get the tags - this is an ownership check, not a blanket removal -
             * and the app's span must still come out clean, even though a publish is
             * exactly the operation whose tags it was previously stealing.
             */
            await _channel.BasicPublishAsync("", queue, true, Encoding.UTF8.GetBytes("hi"));

            appActivity.HasNoTag("messaging.message.envelope.size");
            appActivity.HasNoTag("server.port");
            appActivity.HasNoTag("network.peer.address");

            Activity publishActivity = publishRecorder.VerifyActivityRecordedOnce();
            publishActivity.HasTag("messaging.message.envelope.size");
            publishActivity.HasTag("server.port");
            publishActivity.HasTag("network.peer.address");

            /*
             * Parenting is asserted here rather than through the recorder's
             * VerifyParent, because ExpectedParent has to be set before the recorder
             * sees anything and the ambient activity does not exist that early. The
             * publish span is started while appActivity is current, so it must be its
             * child: scoping the tags to the publisher source must not also detach the
             * span from the caller's trace. See issue #1967.
             */
            Assert.Same(appActivity, publishActivity.Parent);
        }

        [Fact]
        public async Task TestConnectionActivityHasNoMessagingEnvelopeSize_GH1967()
        {
            /*
             * rabbitmq/rabbitmq-dotnet-client#1967
             *
             * The client's own "connection attempt" span used to pick up
             * messaging.message.envelope.size from the handshake frames, because it was
             * Activity.Current while they were transmitted. It is a connection span,
             * not a publish operation, so a messaging attribute has no business on it.
             *
             * This is why the ownership check tests the publisher source specifically
             * rather than "any activity from this library".
             */
            using ActivityRecorder connectionRecorder =
                new(RabbitMQActivitySource.ConnectionSourceName, "connection attempt");
            connectionRecorder.VerifyParent = false;

            // The publisher source must have listeners, or the tagging path is skipped
            // entirely and the test would pass without exercising the check.
            using ActivityRecorder publishRecorder =
                new(RabbitMQActivitySource.PublisherSourceName, "publish");
            publishRecorder.VerifyParent = false;

            ConnectionFactory cf = CreateConnectionFactory();
            await using (IConnection conn = await cf.CreateConnectionAsync())
            {
                await conn.CloseAsync();
            }

            Activity connectionActivity = connectionRecorder.VerifyActivityRecordedOnce();
            connectionActivity.HasNoTag("messaging.message.envelope.size");

            // Network tags still belong on it, from the direct SetNetworkTags call.
            connectionActivity.HasTag("server.port");
        }

        [Fact]
        public async Task TestTcpConnectionActivityHasServerTags_GH1967()
        {
            /*
             * rabbitmq/rabbitmq-dotnet-client#1967
             *
             * OpenTcpConnection was the only activity factory returning a bare activity
             * with no tag block; the server tags were set by the call site instead.
             * Folding them into the factory keeps every factory in this file
             * self-consistent, so a new call site cannot forget them.
             */
            using ActivityRecorder tcpConnectionRecorder =
                new(RabbitMQActivitySource.ConnectionSourceName, "tcp connection attempt");
            tcpConnectionRecorder.VerifyParent = false;

            ConnectionFactory cf = CreateConnectionFactory();
            await using (IConnection conn = await cf.CreateConnectionAsync())
            {
                await conn.CloseAsync();
            }

            Activity tcpActivity = tcpConnectionRecorder.VerifyActivityRecordedOnce();
            // cf.Port is still the UseDefaultPort sentinel; Endpoint.Port resolves it.
            tcpActivity.HasTag("server.port", cf.Endpoint.Port);
            tcpActivity.HasTag("messaging.system", "rabbitmq");
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

        [Fact]
        public async Task TestCreateConnectionRegisterAnActivity()
        {
            using ActivityRecorder connectionRecorder =
                new(RabbitMQActivitySource.ConnectionSourceName, "connection attempt");
            using ActivityRecorder tcpConnectionRecorder =
                new(RabbitMQActivitySource.ConnectionSourceName, "tcp connection attempt");
            tcpConnectionRecorder.VerifyParent = false;
            ConnectionFactory cf = CreateConnectionFactory();
            await using IConnection conn = await cf.CreateConnectionAsync();
            var connectionActivity = connectionRecorder.VerifyActivityRecordedOnce();
            connectionActivity.HasTag("network.peer.address");
            connectionActivity.HasTag("network.local.address");
            connectionActivity.HasTag("server.address");
            connectionActivity.HasTag("client.address");
            connectionActivity.HasTag("network.peer.port");
            connectionActivity.HasTag("network.local.port");
            connectionActivity.HasTag("server.port");
            connectionActivity.HasTag("client.port");
            connectionActivity.HasTag("network.type");
            var tcpConnectionActivity = tcpConnectionRecorder.VerifyActivityRecordedOnce();
            tcpConnectionActivity.HasTag("server.port");
            tcpConnectionActivity.HasTag("server.address");
            Assert.Equal(connectionActivity, tcpConnectionActivity.Parent);
            await conn.CloseAsync();
        }

        [Fact]
        public async Task TestCreateConnectionWithFailureRecordException()
        {
            using ActivityRecorder recorder =
                new(RabbitMQActivitySource.ConnectionSourceName, "connection attempt");
            using ActivityRecorder tcpConnectionRecorder =
                new(RabbitMQActivitySource.ConnectionSourceName, "tcp connection attempt");
            tcpConnectionRecorder.VerifyParent = false;
            ConnectionFactory cf = CreateConnectionFactory();
            var unreachablePort = 1234;
            var ep = new AmqpTcpEndpoint("localhost", unreachablePort);
            var exception = await Assert.ThrowsAsync<BrokerUnreachableException>(() =>
            {
                return cf.CreateConnectionAsync(new List<AmqpTcpEndpoint> { ep });
            });
            Activity connectionActivity = recorder.VerifyActivityRecordedOnce();
            connectionActivity.HasRecordedException(exception);
            connectionActivity.IsInError();
            Activity tcpConnectionActivity = tcpConnectionRecorder.VerifyActivityRecordedOnce();
            tcpConnectionActivity.HasRecordedException("RabbitMQ.Client.Exceptions.ConnectFailureException");
            tcpConnectionActivity.IsInError();
        }

        [Fact]
        public async Task TestCreateConnectionWithFailoverRecordsErrorOnlyOnTheFailedAttempt()
        {
            using ActivityRecorder connectionRecorder =
                new(RabbitMQActivitySource.ConnectionSourceName, "connection attempt");
            using ActivityRecorder tcpConnectionRecorder =
                new(RabbitMQActivitySource.ConnectionSourceName, "tcp connection attempt");
            tcpConnectionRecorder.VerifyParent = false;

            ConnectionFactory cf = CreateConnectionFactory();

            const int unreachablePort = 1234;
            AmqpTcpEndpoint reachableEndpoint = cf.Endpoint;
            var unreachableEndpoint = new AmqpTcpEndpoint(reachableEndpoint.HostName, unreachablePort);

            /*
             * The DefaultEndpointResolver shuffles the endpoint list on every attempt, which
             * would make this test flaky - the reachable endpoint could be tried first, so only
             * one TCP attempt would ever happen. Use a resolver that preserves order so the
             * unreachable endpoint is always tried first.
             */
            var endpoints = new List<AmqpTcpEndpoint> { unreachableEndpoint, reachableEndpoint };
            cf.EndpointResolverFactory = _ => new OrderedEndpointResolver(endpoints);

            await using (IConnection conn = await cf.CreateConnectionAsync(endpoints))
            {
                await conn.CloseAsync();
            }

            /*
             * The first endpoint is unreachable and the second one is not, so the overall
             * operation succeeded. Only the failed attempt is flagged as an error - marking
             * the parent as failed too would report a successful CreateConnectionAsync as a
             * failure, and failing over across endpoints is expected behavior.
             */
            Activity connectionActivity = connectionRecorder.VerifyActivityRecordedOnce();
            Assert.Equal(ActivityStatusCode.Unset, connectionActivity.Status);
            Assert.Empty(connectionActivity.Events);

            tcpConnectionRecorder.VerifyActivityRecorded(2);
            List<Activity> tcpActivities = tcpConnectionRecorder.FinishedActivities.ToList();

            Activity failedAttempt = Assert.Single(tcpActivities,
                a => unreachablePort.Equals(a.GetTagItem("server.port")));
            failedAttempt.HasRecordedException("RabbitMQ.Client.Exceptions.ConnectFailureException");
            failedAttempt.IsInError();

            Activity successfulAttempt = Assert.Single(tcpActivities,
                a => reachableEndpoint.Port.Equals(a.GetTagItem("server.port")));
            Assert.Equal(ActivityStatusCode.Unset, successfulAttempt.Status);
            Assert.Empty(successfulAttempt.Events);
        }

        private sealed class OrderedEndpointResolver : IEndpointResolver
        {
            private readonly IEnumerable<AmqpTcpEndpoint> _endpoints;

            public OrderedEndpointResolver(IEnumerable<AmqpTcpEndpoint> endpoints) => _endpoints = endpoints;

            public IEnumerable<AmqpTcpEndpoint> All() => _endpoints;
        }
    }
}
