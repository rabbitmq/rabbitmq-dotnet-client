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
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Pipelines.Sockets.Unofficial;

using RabbitMQ.Client.Exceptions;

namespace RabbitMQ.Client.Impl
{
    internal static class TaskExtensions
    {
        public static async Task TimeoutAfter(this Task task, TimeSpan timeout)
        {
            Task returnedTask = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
            if (task != returnedTask)
            {
                Task supressErrorTask = returnedTask.ContinueWith((t, s) => t.Exception.Handle(e => true), null, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                throw new TimeoutException();
            }
        }
    }

    internal sealed class SocketFrameHandler : IFrameHandler
    {
        private readonly ITcpClient _socket;

        // Pipes
        private readonly IDuplexPipe _pipe;
        public PipeReader FrameReader => _pipe.Input;
        public PipeWriter FrameWriter => _pipe.Output;

        private readonly object _semaphore = new object();

        public bool IsClosed { get; private set; }

        public SocketFrameHandler(AmqpTcpEndpoint endpoint, Func<AddressFamily, ITcpClient> socketFactory, TimeSpan connectionTimeout, TimeSpan readTimeout, TimeSpan writeTimeout)
        {
            Endpoint = endpoint;

            // Let's check and see if we are connecting as an IP address first
            if (IPAddress.TryParse(endpoint.HostName, out IPAddress address))
            {
                // Connecting straight via IP so we can ignore whatever AddressFamily is set on the endpoint. We support IPv4 and IPv6.
                _socket = address.AddressFamily switch
                {
                    AddressFamily.InterNetwork or AddressFamily.InterNetworkV6 => ConnectUsingAddressFamily(new IPEndPoint(address, endpoint.Port), socketFactory, connectionTimeout, address.AddressFamily),
                    _ => throw new ConnectFailureException("Connection failed", new ArgumentException($"AddressFamily {address.AddressFamily} is not supported.")),
                };
            }
            else
            {
                // We are connecting via. hostname so let's first resolve all the IP addresses for the hostname
                IPAddress[] adds = Dns.GetHostAddresses(endpoint.HostName);

                // We want to connect via. IPv6 and our Socket supports IPv6 so let's try that first
                if ((endpoint.AddressFamily == AddressFamily.InterNetworkV6 || endpoint.AddressFamily == AddressFamily.Unknown) && Socket.OSSupportsIPv6)
                {
                    // Let's then try to find the appropriate IP address for the hostname
                    IPAddress ipv6 = TcpClientAdapterHelper.GetMatchingHost(adds, AddressFamily.InterNetworkV6);
                    if (ipv6 == null)
                    {
                        throw new ConnectFailureException("Connection failed", new ArgumentException($"No IPv6 address could be resolved for {endpoint.HostName}"));
                    }

                    // Let's see if we can connect to the resolved IP address as IPv6
                    try
                    {
                        _socket = ConnectUsingAddressFamily(new IPEndPoint(ipv6, endpoint.Port), socketFactory, connectionTimeout, AddressFamily.InterNetworkV6);
                    }
                    catch (ConnectFailureException)
                    {
                        // Didn't work, let's fall-back to IPv4
                        _socket = null;
                    }
                }

                // No dice, let's fall-back to IPv4 then.
                if (_socket is null)
                {
                    IPAddress ipv4 = TcpClientAdapterHelper.GetMatchingHost(adds, AddressFamily.InterNetwork);
                    if (ipv4 == null)
                    {
                        throw new ConnectFailureException("Connection failed", new ArgumentException($"No IPv4 address could be resolved for {endpoint.HostName}"));
                    }

                    _socket = ConnectUsingAddressFamily(new IPEndPoint(ipv4, endpoint.Port), socketFactory, connectionTimeout, AddressFamily.InterNetwork);
                }
            }

            // We're done setting up our connection, let's configure timeouts and SSL if needed.
            _socket.ReceiveTimeout = readTimeout;
            
            if (endpoint.Ssl.Enabled)
            {
                try
                {
                    _pipe = StreamConnection.GetDuplex(SslHelper.TcpUpgrade(_socket.GetStream(), endpoint.Ssl));
                }
                catch (Exception)
                {
                    Close();
                    throw;
                }
            }
            else
            {
                _pipe = SocketConnection.Create(_socket.Client);
            }

            WriteTimeout = writeTimeout;
        }

        public AmqpTcpEndpoint Endpoint { get; set; }
        public EndPoint LocalEndPoint => _socket.Client.LocalEndPoint;
        public int LocalPort => ((IPEndPoint)LocalEndPoint).Port;
        public EndPoint RemoteEndPoint => _socket.Client.RemoteEndPoint;
        public int RemotePort => ((IPEndPoint)RemoteEndPoint).Port;

        public TimeSpan ReadTimeout
        {
            set
            {
                try
                {
                    if (_socket.Connected)
                    {
                        _socket.ReceiveTimeout = value;
                    }
                }
                catch (SocketException)
                {
                    // means that the socket is already closed
                }
            }
        }

        public TimeSpan WriteTimeout
        {
            set => _socket.Client.SendTimeout = (int)value.TotalMilliseconds;
        }

        public void Close()
        {
            lock (_semaphore)
            {
                if (IsClosed || _socket == null)
                {
                    return;
                }
                else
                {
                    try
                    {
                        FrameWriter.Complete();
                    }
                    catch
                    {
                        // ignore, we are closing anyway
                    }

                    try
                    {
                        _socket.Close();
                    }
                    catch
                    {
                        // ignore, we are closing anyway
                    }
                    finally
                    {
                        IsClosed = true;
                    }
                }
            }
        }

        private ITcpClient ConnectUsingAddressFamily(IPEndPoint endpoint,
                                                    Func<AddressFamily, ITcpClient> socketFactory,
                                                    TimeSpan timeout, AddressFamily family)
        {
            ITcpClient socket = socketFactory(family);
            try
            {
                ConnectOrFail(socket, endpoint, timeout);
                return socket;
            }
            catch (ConnectFailureException)
            {
                socket.Dispose();
                throw;
            }
        }

        private void ConnectOrFail(ITcpClient socket, IPEndPoint endpoint, TimeSpan timeout)
        {
            try
            {
                socket.ConnectAsync(endpoint.Address, endpoint.Port)
                     .TimeoutAfter(timeout)
                     .ConfigureAwait(false)
                     // this ensures exceptions aren't wrapped in an AggregateException
                     .GetAwaiter()
                     .GetResult();
            }
            catch (ArgumentException e)
            {
                throw new ConnectFailureException("Connection failed", e);
            }
            catch (SocketException e)
            {
                throw new ConnectFailureException("Connection failed", e);
            }
            catch (NotSupportedException e)
            {
                throw new ConnectFailureException("Connection failed", e);
            }
            catch (TimeoutException e)
            {
                throw new ConnectFailureException("Connection failed", e);
            }
        }
    }
}
