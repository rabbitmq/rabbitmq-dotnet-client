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
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;

#if !NETFX_CORE
using System.Net.Sockets;
#else
using Windows.Networking.Sockets;
#endif

using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Object describing various overarching parameters
    /// associated with a particular AMQP protocol variant.
    /// </summary>
    public interface IProtocol
    {
        /// <summary>
        /// Retrieve the protocol's API name, used for printing,
        /// configuration properties, IDE integration, Protocols.cs etc.
        /// </summary>
        string ApiName { get; }

        /// <summary>
        /// Retrieve the protocol's default TCP port.
        /// </summary>
        int DefaultPort { get; }

        /// <summary>
        /// Retrieve the protocol's major version number.
        /// </summary>
        int MajorVersion { get; }

        /// <summary>
        /// Retrieve the protocol's minor version number.
        /// </summary>
        int MinorVersion { get; }

        /// <summary>
        /// Retrieve the protocol's revision (if specified).
        /// </summary>
        int Revision { get; }

        /// <summary>
        /// Construct a connection from a given set of parameters,
        /// a frame handler, and no automatic recovery.
        /// The "insist" parameter is passed on to the AMQP connection.open method.
        /// </summary>
        IConnection CreateConnection(IConnectionFactory factory, bool insist, IFrameHandler frameHandler);

        /// <summary>
        /// Construct a connection from a given set of parameters,
        /// a frame handler, and automatic recovery settings.
        /// </summary>
        IConnection CreateConnection(ConnectionFactory factory, IFrameHandler frameHandler, bool automaticRecoveryEnabled);

        /// <summary>
        /// Construct a connection from a given set of parameters,
        /// a frame handler, a client-provided name, and no automatic recovery.
        /// The "insist" parameter is passed on to the AMQP connection.open method.
        /// </summary>
        IConnection CreateConnection(IConnectionFactory factory, bool insist, IFrameHandler frameHandler, String clientProvidedName);

        /// <summary>
        /// Construct a connection from a given set of parameters,
        /// a frame handler, a client-provided name, and automatic recovery settings.
        /// </summary>
        IConnection CreateConnection(ConnectionFactory factory, IFrameHandler frameHandler, bool automaticRecoveryEnabled, String clientProvidedName);

//         /// <summary>
//         ///  Construct a frame handler for a given endpoint.
//         ///  </summary>
//         /// <param name="socketFactory">Socket factory method.</param>
//         /// <param name="connectionTimeout">Timeout in milliseconds.</param>
//         /// <param name="endpoint">Represents a TCP-addressable AMQP peer: a host name and port number.</param>
//         IFrameHandler CreateFrameHandler(
//             AmqpTcpEndpoint endpoint, 
// #if !NETFX_CORE
//             Func<AddressFamily, ITcpClient> socketFactory, 
// #else
//             Func<StreamSocket> socketFactory,
// #endif
//             int connectionTimeout,
//             int readTimeout,
//             int writeTimeout);
        /// <summary>
        /// Construct a protocol model atop a given session.
        /// </summary>
        IModel CreateModel(ISession session);
    }
}
