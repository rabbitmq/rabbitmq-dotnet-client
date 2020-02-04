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
//       https://www.apache.org/licenses/LICENSE-2.0
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
//  at https://www.mozilla.org/MPL/
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
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Pipelines.Sockets.Unofficial;

using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    static class TaskExtensions
    {
        public static Task CompletedTask = Task.FromResult(0);

        public static async Task TimeoutAfter(this Task task, int millisecondsTimeout)
        {
            if (task == await Task.WhenAny(task, Task.Delay(millisecondsTimeout)).ConfigureAwait(false))
                await task;
            else
            {
                var supressErrorTask = task.ContinueWith(t => t.Exception.Handle(e => true), TaskContinuationOptions.OnlyOnFaulted);
                throw new TimeoutException();
            }
        }
    }

    public class SocketFrameHandler : IFrameHandler
    {
        // Timeout in seconds to wait for a clean socket close.
        private const int SOCKET_CLOSING_TIMEOUT = 1;
        // Socket poll timeout in ms. If the socket does not
        // become writeable in this amount of time, we throw
        // an exception.
        private int m_writeableStateTimeout = 30000;
        private readonly PipelineBinaryReader m_reader;
        private readonly ITcpClient m_socket;
        private readonly PipelineBinaryWriter m_writer;
        private readonly object _semaphore = new object();
        private readonly object _sslStreamLock = new object();
        private readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1);
        private readonly CancellationTokenSource _connectionCancellation = new CancellationTokenSource();
        private readonly IDuplexPipe _duplexPipe;
        private Task _socketCheckTask;

        public SocketFrameHandler(AmqpTcpEndpoint endpoint,
            Func<AddressFamily, ITcpClient> socketFactory,
            int connectionTimeout, int readTimeout, int writeTimeout)
        {
            Endpoint = endpoint;

            if (ShouldTryIPv6(endpoint))
            {
                try
                {
                    m_socket = ConnectUsingIPv6(endpoint, socketFactory, connectionTimeout);
                }
                catch (ConnectFailureException)
                {
                    m_socket = null;
                }
            }

            if (m_socket == null && endpoint.AddressFamily != AddressFamily.InterNetworkV6)
            {
                m_socket = ConnectUsingIPv4(endpoint, socketFactory, connectionTimeout);
            }

            if (endpoint.Ssl.Enabled)
            {
                try
                {
                    Stream netstream = m_socket.GetStream();
                    netstream.ReadTimeout = readTimeout;
                    netstream.WriteTimeout = writeTimeout;
                    netstream = SslHelper.TcpUpgrade(netstream, endpoint.Ssl);
                    _duplexPipe = StreamConnection.GetDuplex(netstream);
                }
                catch (Exception)
                {
                    Close();
                    throw;
                }
            }
            else
            {
                _duplexPipe = SocketConnection.Create(m_socket.Client);
            }

            m_reader = new PipelineBinaryReader(_duplexPipe.Input);
            m_writer = new PipelineBinaryWriter(_duplexPipe.Output);
            _socketCheckTask = Task.Run(async () =>
            {
                while (!_connectionCancellation.IsCancellationRequested)
                {
                    await Task.Delay(1000).ConfigureAwait(false);
                    if (!m_socket.Client.Connected)
                    {
                        _connectionCancellation.Cancel();
                    }
                }
            });

            m_writeableStateTimeout = writeTimeout;
        }

        public AmqpTcpEndpoint Endpoint { get; set; }

        public EndPoint LocalEndPoint
        {
            get { return m_socket.Client.LocalEndPoint; }
        }

        public int LocalPort
        {
            get { return ((IPEndPoint)LocalEndPoint).Port; }
        }

        public EndPoint RemoteEndPoint
        {
            get { return m_socket.Client.RemoteEndPoint; }
        }

        public int RemotePort
        {
            get { return ((IPEndPoint)LocalEndPoint).Port; }
        }

        public int ReadTimeout
        {
            set
            {
                try
                {
                    if (m_socket.Connected)
                    {
                        m_socket.ReceiveTimeout = value;
                    }
                }
                catch (SocketException)
                {
                    // means that the socket is already closed
                }
            }
        }

        public int WriteTimeout
        {
            set
            {
                m_writeableStateTimeout = value;
                m_socket.Client.SendTimeout = value;
            }
        }

        public void Close()
        {
            if (!_connectionCancellation.IsCancellationRequested)
            {
                _connectionCancellation.Cancel();
            }

            m_socket.Close();
            _connectionCancellation.Dispose();
        }

        public ValueTask<InboundFrame> ReadFrameAsync()
        {
            return InboundFrame.ReadFromAsync(m_reader, _connectionCancellation.Token);
        }

        private static readonly byte[] amqp = Encoding.ASCII.GetBytes("AMQP");
        public void SendHeader()
        {
            byte[] versionArray = ArrayPool<byte>.Shared.Rent(4);

            if (Endpoint.Protocol.Revision != 0)
            {
                versionArray[0] = 0;
                versionArray[1] = (byte)Endpoint.Protocol.MajorVersion;
                versionArray[2] = (byte)Endpoint.Protocol.MinorVersion;
                versionArray[3] = (byte)Endpoint.Protocol.Revision;
            }
            else
            {
                versionArray[0] = 1;
                versionArray[1] = 1;
                versionArray[2] = (byte)Endpoint.Protocol.MajorVersion;
                versionArray[3] = (byte)Endpoint.Protocol.MinorVersion;
            }
            m_writer.Write(amqp);
            m_writer.Write(versionArray, 0, 4);
            m_writer.Flush();
            ArrayPool<byte>.Shared.Return(versionArray);
        }

        public void WriteFrame(OutboundFrame frame)
        {
            _writeSemaphore.Wait();
            frame.WriteTo(m_writer);
            m_writer.Flush();
            _writeSemaphore.Release();
        }

        public void WriteFrameSet(IList<OutboundFrame> frames)
        {
            _writeSemaphore.Wait();
            foreach (OutboundFrame frame in frames)
            {
                frame.WriteTo(m_writer);
            }
            m_writer.Flush();
            _writeSemaphore.Release();
        }

        private bool ShouldTryIPv6(AmqpTcpEndpoint endpoint)
        {
            return (Socket.OSSupportsIPv6 && endpoint.AddressFamily != AddressFamily.InterNetwork);
        }

        private ITcpClient ConnectUsingIPv6(AmqpTcpEndpoint endpoint,
                                            Func<AddressFamily, ITcpClient> socketFactory,
                                            int timeout)
        {
            return ConnectUsingAddressFamily(endpoint, socketFactory, timeout, AddressFamily.InterNetworkV6);
        }

        private ITcpClient ConnectUsingIPv4(AmqpTcpEndpoint endpoint,
                                            Func<AddressFamily, ITcpClient> socketFactory,
                                            int timeout)
        {
            return ConnectUsingAddressFamily(endpoint, socketFactory, timeout, AddressFamily.InterNetwork);
        }

        private ITcpClient ConnectUsingAddressFamily(AmqpTcpEndpoint endpoint,
                                                    Func<AddressFamily, ITcpClient> socketFactory,
                                                    int timeout, AddressFamily family)
        {
            ITcpClient socket = socketFactory(family);
            try
            {
                ConnectOrFail(socket, endpoint, timeout);
                return socket;
            }
            catch (ConnectFailureException e)
            {
                socket.Dispose();
                throw e;
            }
            catch (Exception e)
            {
                socket.Dispose();
                throw e;
            }
        }

        private void ConnectOrFail(ITcpClient socket, AmqpTcpEndpoint endpoint, int timeout)
        {
            try
            {
                socket.ConnectAsync(endpoint.HostName, endpoint.Port)
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
            catch (Exception e)
            {
                throw new ConnectFailureException("Connection failed", e);
            }
        }
    }
}
