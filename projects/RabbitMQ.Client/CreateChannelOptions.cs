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
using System.Threading.RateLimiting;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Channel creation options.
    /// </summary>
    /// <remarks>
    /// One instance may be reused for any number of <see cref="IConnection.CreateChannelAsync"/>
    /// calls, on any number of connections, including concurrent ones: this library copies the
    /// instance rather than writing a connection's configuration into it, so every value a channel
    /// takes from its connection comes from the connection it was created on.
    /// Through 7.2.2 the instance was written to instead, so a channel could read another
    /// connection's continuation timeout, or another connection's
    /// <see cref="IConnectionFactory.ConsumerDispatchConcurrency"/> where
    /// <see cref="ConsumerDispatchConcurrency"/> was <c>null</c>. See
    /// rabbitmq/rabbitmq-dotnet-client#2034.
    /// <para>
    /// Reuse does not make the members per-channel.
    /// <see cref="OutstandingPublisherConfirmationsRateLimiter"/> is carried by reference, so a
    /// single limiter bounds every channel built from the instance on every connection; its own
    /// remarks cover what it bounds and who disposes it.
    /// </para>
    /// </remarks>
    public sealed class CreateChannelOptions
    {
        private readonly ushort? _connectionConfigConsumerDispatchConcurrency;
        private readonly TimeSpan _connectionConfigContinuationTimeout;
        private readonly ConnectionTracingOptions? _connectionConfigTracingOptions;

        /// <summary>
        /// Enable or disable publisher confirmations on this channel. Defaults to <c>false</c>
        ///
        /// Note that, if this is enabled, and <see cref="PublisherConfirmationTrackingEnabled"/> is <b>not</b>
        /// enabled, the broker may send a <c>basic.return</c> response if a message is published with <c>mandatory: true</c>
        /// and the broker can't route the message. This response will not, however, contain the publish sequence number
        /// for the message, so it is difficult to correlate the response to the correct message. Users of this library
        /// could add the <see cref="Constants.PublishSequenceNumberHeader"/> header with the value returned by
        /// <see cref="IChannel.GetNextPublishSequenceNumberAsync(System.Threading.CancellationToken)"/> to allow correlation
        /// of the response with the correct message.
        /// </summary>
        public readonly bool PublisherConfirmationsEnabled = false;

        /// <summary>
        /// Should this library track publisher confirmations for you? Defaults to <c>false</c>
        ///
        /// When enabled, the <see cref="Constants.PublishSequenceNumberHeader" /> header will be
        /// added to every published message, and will contain the message's publish sequence number.
        /// If the broker then sends a <c>basic.return</c> response for the message, this library can
        /// then correctly handle the message.
        /// </summary>
        public readonly bool PublisherConfirmationTrackingEnabled = false;

        /// <summary>
        /// If the publisher confirmation tracking is enabled, this represents the rate limiter used to
        /// throttle additional attempts to publish once the threshold is reached.
        ///
        /// Options you construct yourself have no rate limiter unless you pass one; see the remarks.
        /// </summary>
        /// <remarks>
        /// Setting the rate limiter to <c>null</c> disables the rate limiting entirely.
        /// <para>
        /// <b>The constructor gives you no limiter.</b> The
        /// <see cref="CreateChannelOptions(bool, bool, RateLimiter, ushort?)"/> constructor defaults this
        /// parameter to <c>null</c> and assigns it unconditionally. A
        /// <see cref="ThrottlingRateLimiter"/> with a limit of 128 and a throttling percentage of 50%
        /// reaches only the options this library builds for itself, and those leave
        /// <see cref="PublisherConfirmationsEnabled"/> false, so the publish path never consults it.
        /// <see cref="ConsumerDispatchConcurrency"/> is the member that does declare a default twice,
        /// and describes what that costs.
        /// </para>
        /// <para>
        /// <b>What a limiter bounds, if you pass one.</b> <see cref="ThrottlingRateLimiter"/> uses the limit
        /// as both its permit and its queue limit. A permit is held only while a publish is being sent and
        /// is released before the broker's acknowledgement is awaited, so the limit bounds concurrent
        /// <c>BasicPublishAsync</c> calls, not unacknowledged messages. The throttling percentage is an
        /// available-permit floor rather than the point at which throttling begins: a delay applies once
        /// available permits fall below <c>limit * percentage / 100</c>, so a lower percentage throttles
        /// later and 1% never throttles at all. Once every permit and queue slot is taken, the limiter
        /// waits a second and then refuses, which surfaces as an
        /// <see cref="System.InvalidOperationException"/> from the publish. Size it against how many
        /// publishes you have in flight at once. See rabbitmq/rabbitmq-dotnet-client#2054.
        /// </para>
        /// <para>
        /// <b>Its lifetime belongs to you.</b> Disposing an <see cref="IChannel"/> does not dispose
        /// this limiter: every channel built from one options object carries it by reference, as does
        /// every channel a recovery rebuilds, so a channel disposing it would break the survivors.
        /// This changed in 7.3.0: earlier versions disposed it, though only on the synchronous path or
        /// for a limiter that overrode <c>DisposeAsyncCore</c>, which
        /// <see cref="ThrottlingRateLimiter"/> did not. A <see cref="ThrottlingRateLimiter"/> needs no
        /// disposal; a limiter that owns a timer does.
        /// </para>
        /// </remarks>
        public readonly RateLimiter? OutstandingPublisherConfirmationsRateLimiter;

        /// <summary>
        /// Set to a value greater than one to enable concurrent processing. For a concurrency greater than one <see cref="IAsyncBasicConsumer"/>
        /// will be offloaded to the worker thread pool so it is important to choose the value for the concurrency wisely to avoid thread pool overloading.
        /// <see cref="IAsyncBasicConsumer"/> can handle concurrency much more efficiently due to the non-blocking nature of the consumer.
        /// <para>
        /// For concurrency greater than one this removes the guarantee that consumers handle messages in
        /// the order they receive them. In addition to that consumers need to be thread/concurrency safe.
        /// </para>
        /// </summary>
        /// <remarks>
        /// <c>null</c> means "use <see cref="IConnectionFactory.ConsumerDispatchConcurrency"/>", and which
        /// default you get depends on how the options were built:
        /// <list type="bullet">
        /// <item><description>
        /// <see cref="IConnection.CreateChannelAsync"/> with no options inherits the factory value.
        /// </description></item>
        /// <item><description>
        /// The <see cref="CreateChannelOptions(bool, bool, RateLimiter, ushort?)"/> constructor defaults
        /// this parameter to <see cref="Constants.DefaultConsumerDispatchConcurrency"/> (1) and assigns it
        /// unconditionally, so explicitly constructed options never inherit: whatever you pass is what the
        /// channel gets, and passing <c>consumerDispatchConcurrency: null</c> is what opts it into the
        /// factory value.
        /// </description></item>
        /// </list>
        /// <para>
        /// The constructor default is not <c>null</c>. Changing it would compile everywhere and
        /// change behaviour silently rather than break a build: C# bakes an optional parameter's default into
        /// the caller's assembly, so applications that upgraded without rebuilding would keep the old
        /// behaviour while rebuilt ones switched, and code reading this member back would see
        /// <see cref="System.Nullable{T}.Value"/> throw at run time where it previously returned 1. The
        /// default is also part of this library's recorded public API surface, so changing it is an API
        /// change rather than an implementation detail. See rabbitmq/rabbitmq-dotnet-client#2027.
        /// </para>
        /// <para>
        /// <see cref="OutstandingPublisherConfirmationsRateLimiter"/> has no such divergence: this type
        /// declares one default for it, the parameter's <c>null</c>, which is what a caller gets. That
        /// <c>null</c> is sharper than this member's, because it disables rate limiting rather than
        /// selecting a different value.
        /// </para>
        /// <para>
        /// A value of 0 is treated as 1. Zero would leave the channel's consumer dispatcher with no
        /// worker at all, so consumers would register successfully and never receive anything.
        /// </para>
        /// </remarks>
        public readonly ushort? ConsumerDispatchConcurrency = null;

        public CreateChannelOptions(bool publisherConfirmationsEnabled,
            bool publisherConfirmationTrackingEnabled,
            RateLimiter? outstandingPublisherConfirmationsRateLimiter = null,
            ushort? consumerDispatchConcurrency = Constants.DefaultConsumerDispatchConcurrency)
        {
            PublisherConfirmationsEnabled = publisherConfirmationsEnabled;
            PublisherConfirmationTrackingEnabled = publisherConfirmationTrackingEnabled;
            OutstandingPublisherConfirmationsRateLimiter = outstandingPublisherConfirmationsRateLimiter;
            ConsumerDispatchConcurrency = consumerDispatchConcurrency;
        }

        // The dispatch concurrency requested for a channel built from these options: the caller's own
        // value if set, otherwise the connection's, otherwise the library default. This is what was
        // ASKED FOR and may be zero; the consumer dispatcher applies the floor, because that is the
        // type whose invariant it is. See docs/internal/consumer-dispatch-concurrency.md.
        //
        // Deliberately `//` and not `///`: csc does not filter doc comments by accessibility, so
        // `///` on an internal member ships in RabbitMQ.Client.xml inside the NuGet package.
        internal ushort InternalConsumerDispatchConcurrency
            => ConsumerDispatchConcurrency
               ?? _connectionConfigConsumerDispatchConcurrency
               ?? Constants.DefaultConsumerDispatchConcurrency;

        internal TimeSpan ContinuationTimeout => _connectionConfigContinuationTimeout;

        internal ConnectionTracingOptions? TracingOptions => _connectionConfigTracingOptions;

        // A null source is channel 0 and CreateChannelAsync with no options. #2034 and the remarks
        // on this type carry why this copies rather than writing into the caller's instance.
        private CreateChannelOptions(CreateChannelOptions? source, ConnectionConfig connectionConfig)
        {
            if (source is not null)
            {
                PublisherConfirmationsEnabled = source.PublisherConfirmationsEnabled;
                PublisherConfirmationTrackingEnabled = source.PublisherConfirmationTrackingEnabled;
                OutstandingPublisherConfirmationsRateLimiter = source.OutstandingPublisherConfirmationsRateLimiter;
                ConsumerDispatchConcurrency = source.ConsumerDispatchConcurrency;
            }
            else
            {
                // Here rather than as a field initializer, which would run on the copy path too and
                // then be overwritten, one throwaway limiter per channel. #2032.
                OutstandingPublisherConfirmationsRateLimiter = new ThrottlingRateLimiter(128);
            }

            _connectionConfigConsumerDispatchConcurrency = connectionConfig.ConsumerDispatchConcurrency;
            _connectionConfigContinuationTimeout = connectionConfig.ContinuationTimeout;
            _connectionConfigTracingOptions = connectionConfig.TracingOptions;
        }

        internal static CreateChannelOptions ForConnection(CreateChannelOptions? createChannelOptions,
            ConnectionConfig config)
            => new CreateChannelOptions(createChannelOptions, config);
    }
}
