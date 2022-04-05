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
using System.Net.Sockets;
using Pipelines.Sockets.Unofficial;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client
{
    public class ConnectionFactoryBase
    {
        /// <summary>
        /// Set custom socket options by providing a SocketFactory.
        /// </summary>
        public Func<AddressFamily, ITcpClient> SocketFactory = DefaultSocketFactory;

        /// <summary>
        /// Creates a new instance of the <see cref="TcpClient"/>.
        /// </summary>
        /// <param name="addressFamily">Specifies the addressing scheme.</param>
        /// <returns>New instance of a <see cref="TcpClient"/>.</returns>
        public static ITcpClient DefaultSocketFactory(AddressFamily addressFamily)
        {
            var socket = new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp);
            SocketConnection.SetRecommendedClientOptions(socket);
            return new TcpClientAdapter(socket);
        }
    }
}
