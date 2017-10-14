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

#if !NETFX_CORE
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;
using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

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
                throw new TimeoutException();
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
        private readonly NetworkBinaryReader m_reader;
        private readonly ITcpClient m_socket;
        private readonly Stream m_writer;
        private readonly object _semaphore = new object();
        private bool _closed;

         
        public SocketFrameHandler(AmqpTcpEndpoint endpoint,
            Func<AddressFamily, ITcpClient> socketFactory,
            int connectionTimeout, int readTimeout, int writeTimeout)
        {
            Endpoint = endpoint;

            if (ShouldTryIPv6(endpoint))
            {
                try {
                    m_socket = ConnectUsingIPv6(endpoint, socketFactory, connectionTimeout);
                } catch (ConnectFailureException)
                {
                    m_socket = null;
                }
            }

            if (m_socket == null && endpoint.AddressFamily != AddressFamily.InterNetworkV6)
            {
                m_socket = ConnectUsingIPv4(endpoint, socketFactory, connectionTimeout);
            }

            Stream netstream = m_socket.GetStream();
            netstream.ReadTimeout  = readTimeout;
            netstream.WriteTimeout = writeTimeout;

            if (endpoint.Ssl.Enabled)
            {
                try
                {
                    netstream = SslHelper.TcpUpgrade(netstream, endpoint.Ssl);
                }
                catch (Exception)
                {
                    Close();
                    throw;
                }
            }
            m_reader = new NetworkBinaryReader(new BufferedStream(netstream, m_socket.Client.ReceiveBufferSize));
            m_writer = netstream;

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
            lock (_semaphore)
            {
                if (!_closed)
                {
                    try
                    {
                        m_socket.Close();
                    }
                    catch (Exception)
                    {
                        // ignore, we are closing anyway
                    }
                    finally
                    {
                        _closed = true;
                    }
                }
            }
        }

        public InboundFrame ReadFrame()
        {
            return RabbitMQ.Client.Impl.InboundFrame.ReadFrom(m_reader);
        }
        public Task< InboundFrame> ReadFrameAsync()
        {
            return RabbitMQ.Client.Impl.InboundFrame.ReadFromAsync(m_reader);
        }

        private static readonly byte[] amqp = Encoding.ASCII.GetBytes("AMQP");
        public void SendHeader()
        {
            var ms = new MemoryStream();
            var nbw = new NetworkBinaryWriter(ms);
            nbw.Write(amqp, 0, 4);
            byte one = (byte)1;
            if (Endpoint.Protocol.Revision != 0)
            {
                nbw.WriteByte(0);
                nbw.WriteByte((byte)Endpoint.Protocol.MajorVersion);
                nbw.WriteByte((byte)Endpoint.Protocol.MinorVersion);
                nbw.WriteByte((byte)Endpoint.Protocol.Revision);
            }
            else
            {
                nbw.WriteByte(one);
                nbw.WriteByte(one);
                nbw.WriteByte((byte)Endpoint.Protocol.MajorVersion);
                nbw.WriteByte((byte)Endpoint.Protocol.MinorVersion);
            }
            byte[] data = ms.ToArray();
            m_writer.Write(data, 0, data.Length);
        }
        public async Task SendHeaderAsync()
        {
            var ms = new MemoryStream();
            var nbw = new NetworkBinaryWriter(ms);
            nbw.Write(amqp,0,4);
            byte one = (byte)1;
            if (Endpoint.Protocol.Revision != 0)
            {
                nbw.WriteByte(0);
                nbw.WriteByte((byte)Endpoint.Protocol.MajorVersion);
                nbw.WriteByte((byte)Endpoint.Protocol.MinorVersion);
                nbw.WriteByte((byte)Endpoint.Protocol.Revision);
            }
            else
            {
                nbw.WriteByte(one);
                nbw.WriteByte(one);
                nbw.WriteByte((byte)Endpoint.Protocol.MajorVersion);
                nbw.WriteByte((byte)Endpoint.Protocol.MinorVersion);
            }
            byte[] data = ms.ToArray();
            await m_writer.WriteAsync(data, 0, data.Length);
        }

        public void WriteFrame(OutboundFrame frame)
        {
            var ms = new MemoryStream();
            var nbw = new NetworkBinaryWriter(ms);
            frame.WriteTo(nbw);
            m_socket.Client.Poll(m_writeableStateTimeout, SelectMode.SelectWrite);
            byte[] data = ms.ToArray();
            m_writer.Write(data, 0, data.Length);
        }

        public void WriteFrameSet(IList<OutboundFrame> frames)
        {
            var ms = new MemoryStream();
            var nbw = new NetworkBinaryWriter(ms);
            foreach (var f in frames) f.WriteTo(nbw);
            m_socket.Client.Poll(m_writeableStateTimeout, SelectMode.SelectWrite);
            byte[] data = ms.ToArray();
            m_writer.Write(data, 0, data.Length);
        }

        public async Task WriteFrameAsync(OutboundFrame frame)
        {
            var ms = new MemoryStream();
            var nbw = new NetworkBinaryWriter(ms);
            frame.WriteTo(nbw);
            m_socket.Client.Poll(m_writeableStateTimeout, SelectMode.SelectWrite);
            byte[] data = ms.ToArray();
            await m_writer.WriteAsync(data, 0, data.Length);
        }

        public async Task WriteFrameSetAsync(IList<OutboundFrame> frames)
        {
            var ms = new MemoryStream();
            var nbw = new NetworkBinaryWriter(ms);
            foreach (var f in frames) f.WriteTo(nbw);
            m_socket.Client.Poll(m_writeableStateTimeout, SelectMode.SelectWrite);
            byte[] data = ms.ToArray();
            await m_writer.WriteAsync(data, 0, data.Length);
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
        private Task<ITcpClient> ConnectUsingIPv6Async(AmqpTcpEndpoint endpoint,
                                            Func<AddressFamily, ITcpClient> socketFactory,
                                            int timeout)
        {
            return ConnectUsingAddressFamilyAsync(endpoint, socketFactory, timeout, AddressFamily.InterNetworkV6);
        }
        private ITcpClient ConnectUsingIPv4(AmqpTcpEndpoint endpoint,
                                            Func<AddressFamily, ITcpClient> socketFactory,
                                            int timeout)
        {
            return ConnectUsingAddressFamily(endpoint, socketFactory, timeout, AddressFamily.InterNetwork);
        }
        private Task<ITcpClient> ConnectUsingIPv4Async(AmqpTcpEndpoint endpoint,
                                            Func<AddressFamily, ITcpClient> socketFactory,
                                            int timeout)
        {
            return ConnectUsingAddressFamilyAsync(endpoint, socketFactory, timeout, AddressFamily.InterNetwork);
        }

        private ITcpClient ConnectUsingAddressFamily(AmqpTcpEndpoint endpoint,
                                                    Func<AddressFamily, ITcpClient> socketFactory,
                                                    int timeout, AddressFamily family)
        {
            ITcpClient socket = socketFactory(family);
            try {
                ConnectOrFail(socket, endpoint, timeout);
                return socket;
            } catch (ConnectFailureException e) {
                socket.Dispose();
                throw e;
            }
        }
        private async Task<ITcpClient> ConnectUsingAddressFamilyAsync(AmqpTcpEndpoint endpoint,
                                                    Func<AddressFamily, ITcpClient> socketFactory,
                                                    int timeout, AddressFamily family)
        {
            ITcpClient socket = socketFactory(family);
            try
            {
                await ConnectOrFailAsync(socket, endpoint, timeout);
                return socket;
            }
            catch (ConnectFailureException e)
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
        }
        private async Task ConnectOrFailAsync(ITcpClient socket, AmqpTcpEndpoint endpoint, int timeout)
        {
            try
            {
                await socket.ConnectAsync(endpoint.HostName, endpoint.Port).TimeoutAfter(timeout);
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
#endif
