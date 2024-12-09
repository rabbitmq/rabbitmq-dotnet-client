// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Exceptions;

namespace RabbitMQ.Client.Impl
{
    internal static class SocketFactory
    {
        public static async Task<ITcpClient> OpenAsync(AmqpTcpEndpoint amqpTcpEndpoint, Func<AddressFamily, ITcpClient> socketFactory,
            TimeSpan connectionTimeout, CancellationToken cancellationToken)
        {
            IPAddress[] ipAddresses = await Dns.GetHostAddressesAsync(
                amqpTcpEndpoint.HostName
#if NET
                , cancellationToken
#endif
            ).ConfigureAwait(false);

            IPAddress? ipv6 = TcpClientAdapter.GetMatchingHost(ipAddresses, AddressFamily.InterNetworkV6);
            if (ipv6 == default(IPAddress))
            {
                if (amqpTcpEndpoint.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    throw new ConnectFailureException($"Connection failed, host {amqpTcpEndpoint}",
                        new ArgumentException($"No IPv6 address could be resolved for {amqpTcpEndpoint}"));
                }
            }
            else if (ShouldTryIPv6(amqpTcpEndpoint))
            {
                try
                {
                    return await ConnectUsingAddressFamilyAsync(new IPEndPoint(ipv6, amqpTcpEndpoint.Port), socketFactory,
                            AddressFamily.InterNetworkV6, connectionTimeout, cancellationToken).ConfigureAwait(false);
                }
                catch (ConnectFailureException)
                {
                    // We resolved to a ipv6 address and tried it but it still didn't connect, try IPv4
                }
            }

            IPAddress? ipv4 = TcpClientAdapter.GetMatchingHost(ipAddresses, AddressFamily.InterNetwork);
            if (ipv4 == default(IPAddress))
            {
                throw new ConnectFailureException($"Connection failed, host {amqpTcpEndpoint}",
                    new ArgumentException($"No ip address could be resolved for {amqpTcpEndpoint}"));
            }

            return await ConnectUsingAddressFamilyAsync(new IPEndPoint(ipv4, amqpTcpEndpoint.Port), socketFactory,
                    AddressFamily.InterNetwork, connectionTimeout, cancellationToken).ConfigureAwait(false);
        }

        private static bool ShouldTryIPv6(AmqpTcpEndpoint endpoint)
        {
            return Socket.OSSupportsIPv6 && endpoint.AddressFamily != AddressFamily.InterNetwork;
        }

        private static async ValueTask<ITcpClient> ConnectUsingAddressFamilyAsync(IPEndPoint endpoint, Func<AddressFamily, ITcpClient> socketFactory,
            AddressFamily family, TimeSpan connectionTimeout, CancellationToken cancellationToken)
        {
            /*
             * Create linked cancellation token that includes the connection timeout value
             * https://learn.microsoft.com/en-us/dotnet/standard/threading/how-to-listen-for-multiple-cancellation-requests
             */
            using var timeoutTokenSource = new CancellationTokenSource(connectionTimeout);
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, cancellationToken);

            ITcpClient socket = socketFactory(family);
            try
            {
                await socket.ConnectAsync(endpoint.Address, endpoint.Port, linkedTokenSource.Token).ConfigureAwait(false);
                return socket;
            }
            catch (Exception e)
            {
                socket.Dispose();

                string msg = $"Connection failed, host {endpoint}";

                if (e is ArgumentException or SocketException or NotSupportedException)
                {
                    throw new ConnectFailureException(msg, e);
                }

                if (e is OperationCanceledException && timeoutTokenSource.Token.IsCancellationRequested)
                {
                    throw new ConnectFailureException(msg, new TimeoutException(msg, e));
                }

                throw;
            }
        }
    }
}
