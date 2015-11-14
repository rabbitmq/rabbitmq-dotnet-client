// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2015 Pivotal Software, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at http://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Information about the reason why a particular model, session, or connection was destroyed.
    /// </summary>
    /// <remarks>
    /// The <see cref="ClassId"/> and <see cref="Initiator"/> properties should be used to determine the originator of the shutdown event.
    /// </remarks>
    public class ShutdownEventArgs : EventArgs
    {
        /// <summary>
        /// Construct a <see cref="ShutdownEventArgs"/> with the given parameters and
        ///  0 for <see cref="ClassId"/> and <see cref="MethodId"/>.
        /// </summary>
        public ShutdownEventArgs(ShutdownInitiator initiator, ushort replyCode, string replyText, object cause = null)
            : this(initiator, replyCode, replyText, 0, 0, cause)
        {
        }

        /// <summary>
        /// Construct a <see cref="ShutdownEventArgs"/> with the given parameters.
        /// </summary>
        public ShutdownEventArgs(ShutdownInitiator initiator, ushort replyCode, string replyText,
            ushort classId, ushort methodId, object cause = null)
        {
            Initiator = initiator;
            ReplyCode = replyCode;
            ReplyText = replyText;
            ClassId = classId;
            MethodId = methodId;
            Cause = cause;
        }

        /// <summary>
        /// Object causing the shutdown, or null if none.
        /// </summary>
        public object Cause { get; private set; }

        /// <summary>
        /// AMQP content-class ID, or 0 if none.
        /// </summary>
        public ushort ClassId { get; private set; }

        /// <summary>
        /// Returns the source of the shutdown event: either the application, the library, or the remote peer.
        /// </summary>
        public ShutdownInitiator Initiator { get; private set; }

        /// <summary>
        /// AMQP method ID within a content-class, or 0 if none.
        /// </summary>
        public ushort MethodId { get; private set; }

        /// <summary>
        /// One of the standardised AMQP reason codes. See RabbitMQ.Client.Framing.*.Constants.
        /// </summary>
        public ushort ReplyCode { get; private set; }

        /// <summary>
        /// Informative human-readable reason text.
        /// </summary>
        public string ReplyText { get; private set; }

        /// <summary>
        /// Override ToString to be useful for debugging.
        /// </summary>
        public override string ToString()
        {
            return "AMQP close-reason, initiated by " + Initiator +
                   ", code=" + ReplyCode +
                   ", text=\"" + ReplyText + "\"" +
                   ", classId=" + ClassId +
                   ", methodId=" + MethodId +
                   ", cause=" + Cause;
        }
    }
}
