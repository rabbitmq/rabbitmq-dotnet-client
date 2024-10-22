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

using System;
using System.Threading.RateLimiting;

namespace RabbitMQ.Client.Impl
{
    internal sealed class ChannelOptions
    {
        private readonly bool _publisherConfirmationEnabled;
        private readonly bool _publisherConfirmationTrackingEnabled;
        private readonly ushort _consumerDispatchConcurrency;
        private readonly RateLimiter? _outstandingPublisherConfirmationsRateLimiter;
        private readonly TimeSpan _continuationTimeout;

        public ChannelOptions(bool publisherConfirmationEnabled,
            bool publisherConfirmationTrackingEnabled,
            ushort consumerDispatchConcurrency,
            RateLimiter? outstandingPublisherConfirmationsRateLimiter,
            TimeSpan continuationTimeout)
        {
            _publisherConfirmationEnabled = publisherConfirmationEnabled;
            _publisherConfirmationTrackingEnabled = publisherConfirmationTrackingEnabled;
            _consumerDispatchConcurrency = consumerDispatchConcurrency;
            _outstandingPublisherConfirmationsRateLimiter = outstandingPublisherConfirmationsRateLimiter;
            _continuationTimeout = continuationTimeout;
        }

        public bool PublisherConfirmationsEnabled => _publisherConfirmationEnabled;

        public bool PublisherConfirmationTrackingEnabled => _publisherConfirmationTrackingEnabled;

        public ushort ConsumerDispatchConcurrency => _consumerDispatchConcurrency;

        public RateLimiter? OutstandingPublisherConfirmationsRateLimiter => _outstandingPublisherConfirmationsRateLimiter;

        public TimeSpan ContinuationTimeout => _continuationTimeout;

        public static ChannelOptions From(CreateChannelOptions createChannelOptions,
            ConnectionConfig connectionConfig)
        {
            ushort cdc = createChannelOptions.ConsumerDispatchConcurrency.GetValueOrDefault(
                connectionConfig.ConsumerDispatchConcurrency);

            return new ChannelOptions(createChannelOptions.PublisherConfirmationsEnabled,
                createChannelOptions.PublisherConfirmationTrackingEnabled,
                cdc,
                createChannelOptions.OutstandingPublisherConfirmationsRateLimiter,
                connectionConfig.ContinuationTimeout);
        }

        public static ChannelOptions From(ConnectionConfig connectionConfig)
        {
            return new ChannelOptions(publisherConfirmationEnabled: false,
                publisherConfirmationTrackingEnabled: false,
                consumerDispatchConcurrency: Constants.DefaultConsumerDispatchConcurrency,
                outstandingPublisherConfirmationsRateLimiter: null,
                continuationTimeout: connectionConfig.ContinuationTimeout);
        }
    }
}
