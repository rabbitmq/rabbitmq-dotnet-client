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
    public sealed class CreateChannelOptions
    {
        private ushort? _connectionConfigConsumerDispatchConcurrency;
        private TimeSpan _connectionConfigContinuationTimeout;
        private ConnectionTracingOptions? _connectionConfigTracingOptions;

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
        /// Defaults to a <see cref="ThrottlingRateLimiter"/> with a limit of 128 and a throttling percentage of 50% with a delay during throttling.
        /// </summary>
        /// <remarks>
        /// Setting the rate limiter to <c>null</c> disables the rate limiting entirely.
        /// <para>
        /// <b>That default applies only to options this library builds for you.</b> The
        /// <see cref="CreateChannelOptions(bool, bool, RateLimiter, ushort?)"/> constructor defaults this
        /// parameter to <c>null</c> and assigns it unconditionally, so options you construct yourself have
        /// no rate limiter - and therefore no limit on outstanding publisher confirmations - unless you pass
        /// one. This is the same divergence between field initializer and constructor default described on
        /// <see cref="ConsumerDispatchConcurrency"/>, but with a safety mechanism rather than a number on
        /// the other side of it.
        /// </para>
        /// <para>
        /// <b>Its lifetime belongs to you.</b> Disposing an <see cref="IChannel"/> does not dispose
        /// this limiter: it is shared by every channel created from these options, and a recovering
        /// channel reuses them, so a channel disposing it would break the survivors. This changed in
        /// 7.3.0: earlier versions disposed it, though only on the synchronous path or for a limiter
        /// that overrode <c>DisposeAsyncCore</c>, which <see cref="ThrottlingRateLimiter"/> did not.
        /// The default above needs no disposal; a limiter that owns a timer does.
        /// </para>
        /// </remarks>
        public readonly RateLimiter? OutstandingPublisherConfirmationsRateLimiter = new ThrottlingRateLimiter(128);

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
        /// The constructor default is deliberately not <c>null</c>. Changing it would compile everywhere and
        /// change behaviour silently rather than break a build: C# bakes an optional parameter's default into
        /// the caller's assembly, so applications that upgraded without rebuilding would keep the old
        /// behaviour while rebuilt ones switched, and code reading this member back would see
        /// <see cref="System.Nullable{T}.Value"/> throw at run time where it previously returned 1. The
        /// default is also part of this library's recorded public API surface, so changing it is an API
        /// change rather than an implementation detail. See rabbitmq/rabbitmq-dotnet-client#2027.
        /// </para>
        /// <para>
        /// <see cref="OutstandingPublisherConfirmationsRateLimiter"/> diverges the same way, and more
        /// sharply: its field initializer is a limiter with a limit of 128, while the constructor parameter
        /// defaults to <c>null</c> and is likewise assigned unconditionally - and <c>null</c> there disables
        /// rate limiting rather than selecting a different value.
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

        internal CreateChannelOptions(ConnectionConfig connectionConfig)
        {
            _connectionConfigConsumerDispatchConcurrency = connectionConfig.ConsumerDispatchConcurrency;
            _connectionConfigContinuationTimeout = connectionConfig.ContinuationTimeout;
            _connectionConfigTracingOptions = connectionConfig.TracingOptions;
        }

        private CreateChannelOptions WithConnectionConfig(ConnectionConfig connectionConfig)
        {
            _connectionConfigConsumerDispatchConcurrency = connectionConfig.ConsumerDispatchConcurrency;
            _connectionConfigContinuationTimeout = connectionConfig.ContinuationTimeout;
            _connectionConfigTracingOptions = connectionConfig.TracingOptions;
            return this;
        }

        internal static CreateChannelOptions CreateOrUpdate(CreateChannelOptions? createChannelOptions, ConnectionConfig config)
        {
            if (createChannelOptions is null)
            {
                return new CreateChannelOptions(config);
            }
            else
            {
                return createChannelOptions.WithConnectionConfig(config);
            }
        }
    }
}
