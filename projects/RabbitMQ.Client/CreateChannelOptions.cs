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
        /// <b>Its lifetime belongs to you.</b> Disposing an <see cref="IChannel"/> does not dispose
        /// this limiter, because it is not the channel's to dispose: it is shared by every channel
        /// created from this options instance, and a channel on a connection with automatic recovery
        /// reuses these options for every recovery, so the replacement channel publishes through the
        /// same limiter. A channel disposing it broke those survivors, and the next confirm-tracked
        /// publish threw <see cref="ObjectDisposedException"/>.
        /// </para>
        /// <para>
        /// This changed in 7.3.0. Earlier versions disposed it with the channel, so code that relied
        /// on that must now dispose it itself. It matters most for a limiter that owns a timer, such
        /// as <c>TokenBucketRateLimiter</c> or the window limiters, which otherwise keeps running for
        /// the life of the process. The default above needs no disposal: it wraps a
        /// <c>ConcurrencyLimiter</c>, which holds no timer and no unmanaged handle.
        /// </para>
        /// </remarks>
        public readonly RateLimiter? OutstandingPublisherConfirmationsRateLimiter = new ThrottlingRateLimiter(128);

        /// <summary>
        /// Set to a value greater than one to enable concurrent processing. For a concurrency greater than one <see cref="IAsyncBasicConsumer"/>
        /// will be offloaded to the worker thread pool so it is important to choose the value for the concurrency wisely to avoid thread pool overloading.
        /// <see cref="IAsyncBasicConsumer"/> can handle concurrency much more efficiently due to the non-blocking nature of the consumer.
        ///
        /// The field's own default is <c>null</c>, which uses the value from
        /// <see cref="IConnectionFactory.ConsumerDispatchConcurrency"/>. Note that the public constructor
        /// defaults its parameter to 1 rather than <c>null</c> and assigns it unconditionally, so options
        /// built through the constructor do NOT inherit the connection's value unless you pass
        /// <c>null</c> explicitly. Only <see cref="IConnection.CreateChannelAsync"/> with no options
        /// inherits it.
        ///
        /// For concurrency greater than one this removes the guarantee that consumers handle messages in the order they receive them.
        /// In addition to that consumers need to be thread/concurrency safe.
        ///
        /// A value of 0 is treated as 1. Zero would leave the channel's consumer dispatcher with no
        /// worker at all, so consumers would register successfully and never receive anything.
        /// </summary>
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

        internal CreateChannelOptions(ConnectionConfig connectionConfig)
        {
            _connectionConfigConsumerDispatchConcurrency = connectionConfig.ConsumerDispatchConcurrency;
            _connectionConfigContinuationTimeout = connectionConfig.ContinuationTimeout;
        }

        private CreateChannelOptions WithConnectionConfig(ConnectionConfig connectionConfig)
        {
            _connectionConfigConsumerDispatchConcurrency = connectionConfig.ConsumerDispatchConcurrency;
            _connectionConfigContinuationTimeout = connectionConfig.ContinuationTimeout;
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
