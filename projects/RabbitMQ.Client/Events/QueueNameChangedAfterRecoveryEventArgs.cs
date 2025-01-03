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
using System.Threading;

namespace RabbitMQ.Client.Events
{
    public sealed class QueueNameChangedAfterRecoveryEventArgs : AsyncEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QueueNameChangedAfterRecoveryEventArgs"/> class.
        /// </summary>
        /// <param name="nameBefore">The name before.</param>
        /// <param name="nameAfter">The name after.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public QueueNameChangedAfterRecoveryEventArgs(string nameBefore, string nameAfter, CancellationToken cancellationToken = default)
            : base(cancellationToken)
        {
            NameBefore = nameBefore;
            NameAfter = nameAfter;
        }

        /// <summary>
        /// Gets the name before.
        /// </summary>
        public readonly string NameBefore;

        /// <summary>
        /// Gets the name after.
        /// </summary>
        public readonly string NameAfter;
    }
}
