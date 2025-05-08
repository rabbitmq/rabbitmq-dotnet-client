// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;

namespace RabbitMQ.Client.Exceptions
{
    /// <summary>
    /// Class for exceptions related to publisher confirmations
    /// or the <c>mandatory</c> flag.
    /// </summary>
    public class PublishException : RabbitMQClientException
    {
        private bool _isReturn = false;
        private ulong _publishSequenceNumber = ulong.MinValue;

        public PublishException(ulong publishSequenceNumber, bool isReturn) : base()
        {
            if (publishSequenceNumber == ulong.MinValue)
            {
                throw new ArgumentException($"{nameof(publishSequenceNumber)} must not be 0");
            }

            _isReturn = isReturn;
            _publishSequenceNumber = publishSequenceNumber;
        }

        /// <summary>
        /// <c>true</c> if this exception is due to a <c>basic.return</c>
        /// </summary>
        public bool IsReturn => _isReturn;

        /// <summary>
        /// Retrieve the publish sequence number.
        /// </summary>
        public ulong PublishSequenceNumber => _publishSequenceNumber;
    }

    /// <summary>
    /// Class for exceptions related to publisher confirmations
    /// or the <c>mandatory</c> flag, when <c>basic.return</c> is
    /// sent from the broker.
    /// </summary>
    public class PublishReturnException : PublishException
    {
        private readonly string _exchange;
        private readonly string _routingKey;

        public PublishReturnException(ulong publishSequenceNumber, string exchange, string routingKey)
            : base(publishSequenceNumber, true)
        {
            _exchange = exchange;
            _routingKey = routingKey;
        }

        /// <summary>
        /// Get the Exchange associated with this <c>basic.return</c>
        /// </summary>
        public string Exchange => _exchange;

        /// <summary>
        /// Get the RoutingKey associated with this <c>basic.return</c>
        /// </summary>
        public string RoutingKey => _routingKey;
    }

    internal static class PublishExceptionFactory
    {
        internal static PublishException Create(bool isReturn,
            ulong deliveryTag, string? exchange = null, string? routingKey = null)
        {
            if (isReturn)
            {
                if (exchange is not null && routingKey is not null)
                {
                    return new PublishReturnException(deliveryTag, exchange, routingKey);
                }
                else
                {
                    return new PublishException(deliveryTag, isReturn);
                }
            }
            else
            {
                return new PublishException(deliveryTag, isReturn);
            }
        }
    }
}
