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
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Test.Integration.GH
{
    /// <summary>
    /// A minimal in-process TCP proxy that forwards bytes between a client and an upstream
    /// broker, and, the first time the broker answers a consume, swallows that
    /// <c>Basic.ConsumeOk</c> frame and drops the connection instead. This guarantees a
    /// <c>Basic.Consume</c> RPC is still outstanding at the instant the socket dies, which is
    /// what makes the shutdown-ordering deadlock in GH-2005 deterministic rather than
    /// timing-dependent. Ported from the reproducer attached to that issue.
    /// </summary>
    public sealed class ConsumeOkSwallowingProxy : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly string _upstreamHost;
        private readonly int _upstreamPort;
        private readonly Action<string> _log;
        private readonly List<TcpClient> _sockets = new();
        private int _armed = 1;

        public ConsumeOkSwallowingProxy(string upstreamHost, int upstreamPort, Action<string> log)
        {
            _upstreamHost = upstreamHost;
            _upstreamPort = upstreamPort;
            _log = log;
            _listener = new TcpListener(IPAddress.Loopback, 0);
        }

        /// <summary>The loopback port the proxy is listening on. Valid after <see cref="Start"/>.</summary>
        public int ListenPort { get; private set; }

        public void Start()
        {
            _listener.Start();
            ListenPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _ = Task.Run(AcceptLoopAsync);
        }

        private async Task AcceptLoopAsync()
        {
            while (true)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync();
                }
                catch
                {
                    return;
                }

                var server = new TcpClient();
                await server.ConnectAsync(_upstreamHost, _upstreamPort);
                lock (_sockets)
                {
                    _sockets.Add(client);
                    _sockets.Add(server);
                }

                _ = PumpAsync(client, server, inspect: false);
                _ = PumpAsync(server, client, inspect: true);
            }
        }

        private async Task PumpAsync(TcpClient from, TcpClient to, bool inspect)
        {
            var buffer = new byte[16 * 1024];
            var pending = new List<byte>(64 * 1024);
            try
            {
                while (true)
                {
                    int read = await from.GetStream().ReadAsync(buffer);
                    if (read == 0)
                    {
                        break;
                    }

                    if (!inspect)
                    {
                        await to.GetStream().WriteAsync(buffer.AsMemory(0, read));
                        continue;
                    }

                    // Broker -> client: parse AMQP frames so Basic.ConsumeOk can be spotted.
                    pending.AddRange(buffer.AsSpan(0, read).ToArray());
                    while (pending.Count >= 7)
                    {
                        int size = (int)BinaryPrimitives.ReadUInt32BigEndian(pending.GetRange(3, 4).ToArray());
                        int frameLength = 7 + size + 1;
                        if (pending.Count < frameLength)
                        {
                            break;
                        }

                        byte[] frame = pending.GetRange(0, frameLength).ToArray();
                        pending.RemoveRange(0, frameLength);

                        bool isBasicConsumeOk = frame[0] == 1 && size >= 4
                            && BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(7, 2)) == 60    // class Basic
                            && BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(9, 2)) == 21;   // method ConsumeOk

                        if (isBasicConsumeOk && Interlocked.Exchange(ref _armed, 0) == 1)
                        {
                            _log("proxy: broker answered Basic.ConsumeOk - swallowing it and killing the connection");
                            Kill();
                            return;
                        }

                        await to.GetStream().WriteAsync(frame);
                    }
                }
            }
            catch
            {
            }
            finally
            {
                try { to.Close(); } catch { }
            }
        }

        private void Kill()
        {
            lock (_sockets)
            {
                foreach (TcpClient socket in _sockets)
                {
                    try { socket.Close(); } catch { }
                }

                _sockets.Clear();
            }
        }

        public void Dispose()
        {
            Kill();
            try { _listener.Stop(); } catch { }
        }
    }
}
