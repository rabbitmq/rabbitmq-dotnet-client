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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.ConsumerDispatching;
using RabbitMQ.Client.Events;
using Xunit;

namespace Test.Unit
{
    /// <summary>
    /// rabbitmq/rabbitmq-dotnet-client#2035. A dispatch concurrency of zero built a consumer
    /// dispatcher with no reader loops, so nothing drained the work channel. See
    /// <c>docs/internal/consumer-dispatch-concurrency.md</c> for the mechanism.
    ///
    /// These assert on <see cref="IConsumerDispatcher.Concurrency"/>, which is the state the broken
    /// loop count is derived from, rather than on the options resolution that feeds it. The dispatcher
    /// is where the invariant lives and can be constructed directly, so no broker is needed.
    /// </summary>
    public class TestConsumerDispatchConcurrency
    {
        [Theory]
        [InlineData((ushort)0)]  // the bug: zero built no reader loops at all
        [InlineData((ushort)1)]
        [InlineData((ushort)2)]
        [InlineData((ushort)9)]
        public async Task DispatcherActuallyDispatchesWork_GH2035(ushort requested)
        {
            /*
             * Asserts that work is dispatched, not that a field holds a particular number.
             *
             * Concurrency is a direct proxy for the field the guard writes, so a test reading it
             * cannot see whether any reader loop was actually started: changing the loop below to
             * count the raw constructor parameter instead of the guarded field reintroduces #2035 in
             * full while every such assertion still passes. Dispatching a ConsumeOk is the cheapest
             * observation that requires a live loop - that work type touches only the consumer, never
             * the channel, so a null channel is fine and no broker is needed.
             */
            var consumer = new RecordingConsumer();
            var dispatcher = new AsyncConsumerDispatcher(null, requested);
            try
            {
                await dispatcher.HandleBasicConsumeOkAsync(consumer, "tag", CancellationToken.None);

                Task dispatched = await Task.WhenAny(consumer.ConsumeOkReceived,
                    Task.Delay(TimeSpan.FromSeconds(10)));

                Assert.True(ReferenceEquals(dispatched, consumer.ConsumeOkReceived),
                    $"concurrency {requested} produced a dispatcher that never dispatched: no reader loop drained the work channel");
            }
            finally
            {
                /*
                 * Dispose alone cannot stop this: it quiesces and disposes the shutdown source but
                 * never completes the work-channel writer, and the reader loops await
                 * WaitToReadAsync with no token, so they would stay parked for the life of the test
                 * host. ShutdownAsync is what completes the writer, and it is safe with a null
                 * channel because no consumer is registered on one.
                 */
                await dispatcher.ShutdownAsync(new ShutdownEventArgs(ShutdownInitiator.Library, 0, "test over"));
                dispatcher.Dispose();
            }
        }

        [Theory]
        [InlineData((ushort)0, (ushort)1)]  // the bug: zero would build no reader loops
        [InlineData((ushort)1, (ushort)1)]
        [InlineData((ushort)2, (ushort)2)]
        [InlineData((ushort)9, (ushort)9)]
        public void DispatcherReportsAtLeastOneReaderLoop_GH2035(ushort requested, ushort expected)
        {
            /*
             * The companion to the behavioural case above: the reported concurrency must agree with
             * the floor. Expectations are written out per row rather than computed, so the assertion
             * cannot restate the implementation it is checking.
             */
            using var dispatcher = new AsyncConsumerDispatcher(null, requested);

            Assert.Equal(expected, dispatcher.Concurrency);
        }

        [Theory]
        [InlineData((ushort)0, (ushort)0)]
        [InlineData((ushort)4, (ushort)4)]
        public void OptionsResolveTheCallersValueVerbatim_GH2035(ushort requested, ushort expected)
        {
            /*
             * The options layer deliberately does NOT correct zero: it reports what was asked for, so
             * the public field and this resolution agree, and the dispatcher applies the floor. This
             * pins that split so a future change does not quietly move the guard back up a layer and
             * leave the dispatcher unprotected against its other callers.
             */
            var options = new CreateChannelOptions(publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false, consumerDispatchConcurrency: requested);

            Assert.Equal(expected, options.InternalConsumerDispatchConcurrency);
        }

