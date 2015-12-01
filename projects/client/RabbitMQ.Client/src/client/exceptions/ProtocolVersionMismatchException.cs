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
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Net;

namespace RabbitMQ.Client.Exceptions
{
    ///<summary>Thrown to indicate that the peer does not support the
    ///wire protocol version we requested immediately after opening
    ///the TCP socket.</summary>
    public class ProtocolVersionMismatchException : ProtocolViolationException
    {
        ///<summary>Fills the new instance's properties with the values passed in.</summary>
        public ProtocolVersionMismatchException(int clientMajor,
            int clientMinor,
            int serverMajor,
            int serverMinor)
            : base("AMQP server protocol negotiation failure: server version " +
                   positiveOrUnknown(serverMajor) + "-" + positiveOrUnknown(serverMinor) +
                   ", client version " + positiveOrUnknown(clientMajor) + "-" + positiveOrUnknown(clientMinor))
        {
            ClientMajor = clientMajor;
            ClientMinor = clientMinor;
            ServerMajor = serverMajor;
            ServerMinor = serverMinor;
        }

        ///<summary>The client's AMQP specification major version.</summary>
        public int ClientMajor { get; private set; }

        ///<summary>The client's AMQP specification minor version.</summary>
        public int ClientMinor { get; private set; }

        ///<summary>The peer's AMQP specification major version.</summary>
        public int ServerMajor { get; private set; }

        ///<summary>The peer's AMQP specification minor version.</summary>
        public int ServerMinor { get; private set; }

        private static String positiveOrUnknown(int version)
        {
            return version >= 0 ? version.ToString() : "unknown";
        }
    }
}
