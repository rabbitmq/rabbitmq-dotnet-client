// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2016 Pivotal Software, Inc.
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
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;

namespace RabbitMQ.Client.Exceptions
{
    /// <summary>
    /// Thrown when a session is destroyed during an RPC call to a
    /// broker. For example, if a TCP connection dropping causes the
    /// destruction of a session in the middle of a QueueDeclare
    /// operation, an OperationInterruptedException will be thrown to
    /// the caller of IModel.QueueDeclare.
    /// </summary>
    public class OperationInterruptedException
        // TODO: inherit from OperationCanceledException
        : Exception
    {
        ///<summary>Construct an OperationInterruptedException with
        ///the passed-in explanation, if any.</summary>
        public OperationInterruptedException(ShutdownEventArgs reason)
            : base(reason == null ? "The AMQP operation was interrupted" :
                string.Format("The AMQP operation was interrupted: {0}",
                    reason))
        {
            ShutdownReason = reason;
        }

        ///<summary>Construct an OperationInterruptedException with
        ///the passed-in explanation and prefix, if any.</summary>
        public OperationInterruptedException(ShutdownEventArgs reason, String prefix)
            : base(reason == null ? (prefix + ": The AMQP operation was interrupted") :
                string.Format("{0}: The AMQP operation was interrupted: {1}",
                    prefix, reason))
        {
            ShutdownReason = reason;
        }

        protected OperationInterruptedException()
        {
        }

        protected OperationInterruptedException(string message) : base(message)
        {
        }

        protected OperationInterruptedException(string message, Exception inner)
            : base(message, inner)
        {
        }
/*
#if !(NETFX_CORE)
        protected OperationInterruptedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
*/

        ///<summary>Retrieves the explanation for the shutdown. May
        ///return null if no explanation is available.</summary>
        public ShutdownEventArgs ShutdownReason { get; protected set; }
    }
}
