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

using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Exceptions
{
    /// <summary> Instances of subclasses of subclasses
    /// HardProtocolException and SoftProtocolException are thrown in
    /// situations when we detect a problem with the connection-,
    /// channel- or wire-level parts of the AMQP protocol. </summary>
    public abstract class ProtocolException : RabbitMQClientException
    {
        protected ProtocolException(string message) : base(message)
        {
        }

        ///<summary>Retrieve the reply code to use in a
        ///connection/channel close method.</summary>
        public abstract ushort ReplyCode { get; }

        ///<summary>Retrieve the shutdown details to use in a
        ///connection/channel close method. Defaults to using
        ///ShutdownInitiator.Library, and this.ReplyCode and
        ///this.Message as the reply code and text,
        ///respectively.</summary>
        public virtual ShutdownEventArgs ShutdownReason
        {
            get { return new ShutdownEventArgs(ShutdownInitiator.Library, ReplyCode, Message, this); }
        }
    }
}