        [Theory]
        [InlineData((ushort)0, (ushort)1)]
        [InlineData((ushort)7, (ushort)7)]
        public void ConcurrencyInheritedFromTheConnectionIsUsed_GH2035(ushort configured, ushort expected)
        {
            /*
             * Covers the second arm of the resolution - the connection's value, used when the caller
             * named none. Without this, deleting `?? _connectionConfigConsumerDispatchConcurrency`
             * leaves the whole suite green while every CreateChannelAsync() with no options silently
             * ignores ConnectionFactory.ConsumerDispatchConcurrency and runs a single dispatch loop.
             * The integration assertions do not cover it either: they all create channels from
             * explicit options, which pins the first arm.
             */
            CreateChannelOptions options =
                CreateChannelOptions.CreateOrUpdate(null, ConnectionConfigWithConcurrency(configured));

            Assert.Equal(configured, options.InternalConsumerDispatchConcurrency);

            using var dispatcher = new AsyncConsumerDispatcher(null, options.InternalConsumerDispatchConcurrency);
            Assert.Equal(expected, dispatcher.Concurrency);
        }

        /*
         * Built directly rather than through ConnectionFactory, whose CreateConfig is private. Only
         * consumerDispatchConcurrency matters here; the rest are the least surprising values that
         * satisfy the constructor.
         */
        private static ConnectionConfig ConnectionConfigWithConcurrency(ushort consumerDispatchConcurrency)
            => new ConnectionConfig(
                virtualHost: "/",
                userName: "guest",
                password: "guest",
                credentialsProvider: null,
                authMechanisms: Array.Empty<IAuthMechanismFactory>(),
                clientProperties: new System.Collections.Generic.Dictionary<string, object>(),
                clientProvidedName: null,
                maxChannelCount: ConnectionFactory.DefaultChannelMax,
                maxFrameSize: 0,
                maxInboundMessageBodySize: ConnectionFactory.DefaultMaxInboundMessageBodySize,
                topologyRecoveryEnabled: false,
                topologyRecoveryFilter: new TopologyRecoveryFilter(),
                topologyRecoveryExceptionHandler: new TopologyRecoveryExceptionHandler(),
                networkRecoveryInterval: TimeSpan.FromSeconds(5),
                heartbeatInterval: TimeSpan.FromSeconds(60),
                continuationTimeout: TimeSpan.FromSeconds(20),
                handshakeContinuationTimeout: TimeSpan.FromSeconds(10),
                requestedConnectionTimeout: TimeSpan.FromSeconds(30),
                consumerDispatchConcurrency: consumerDispatchConcurrency,
                frameHandlerFactoryAsync: (_, __) => throw new NotSupportedException("not connected in this test"));

        [Fact]
        public async Task ADeliveryOnACompletedWorkChannelIsDroppedNotThrown_GH1988()
        {
            /*
             * Dispose completes the work channel, and the `_disposed`/IsQuiescing check each
             * Handle*Async makes is not atomic with the write that follows it, so a caller can pass
             * the check and then find the channel completed. WriteAsync raises
             * ChannelClosedException there, and for a delivery that unwinds through
             * Channel.HandleCommandAsync - which has no catch - into the connection's frame-receive
             * loop, tearing down the whole connection instead of the one channel and abandoning the
             * delivery's pooled body.
             *
             * The race itself cannot be scheduled, but the state it produces can: complete the
             * writer directly, leaving _disposed false so the guard is passed exactly as it would be
             * mid-race. Without the catch this call throws.
             */
            var dispatcher = new AsyncConsumerDispatcher(null, 1);
            try
            {
                object writer = typeof(ConsumerDispatcherChannelBase)
                    .GetField("_writer", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(dispatcher);
                Assert.NotNull(writer);
                Assert.True((bool)writer.GetType().GetMethod("TryComplete").Invoke(writer, new object[] { null }),
                    "could not complete the work channel, so this test never reaches the state it is about");

                Assert.False((bool)typeof(ConsumerDispatcherChannelBase)
                    .GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(dispatcher),
                    "the dispatcher reports disposed, so the guard under test is short-circuited");

                await dispatcher.HandleBasicDeliverAsync("tag", 1, false, "ex", "rk",
                    new BasicProperties(), default, CancellationToken.None);
            }
            finally
            {
                await dispatcher.ShutdownAsync(new ShutdownEventArgs(ShutdownInitiator.Library, 0, "test over"));
                dispatcher.Dispose();
            }
        }

        private sealed class RecordingConsumer : IAsyncBasicConsumer
        {
            private readonly TaskCompletionSource<bool> _consumeOk =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task ConsumeOkReceived => _consumeOk.Task;

            public IChannel Channel => null;

            public Task HandleBasicCancelAsync(string consumerTag, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task HandleBasicCancelOkAsync(string consumerTag, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task HandleBasicConsumeOkAsync(string consumerTag, CancellationToken cancellationToken = default)
            {
                _consumeOk.TrySetResult(true);
                return Task.CompletedTask;
            }

            public Task HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered,
                string exchange, string routingKey, IReadOnlyBasicProperties properties,
                ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason)
                => Task.CompletedTask;
        }
    }
}
