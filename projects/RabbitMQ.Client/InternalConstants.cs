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

namespace RabbitMQ.Client
{
    internal static class InternalConstants
    {
        internal static readonly TimeSpan DefaultConnectionAbortTimeout = TimeSpan.FromSeconds(5);
        internal static readonly TimeSpan DefaultConnectionCloseTimeout = TimeSpan.FromSeconds(30);
        internal static readonly TimeSpan DefaultChannelDisposeTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The shortest graceful close budget that can actually complete a close.
        /// </summary>
        /// <remarks>
        /// The timeout does not only bound the wait for the peer's reply: it is linked into the
        /// tokens passed to <c>session.SetSessionClosingAsync</c> and the <c>connection.close</c>
        /// transmit. A value too small to reach those cancels the close before it sends anything,
        /// and because that cancellation escapes before the teardown block runs, the main loop is
        /// never awaited, the socket is never closed and the broker keeps the connection until the
        /// process exits - while <c>IsOpen</c> already reports false. Measured: with
        /// <see cref="TimeSpan.Zero"/> the close faults in about 6ms and the connection is still
        /// listed on the broker; with one second it closes cleanly in about 20ms.
        /// <para>
        /// This is deliberately far below the 30 second value it replaced. That floor was not policy
        /// and hid a caller's intent entirely (see #1973); this one exists only to keep a close from
        /// cancelling itself, so any realistic caller value passes through untouched.
        /// </para>
        /// </remarks>
        internal static readonly TimeSpan MinConnectionCloseTimeout = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The fewest consumer dispatch loops a channel may have.
        /// </summary>
        /// <remarks>
        /// Distinct from <see cref="Constants.DefaultConsumerDispatchConcurrency"/> even though both are
        /// 1 today, because they answer different questions: one is what you get when you ask for
        /// nothing, the other is the floor below which the dispatcher cannot function. Sharing a
        /// constant would couple them, so raising the default would silently raise every
        /// zero-configured deployment to the new value and cost it the in-order delivery guarantee.
        /// </remarks>
        internal const ushort MinConsumerDispatchConcurrency = 1;

        /// <summary>
        /// The longest an abort will wait, whatever the caller asked for.
        /// </summary>
        /// <remarks>
        /// An abort is best-effort teardown that never throws, so its value to a caller is that it
        /// returns promptly. Honouring an arbitrarily large abort timeout defeats that: it turns
        /// "tear this down and move on" into a wait that can outlast the process. The caller's value
        /// is still honoured between <see cref="DefaultConnectionAbortTimeout"/> and this ceiling, so
        /// asking for longer is not an error, just capped.
        /// </remarks>
        internal static readonly TimeSpan MaxConnectionAbortTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Largest message size, in bytes, allowed in RabbitMQ.        
        /// Note: <code>rabbit.max_message_size</code> setting (https://www.rabbitmq.com/configure.html)
        /// configures the largest message size which should be lower than this maximum of 128MiB.
        /// </summary>
        internal const uint DefaultRabbitMqMaxInboundMessageBodySize = 1_048_576 * 128;

        /// <summary>
        /// Largest client provide name, in characters, allowed in RabbitMQ.
        /// This is not configurable, but was discovered while working on this issue:
        /// https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/980
        /// </summary>
        internal const int DefaultRabbitMqMaxClientProvideNameLength = 3000;

        internal const string BugFound = "BUG FOUND - please report this exception (with stacktrace) here: https://github.com/rabbitmq/rabbitmq-dotnet-client/issues";
    }
}
