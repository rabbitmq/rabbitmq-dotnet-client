// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System.Threading.RateLimiting;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Channel creation options.
    /// </summary>
    public sealed class CreateChannelOptions
    {
        /// <summary>
        /// Enable or disable publisher confirmations on this channel. Defaults to <c>false</c>
        /// </summary>
        public bool PublisherConfirmationsEnabled { get; set; } = false;

        /// <summary>
        /// Should this library track publisher confirmations for you? Defaults to <c>false</c>
        /// </summary>
        public bool PublisherConfirmationTrackingEnabled { get; set; } = false;

        /// <summary>
        /// If the publisher confirmation tracking is enabled, this represents the rate limiter used to
        /// throttle additional attempts to publish once the threshold is reached.
        ///
        /// Defaults to a <see cref="ThrottlingRateLimiter"/> with a limit of 128 and a throttling percentage of 50% with a delay during throttling.
        /// </summary>
        /// <remarks>Setting the rate limiter to <c>null</c> disables the rate limiting entirely.</remarks>
        public RateLimiter? OutstandingPublisherConfirmationsRateLimiter { get; set; } = new ThrottlingRateLimiter(128);

        /// <summary>
        /// Set to a value greater than one to enable concurrent processing. For a concurrency greater than one <see cref="IAsyncBasicConsumer"/>
        /// will be offloaded to the worker thread pool so it is important to choose the value for the concurrency wisely to avoid thread pool overloading.
        /// <see cref="IAsyncBasicConsumer"/> can handle concurrency much more efficiently due to the non-blocking nature of the consumer.
        ///
        /// Defaults to <c>null</c>, which will use the value from <see cref="IConnectionFactory.ConsumerDispatchConcurrency"/>
        ///
        /// For concurrency greater than one this removes the guarantee that consumers handle messages in the order they receive them.
        /// In addition to that consumers need to be thread/concurrency safe.
        /// </summary>
        public ushort? ConsumerDispatchConcurrency { get; set; } = null;

        /// <summary>
        /// The default channel options.
        /// </summary>
        public static CreateChannelOptions Default { get; } = new CreateChannelOptions();
    }
}
