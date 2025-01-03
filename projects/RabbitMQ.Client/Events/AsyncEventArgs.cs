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

using System.Threading;

namespace RabbitMQ.Client.Events
{
    /// <summary>
    /// Provides data for <see cref="AsyncEventHandler{T}"/>
    /// events that can be invoked asynchronously.
    /// </summary>
    public class AsyncEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncEventArgs"/>
        /// class.
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token related to the original operation that raised
        /// the event.  It's important for your handler to pass this token
        /// along to any asynchronous or long-running synchronous operations
        /// that take a token so cancellation will correctly propagate.  The
        /// default value is <see cref="CancellationToken.None"/>.
        /// </param>
        public AsyncEventArgs(CancellationToken cancellationToken = default)
            : base()
        {
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets a cancellation token related to the original operation that
        /// raised the event.  It's important for your handler to pass this
        /// token along to any asynchronous or long-running synchronous
        /// operations that take a token so cancellation (via something like
        /// <code>
        /// new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token
        /// </code>
        /// for example) will correctly propagate.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        public static AsyncEventArgs CreateOrDefault(CancellationToken cancellationToken)
        {
            if (cancellationToken.CanBeCanceled)
            {
                return new AsyncEventArgs(cancellationToken);
            }

            return Empty;
        }

        /// <summary>
        /// Provides a value to use with events that do not have event data.
        /// </summary>
        public static readonly AsyncEventArgs Empty = new AsyncEventArgs();
    }
}
