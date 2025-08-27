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

namespace RabbitMQ.Client
{
    internal static class InternalConstants
    {
        internal static readonly TimeSpan DefaultConnectionAbortTimeout = TimeSpan.FromSeconds(5);
        internal static readonly TimeSpan DefaultConnectionCloseTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Largest message size, in bytes, allowed in RabbitMQ.        
        /// Note: <code>rabbit.max_message_size</code> setting (https://www.rabbitmq.com/configure.html)
        /// configures the largest message size which should not be higher than this maximum of 512MiB.
        /// </summary>
        internal const uint MaximumRabbitMqMaxInboundMessageBodySize = 1_048_576 * 512;

        /// <summary>
        /// Largest client provide name, in characters, allowed in RabbitMQ.
        /// This is not configurable, but was discovered while working on this issue:
        /// https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/980
        /// </summary>
        internal const int DefaultRabbitMqMaxClientProvideNameLength = 3000;

        internal const string BugFound = "BUG FOUND - please report this exception (with stacktrace) here: https://github.com/rabbitmq/rabbitmq-dotnet-client/issues";
    }
}
