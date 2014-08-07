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

using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client
{
    ///<summary>Object describing various overarching parameters
    ///associated with a particular AMQP protocol variant.</summary>
    public interface IProtocol
    {
        ///<summary>Retrieve the protocol's major version number</summary>
        int MajorVersion { get; }
        ///<summary>Retrieve the protocol's minor version number</summary>
        int MinorVersion { get; }
        ///<summary>Retrieve the protocol's revision (if specified)</summary>
        int Revision { get; }
        ///<summary>Retrieve the protocol's API name, used for
        ///printing, configuration properties, IDE integration,
        ///Protocols.cs etc.</summary>
        string ApiName { get; }
        ///<summary>Retrieve the protocol's default TCP port</summary>
        int DefaultPort { get; }

        ///<summary>Construct a frame handler for a given endpoint.</summary>
        IFrameHandler CreateFrameHandler(AmqpTcpEndpoint endpoint,
                                         ConnectionFactoryBase.ObtainSocket socketFactory,
                                         int timeout);
        ///<summary>Construct a connection from a given set of
        ///parameters and a frame handler. The "insist" parameter is
        ///passed on to the AMQP connection.open method.</summary>
        IConnection CreateConnection(IConnectionFactory factory,
                                     bool insist,
                                     IFrameHandler frameHandler);
        ///<summary>Construct a protocol model atop a given session.</summary>
        IModel CreateModel(ISession session);
    }
}
