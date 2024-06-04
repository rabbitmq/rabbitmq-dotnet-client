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

namespace RabbitMQ.Client.Events
{
    ///<summary>Contains all the information about a message returned
    ///from an AMQP broker within the Basic content-class.</summary>
    public class BasicReturnEventArgs : EventArgs
    {
        public BasicReturnEventArgs(
            ushort replyCode,
            string replyText,
            string exchange,
            string routingKey,
            ReadOnlyBasicProperties basicProperties,
            ReadOnlyMemory<byte> body) : base()
        {
            ReplyCode = replyCode;
            ReplyText = replyText;
            Exchange = exchange;
            RoutingKey = routingKey;
            BasicProperties = basicProperties;
            Body = body;
        }

        ///<summary>The content header of the message.</summary>
        public readonly ReadOnlyBasicProperties BasicProperties;

        ///<summary>The message body.</summary>
        public readonly ReadOnlyMemory<byte> Body;

        ///<summary>The exchange the returned message was originally
        ///published to.</summary>
        public readonly string Exchange;

        ///<summary>The AMQP reason code for the return. See
        ///RabbitMQ.Client.Framing.*.Constants.</summary>
        public readonly ushort ReplyCode;

        ///<summary>Human-readable text from the broker describing the
        ///reason for the return.</summary>
        public readonly string ReplyText;

        ///<summary>The routing key used when the message was
        ///originally published.</summary>
        public readonly string RoutingKey;
    }
}
