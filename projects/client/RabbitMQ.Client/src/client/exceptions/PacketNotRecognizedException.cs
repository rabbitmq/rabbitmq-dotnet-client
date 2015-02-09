// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2014 GoPivotal, Inc.
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
//  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Net;

namespace RabbitMQ.Client.Exceptions
{
    ///<summary>Thrown to indicate that the peer didn't understand
    ///the packet received from the client. Peer sent default message
    ///describing protocol version it is using and transport parameters.
    ///</summary>
    ///<remarks>
    ///The peer's {'A','M','Q','P',txHi,txLo,major,minor} packet is
    ///decoded into instances of this class.
    ///</remarks>
    public class PacketNotRecognizedException : ProtocolViolationException
    {
        ///<summary>Fills the new instance's properties with the values passed in.</summary>
        public PacketNotRecognizedException(int transportHigh,
            int transportLow,
            int serverMajor,
            int serverMinor)
            : base("AMQP server protocol version " + serverMajor + "-" + serverMinor +
                   ", transport parameters " + transportHigh + ":" + transportLow)
        {
            TransportHigh = transportHigh;
            TransportLow = transportLow;
            ServerMajor = serverMajor;
            ServerMinor = serverMinor;
        }

        ///<summary>The peer's AMQP specification major version.</summary>
        public int ServerMajor { get; private set; }

        ///<summary>The peer's AMQP specification minor version.</summary>
        public int ServerMinor { get; private set; }

        ///<summary>The peer's high transport byte.</summary>
        public int TransportHigh { get; private set; }

        ///<summary>The peer's low transport byte.</summary>
        public int TransportLow { get; private set; }
    }
}
