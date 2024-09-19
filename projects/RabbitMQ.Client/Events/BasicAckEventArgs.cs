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

namespace RabbitMQ.Client.Events
{
    ///<summary>Contains all the information about a message acknowledged
    ///from an AMQP broker within the Basic content-class.</summary>
    public class BasicAckEventArgs : AsyncEventArgs
    {
        public BasicAckEventArgs(ulong deliveryTag, bool multiple, bool returned,
            CancellationToken cancellationToken = default)
            : base(cancellationToken)
        {
            DeliveryTag = deliveryTag;
            Multiple = multiple;
            Returned = returned;
        }

        ///<summary>The sequence number of the acknowledged message, or
        ///the closed upper bound of acknowledged messages if multiple
        ///is true.</summary>
        public readonly ulong DeliveryTag;

        ///<summary>Whether this acknowledgement applies to one message
        ///or multiple messages.</summary>
        public readonly bool Multiple;

        ///<summary>
        ///Whether this acknowledgement was due to a <c>basic.return</c>
        ///</summary>
        public readonly bool Returned;
    }
}
