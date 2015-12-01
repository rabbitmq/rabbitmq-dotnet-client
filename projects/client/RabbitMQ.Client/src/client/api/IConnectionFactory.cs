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
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabbitMQ.Client
{
    public interface IConnectionFactory
    {
        /// <summary>
        /// Dictionary of client properties to be sent to the server.
        /// </summary>
        IDictionary<String, object> ClientProperties { get; set; }

        /// <summary>
        /// Password to use when authenticating to the server.
        /// </summary>
        string Password { get; set; }

        /// <summary>
        /// Maximum channel number to ask for.
        /// </summary>
        ushort RequestedChannelMax { get; set; }

        /// <summary>
        /// Frame-max parameter to ask for (in bytes).
        /// </summary>
        uint RequestedFrameMax { get; set; }

        /// <summary>
        /// Heartbeat setting to request (in seconds).
        /// </summary>
        ushort RequestedHeartbeat { get; set; }

        /// <summary>
        /// When set to true, background threads will be used for I/O and heartbeats.
        /// </summary>
        bool UseBackgroundThreadsForIO { get; set; }

        /// <summary>
        /// Username to use when authenticating to the server.
        /// </summary>
        string UserName { get; set; }

        /// <summary>
        /// Virtual host to access during this connection.
        /// </summary>
        string VirtualHost { get; set; }

        /// <summary>
        /// Given a list of mechanism names supported by the server, select a preferred mechanism,
        /// or null if we have none in common.
        /// </summary>
        AuthMechanismFactory AuthMechanismFactory(IList<string> mechanismNames);

        /// <summary>
        /// Create a connection to the specified endpoint.
        /// </summary>
        IConnection CreateConnection();

        /// <summary>
        /// Connects to the first reachable hostname from the list.
        /// </summary>
        /// <param name="hostnames">List of host names to use</param>
        /// <returns></returns>
        IConnection CreateConnection(IList<string> hostnames);

        /// <summary>
        /// Advanced option.
        /// 
        /// What task scheduler should consumer dispatcher use.
        /// </summary>
        TaskScheduler TaskScheduler { get; set; }

        /// <summary>
        /// Amount of time protocol handshake operations are allowed to take before
        /// timing out.
        /// </summary>
        TimeSpan HandshakeContinuationTimeout { get; set; }

        /// <summary>
        /// Amount of time protocol  operations (e.g. <code>queue.declare</code>) are allowed to take before
        /// timing out.
        /// </summary>
        TimeSpan ContinuationTimeout { get; set; }
    }
}
