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

using System.Collections.Generic;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Convenience class providing compile-time names for standard exchange types.
    /// </summary>
    /// <remarks>
    /// Use the static members of this class as values for the
    /// "exchangeType" arguments for IChannel methods such as
    /// ExchangeDeclare. The broker may be extended with additional
    /// exchange types that do not appear in this class.
    /// </remarks>
    public static class ExchangeType
    {
        /// <summary>
        /// Exchange type used for AMQP direct exchanges.
        /// </summary>
        public const string Direct = "direct";

        /// <summary>
        /// Exchange type used for AMQP fanout exchanges.
        /// </summary>
        public const string Fanout = "fanout";

        /// <summary>
        /// Exchange type used for AMQP headers exchanges.
        /// </summary>
        public const string Headers = "headers";

        /// <summary>
        /// Exchange type used for AMQP topic exchanges.
        /// </summary>
        public const string Topic = "topic";

        /// <summary>
        /// Exchange type used for consistent hash exchanges.
        /// </summary>
        /// <remarks>
        /// Requires the <c>rabbitmq_consistent_hash_exchange</c> plugin to be enabled.
        /// </remarks>
        public const string ConsistentHash = "x-consistent-hash";

        /// <summary>
        /// Exchange type used for local random exchanges.
        /// </summary>
        /// <remarks>
        /// Available in RabbitMQ 4.2.0 and later.
        /// </remarks>
        public const string LocalRandom = "x-local-random";

        /// <summary>
        /// Exchange type used for modulus hash exchanges.
        /// </summary>
        /// <remarks>
        /// Available in RabbitMQ 4.3.0 and later.
        /// </remarks>
        public const string ModulusHash = "x-modulus-hash";

        /// <summary>
        /// Exchange type used for random exchanges.
        /// </summary>
        /// <remarks>
        /// Requires the <c>rabbitmq_random_exchange</c> plugin to be enabled.
        /// </remarks>
        public const string Random = "x-random";

        private static readonly string[] s_all = { Fanout, Direct, Topic, Headers, ConsistentHash, LocalRandom, ModulusHash, Random };

        /// <summary>
        /// Retrieve a collection containing all standard exchange types.
        /// </summary>
        public static ICollection<string> All()
        {
            return s_all;
        }
    }
}
