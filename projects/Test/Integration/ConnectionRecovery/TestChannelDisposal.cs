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

            Assert.False(await DispatcherWasReleasedAsync(dispatcher, TimeSpan.FromMilliseconds(200)));

            await channel.CloseAsync();
            await channel.DisposeAsync();

            Assert.True(await DispatcherWasReleasedAsync(dispatcher, WaitSpan),
                "disposing the AutorecoveringChannel must dispose its inner channel, which completes " +
                "the consumer dispatcher's work channel and so releases its worker. See #1988.");
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
                    Assert.True(await DispatcherWasReleasedAsync(dispatcherBeforeRecovery, WaitSpan),
                        "recovery replaces the inner channel and must dispose the replaced one, " +
                        "otherwise each recovery cycle abandons a channel. See #1988.");

                    /*
                     * The live channel's dispatcher must NOT have been disposed. A disposed
                     * dispatcher quiesces, so every later basic.deliver is silently dropped and its
                     * pooled body leaked, on a channel that still reports IsOpen. Without this the
                     * suite stays green when the wrong channel is disposed. See #1988.
                     */
                    Assert.False(
                        await DispatcherWasReleasedAsync(innerAfterRecovery.ConsumerDispatcher,
                            TimeSpan.FromMilliseconds(200)),
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
                    await deliveredTcs.Task.WaitAsync(WaitSpan);
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
                    /*
                     * In a finally so a failed assertion does not leave the queue on the broker, and
                     * each step guarded: when an assertion above fails the channel may already be
                     * closed, and an AlreadyClosedException thrown from here would replace the
                     * assertion failure and hide the diagnosis it carries.
                     */
                    try
                    {
                        await channel.QueueDeleteAsync(queueName);
                    }
                    catch (Exception e)
                    {
                        _output.WriteLine($"queue cleanup failed: {e.Message}");
                    }

                    try
                    {
                        await channel.CloseAsync();
                    }
                    catch (Exception e)
                    {
                        _output.WriteLine($"channel close failed: {e.Message}");
                    }
                }
            }
        }

        /*
         * Probe the dispatcher's observable state rather than its _disposed flag. That flag is set
         * from a finally wrapped around a catch that swallows everything, so it proves only that
         * Dispose was entered: empty the body of Dispose and a flag-based assertion still passes.
         * What disposal actually has to achieve is completing the work channel, which is what
         * releases the worker loop, and the reader's Completion task is the read-only way to see it.
         *
         * It must be read-only. An earlier version called TryComplete on the writer, which completes
         * the channel as a side effect, so the assertion itself released the live dispatcher and
         * every later delivery failed with ChannelClosedException.
         *
         * Completion finishes once the channel is completed and drained, so allow a bounded wait
         * rather than reading it instantly.
         */
        private static async Task<bool> DispatcherWasReleasedAsync(IConsumerDispatcher dispatcher,
            TimeSpan timeout)
        {
            object readerObj = GetPrivateField(dispatcher, "_reader");
            Assert.NotNull(readerObj);

            var completion = (Task)readerObj.GetType()
                .GetProperty("Completion", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(readerObj);
            Assert.NotNull(completion);

            try
            {
                await completion.WaitAsync(timeout);
                return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
            catch
            {
                // A faulted completion still means the channel was completed.
                return true;
            }
        }

        private static object GetPrivateField(object target, string name)
        {
            Type type = target.GetType();

            while (type is not null)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field is not null)
                {
                    return field.GetValue(target);
                }

                type = type.BaseType;
            }

            return null;
        }
    }
}
