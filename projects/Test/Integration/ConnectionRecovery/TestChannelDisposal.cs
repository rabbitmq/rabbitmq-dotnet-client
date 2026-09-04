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
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.ConsumerDispatching;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration.ConnectionRecovery
{
    /// <summary>
    /// rabbitmq/rabbitmq-dotnet-client#1988
    ///
    /// AutorecoveringChannel wraps a RecoveryAwareChannel and is what
    /// CreateChannelAsync hands back when automatic recovery is enabled (the
    /// default). The inner channel's consumer dispatcher owns a worker loop and a
    /// CancellationTokenSource, and it is created fresh per channel, so the wrapper
    /// must dispose it: once when the wrapper is disposed, and once per recovery for
    /// the dispatcher of the channel recovery replaces. The publisher-confirmation
    /// rate limiter is deliberately not disposed with it: it lives on the reused
    /// CreateChannelOptions and is shared across the original channel, every recovery,
    /// and any sibling channel, so disposing it per channel would break the survivors.
    /// The dispatcher's private _disposed field is read by reflection because it is the
    /// direct signal that the dispatcher was released.
    /// </summary>
    public class TestChannelDisposal : TestConnectionRecoveryBase
    {
        public TestChannelDisposal(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestDisposingChannelDisposesInnerConsumerDispatcher_GH1988()
        {
            IChannel channel = await _conn.CreateChannelAsync(_createChannelOptions);
            IConsumerDispatcher dispatcher = ((AutorecoveringChannel)channel).InnerChannel.ConsumerDispatcher;

            Assert.False(GetDispatcherDisposed(dispatcher));

            await channel.CloseAsync();
            await channel.DisposeAsync();

            Assert.True(GetDispatcherDisposed(dispatcher),
                "disposing the AutorecoveringChannel must dispose its inner channel, which releases " +
                "the consumer dispatcher's CancellationTokenSource. See #1988.");
        }

        [Fact]
        public async Task TestRecoveryDisposesReplacedDispatcherAndKeepsPublishingWorking_GH1988()
        {
            /*
             * Give the channel an explicit publisher-confirmation rate limiter so the
             * post-recovery publish below actually acquires from it. The fixture's
             * _createChannelOptions leaves the limiter null - the constructor parameter
             * defaults to null, overriding the field initializer - which would make the
             * regression guard vacuous. The limiter lives on the reused
             * CreateChannelOptions and is shared across the original channel and every
             * recovery. See #1988.
             */
            using var rateLimiter = new ThrottlingRateLimiter(128);
            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true,
                outstandingPublisherConfirmationsRateLimiter: rateLimiter);
            IChannel channel = await _conn.CreateChannelAsync(channelOptions);
            await using (channel.ConfigureAwait(false))
            {
                var autorecoveringChannel = (AutorecoveringChannel)channel;

                // A non-default prefetch makes recovery issue a basic.qos before it installs the
                // new channel, so the setup-RPC path that the lifetime finally guards is executed.
                await channel.BasicQosAsync(0, 10, false);

                RecoveryAwareChannel innerBeforeRecovery = autorecoveringChannel.InnerChannel;
                IConsumerDispatcher dispatcherBeforeRecovery = innerBeforeRecovery.ConsumerDispatcher;

                string queueName = GenerateQueueName();
                // Durable: the broker rejects transient non-exclusive queues
                // (transient_nonexcl_queues is deprecated), and recovery must recreate it.
                await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false,
                    autoDelete: false, arguments: null);
                try
                {
                    var deliveredTcs =
                        new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.ReceivedAsync += (_, _) =>
                    {
                        deliveredTcs.TrySetResult(true);
                        return Task.CompletedTask;
                    };
                    await channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);

                    await CloseAndWaitForRecoveryAsync();

                    RecoveryAwareChannel innerAfterRecovery = autorecoveringChannel.InnerChannel;

                    Assert.NotSame(innerBeforeRecovery, innerAfterRecovery);
                    Assert.True(GetDispatcherDisposed(dispatcherBeforeRecovery),
                        "recovery replaces the inner channel and must dispose the replaced one, " +
                        "otherwise each recovery cycle abandons a channel. See #1988.");

                    /*
                     * The live channel's dispatcher must NOT have been disposed. A disposed
                     * dispatcher quiesces, so every later basic.deliver is silently dropped and its
                     * pooled body leaked, on a channel that still reports IsOpen. Without this the
                     * suite stays green when the wrong channel is disposed. See #1988.
                     */
                    Assert.False(GetDispatcherDisposed(innerAfterRecovery.ConsumerDispatcher),
                        "recovery must not dispose the channel it just installed. See #1988.");

                    /*
                     * The publisher-confirmation rate limiter belongs to the reused
                     * CreateChannelOptions, so it must survive recovery. Publishing with
                     * confirmation tracking acquires from it; before the ownership fix, disposing a
                     * channel disposed the limiter its replacement still publishes through.
                     */
                    await channel.BasicPublishAsync(string.Empty, queueName,
                        _encoding.GetBytes("after recovery"));

                    // The recovered consumer must actually receive it. This is what proves the
                    // surviving dispatcher still dispatches, rather than only that a flag is unset.
                    await deliveredTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    Assert.True(await deliveredTcs.Task);

                    // The shared limiter must still be usable directly: a disposed RateLimiter
                    // throws ObjectDisposedException from AcquireAsync. See #1988.
                    using (RateLimitLease lease = await rateLimiter.AcquireAsync(1))
                    {
                        Assert.True(lease.IsAcquired);
                    }
                }
                finally
                {
                    // In a finally so a failed assertion does not leave the queue on the broker.
                    await channel.QueueDeleteAsync(queueName);
                    await channel.CloseAsync();
                }
            }
        }

        private static bool GetDispatcherDisposed(IConsumerDispatcher dispatcher)
        {
            Type type = dispatcher.GetType();
            FieldInfo field = null;

            while (type is not null && field is null)
            {
                field = type.GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic);
                type = type.BaseType;
            }

            Assert.NotNull(field);
            return Assert.IsType<bool>(field.GetValue(dispatcher));
        }
    }
}
