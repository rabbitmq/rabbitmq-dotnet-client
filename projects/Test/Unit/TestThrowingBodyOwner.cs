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
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Impl;
using Xunit;

namespace Test.Unit
{
    public class TestThrowingBodyOwner
    {
        [Fact]
        public async Task ABodyOwnerThatThrowsFromDisposeDoesNotStopTheWriteLoop_GH2100()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var endpoint = new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)listener.LocalEndpoint).Port);
                Task<TcpClient> accept = listener.AcceptTcpClientAsync();
                SocketFrameHandler frameHandler = await SocketFrameHandler.CreateAsync(endpoint,
                    ConnectionFactoryBase.DefaultSocketFactory, TimeSpan.FromSeconds(5), CancellationToken.None);
                using TcpClient peer = await accept;
                try
                {
                    OutgoingFrame first = Frame(new ThrowingOwner());
                    OutgoingFrame second = Frame(null);
                    int expected = first.Size + second.Size;

                    await frameHandler.WriteAsync(first, CancellationToken.None);
                    await frameHandler.WriteAsync(second, CancellationToken.None);

                    // Both frames reach the peer only if the write loop survived the first owner.
                    var buffer = new byte[expected];
                    int received = 0;
                    // Closing the peer is what ends a stuck read on .NET Framework, which ignores the token.
                    using var readTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    using CancellationTokenRegistration closePeer = readTimeout.Token.Register(() => peer.Close());
                    NetworkStream stream = peer.GetStream();
                    while (received < expected)
                    {
                        int n = await stream.ReadAsync(buffer, received, expected - received, readTimeout.Token);
                        Assert.True(n > 0, "the peer's connection closed early");
                        received += n;
                    }
                }
                finally
                {
                    await frameHandler.CloseAsync(CancellationToken.None);
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public void DisposingAFrameWhoseOwnerThrowsReturns_GH2100()
        {
            OutgoingFrame frame = Frame(new ThrowingOwner());

            frame.Dispose();
        }

        [Fact]
        public async Task AWriteOnAClosedFrameHandlerWithAThrowingOwnerReturns_GH2100()
        {
            // The closed path disposes the frame synchronously; a throw reached the caller, which
            // disposed its own copy of the frame again.
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var endpoint = new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)listener.LocalEndpoint).Port);
                Task<TcpClient> accept = listener.AcceptTcpClientAsync();
                SocketFrameHandler frameHandler = await SocketFrameHandler.CreateAsync(endpoint,
                    ConnectionFactoryBase.DefaultSocketFactory, TimeSpan.FromSeconds(5), CancellationToken.None);
                using TcpClient peer = await accept;
                await frameHandler.CloseAsync(CancellationToken.None);

                await frameHandler.WriteAsync(Frame(new ThrowingOwner()), CancellationToken.None);
            }
            finally
            {
                listener.Stop();
            }
        }

        private static OutgoingFrame Frame(IDisposable owner)
        {
            var method = new BasicPublish("exchange", "routing-key", false, false);
            var header = new BasicProperties();
            ReadOnlyMemory<byte> body = new byte[8];
            return RabbitMQ.Client.Impl.Framing.SerializeToFrames(ref method, ref header, body, owner, 1, int.MaxValue);
        }

        private sealed class ThrowingOwner : IDisposable
        {
            public void Dispose() => throw new InvalidOperationException("owner bug");
        }
    }
}
