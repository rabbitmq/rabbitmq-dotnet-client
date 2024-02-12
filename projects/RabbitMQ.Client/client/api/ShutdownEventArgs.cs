// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2020 VMware, Inc.
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
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Information about the reason why a particular channel, session, or connection was destroyed.
    /// </summary>
    /// <remarks>
    /// The <see cref="ClassId"/> and <see cref="Initiator"/> properties should be used to determine the originator of the shutdown event.
    /// </remarks>
    public class ShutdownEventArgs : EventArgs
    {
        private readonly Exception _exception;

        /// <summary>
        /// Construct a <see cref="ShutdownEventArgs"/> with the given parameters and
        ///  0 for <see cref="ClassId"/> and <see cref="MethodId"/>.
        /// </summary>
        public ShutdownEventArgs(ShutdownInitiator initiator, ushort replyCode, string replyText,
            object cause = null)
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
        /// Construct a <see cref="ShutdownEventArgs"/> with the given parameters.
        /// </summary>
        public ShutdownEventArgs(ShutdownInitiator initiator, ushort replyCode, string replyText, Exception exception)
            : this(initiator, replyCode, replyText, 0, 0)
        {
            _exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        /// <summary>
        /// Exception causing the shutdown, or null if none.
        /// </summary>
        public Exception Exception
        {
            get
            {
                return _exception;
            }
        }

        /// <summary>
        /// Object causing the shutdown, or null if none.
        /// </summary>
        public object Cause { get; }

        /// <summary>
        /// AMQP content-class ID, or 0 if none.
        /// </summary>
        public ushort ClassId { get; }

        /// <summary>
        /// Returns the source of the shutdown event: either the application, the library, or the remote peer.
        /// </summary>
        public ShutdownInitiator Initiator { get; }

        /// <summary>
        /// AMQP method ID within a content-class, or 0 if none.
        /// </summary>
        public ushort MethodId { get; }

        /// <summary>
        /// One of the standardised AMQP reason codes. See RabbitMQ.Client.Framing.*.Constants.
        /// </summary>
        public ushort ReplyCode { get; }

        /// <summary>
        /// Informative human-readable reason text.
        /// </summary>
        public string ReplyText { get; }

        /// <summary>
        /// Override ToString to be useful for debugging.
        /// </summary>
        public override string ToString()
        {
            return GetMessageCore()
                + (_exception != null ? $", exception={_exception}" : string.Empty);
        }

        /// <summary>
        /// Gets a message suitable for logging.
        /// </summary>
        /// <remarks>
        /// This leaves out the full exception ToString since logging will include it separately.
        /// </remarks>
        internal string GetLogMessage()
        {
            return GetMessageCore()
                + (_exception != null ? $", exception={_exception.Message}" : string.Empty);
        }

        private string GetMessageCore()
        {
            return $"AMQP close-reason, initiated by {Initiator}"
                + $", code={ReplyCode}"
                + (ReplyText != null ? $", text='{ReplyText}'" : string.Empty)
                + $", classId={ClassId}"
                + $", methodId={MethodId}"
                + (Cause != null ? $", cause={Cause}" : string.Empty);
        }

    }
}
