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

using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    internal sealed class RecoveryAwareChannel : Channel
    {
        public RecoveryAwareChannel(ISession session, ChannelOptions channelOptions)
            : base(session, channelOptions)
        {
            ActiveDeliveryTagOffset = 0;
            MaxSeenDeliveryTag = 0;
        }

        public ulong ActiveDeliveryTagOffset { get; private set; }
        public ulong MaxSeenDeliveryTag { get; private set; }

        internal void TakeOver(RecoveryAwareChannel other)
        {
            base.TakeOver(other);
            ActiveDeliveryTagOffset = other.ActiveDeliveryTagOffset + other.MaxSeenDeliveryTag;
            MaxSeenDeliveryTag = 0;
        }

        protected override ulong AdjustDeliveryTag(ulong deliveryTag)
        {
            if (deliveryTag > MaxSeenDeliveryTag)
            {
                MaxSeenDeliveryTag = deliveryTag;
            }
            return deliveryTag + ActiveDeliveryTagOffset;
        }

        public override ValueTask BasicAckAsync(ulong deliveryTag, bool multiple,
            CancellationToken cancellationToken)
        {
            ulong realTag = deliveryTag - ActiveDeliveryTagOffset;
            if (realTag > 0 && realTag <= deliveryTag)
            {
                return base.BasicAckAsync(realTag, multiple, cancellationToken);
            }
            else
            {
                return default;
            }
        }

        public override ValueTask BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue,
            CancellationToken cancellationToken)
        {
            ulong realTag = deliveryTag - ActiveDeliveryTagOffset;
            if (realTag > 0 && realTag <= deliveryTag)
            {
                return base.BasicNackAsync(realTag, multiple, requeue, cancellationToken);
            }
            else
            {
                return default;
            }
        }

        public override ValueTask BasicRejectAsync(ulong deliveryTag, bool requeue,
            CancellationToken cancellationToken)
        {
            ulong realTag = deliveryTag - ActiveDeliveryTagOffset;
            if (realTag > 0 && realTag <= deliveryTag)
            {
                return base.BasicRejectAsync(realTag, requeue, cancellationToken);
            }
            else
            {
                return default;
            }
        }

        internal static async ValueTask<RecoveryAwareChannel> CreateAndOpenAsync(ISession session, ChannelOptions channelOptions,
            CancellationToken cancellationToken)
        {
            var result = new RecoveryAwareChannel(session, channelOptions);
            return (RecoveryAwareChannel)await result.OpenAsync(channelOptions, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
