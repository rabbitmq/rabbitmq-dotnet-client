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
    public class TestConnectionStartTimeout
    {
        [Fact]
        public Task APeerThatNeverSendsConnectionStartFailsTheAttempt_GH1871()
            => AssertASilentPeerFailsTheAttempt(tls: false);

        [Fact]
        public Task APeerThatNeverAnswersTheTlsHandshakeFailsTheAttempt_GH1871()
            => AssertASilentPeerFailsTheAttempt(tls: true);

        private static async Task AssertASilentPeerFailsTheAttempt(bool tls)
        {
            // Accepts the TCP connection and never answers, as a half-up broker host can.
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Task<TcpClient> accept = listener.AcceptTcpClientAsync();
            try
            {
                var cf = new ConnectionFactory
                {
                    HostName = IPAddress.Loopback.ToString(),
                    Port = ((IPEndPoint)listener.LocalEndpoint).Port,
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(1),
                    HandshakeContinuationTimeout = TimeSpan.FromSeconds(1)
                };
                cf.Ssl.Enabled = tls;
                cf.Ssl.ServerName = "localhost";

                Task<IConnection> connect = cf.CreateConnectionAsync();

                Task finished = await Task.WhenAny(connect, Task.Delay(TimeSpan.FromSeconds(15)));
                Assert.True(ReferenceEquals(connect, finished), "the attempt is still waiting on the silent peer");

                await Assert.ThrowsAsync<BrokerUnreachableException>(() => connect);
            }
            finally
            {
                // Also what unwinds a hung attempt: the client sees the connection drop.
                listener.Stop();
                if (accept.Status == TaskStatus.RanToCompletion)
                {
                    (await accept).Dispose();
                }
            }
        }
    }
}
