// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------


using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Xunit;

namespace Test.Unit
{
    public class TestSocketClosedOnFailedOpen
    {
        [Fact]
        public Task AnEndpointWithANullSslClosesItsConnectedSocket_GH2176()
            => AssertTheConnectedSocketIsClosedAsync(
                cf => { },
                port => new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), port, null!));

        [Fact]
        public Task AFailureAfterTheConnectClosesTheSocket_GH2176()
            => AssertTheConnectedSocketIsClosedAsync(
                // A failure the null-Ssl case does not depend on, raised right after the connect.
                cf => cf.SocketFactory = family => new ThrowingGetStreamTcpClient(new Socket(family, SocketType.Stream, ProtocolType.Tcp)),
                port => new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), port));

        private static async Task AssertTheConnectedSocketIsClosedAsync(Action<ConnectionFactory> configure, Func<int, AmqpTcpEndpoint> endpoint)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                var cf = new ConnectionFactory { AutomaticRecoveryEnabled = false, RequestedConnectionTimeout = TimeSpan.FromSeconds(5) };
                configure(cf);
                Task<TcpClient> accept = listener.AcceptTcpClientAsync();

                await Assert.ThrowsAsync<BrokerUnreachableException>(() => cf.CreateConnectionAsync(new[] { endpoint(port) }));

                Assert.True(await Task.WhenAny(accept, Task.Delay(TimeSpan.FromSeconds(10))) == accept,
                    "the client never connected, so nothing was left to close");
                using TcpClient peer = await accept;
                Task<int> read = peer.GetStream().ReadAsync(new byte[1], 0, 1);
                Assert.True(await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(10))) == read, "the client's socket was left open");
                Assert.Equal(0, await read);
            }
            finally
            {
                listener.Stop();
            }
        }

        private sealed class ThrowingGetStreamTcpClient : TcpClientAdapter
        {
            public ThrowingGetStreamTcpClient(Socket socket) : base(socket)
            {
            }

            public override NetworkStream GetStream() => throw new InvalidOperationException("GetStream failed");
        }
    }
}
