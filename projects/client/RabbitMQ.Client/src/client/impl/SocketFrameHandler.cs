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
        public const int SOCKET_CLOSING_TIMEOUT = 1;
        // Socket poll timeout in ms. If the socket does not
        // become writeable in this amount of time, we throw
        // an exception.
        protected int m_writeableStateTimeout = 30000;

        public NetworkBinaryReader m_reader;
        public ITcpClient m_socket;
        public NetworkBinaryWriter m_writer;
        private readonly object _semaphore = new object();
        private bool _closed;

        public SocketFrameHandler(AmqpTcpEndpoint endpoint,
            Func<AddressFamily, ITcpClient> socketFactory,
            int connectionTimeout, int readTimeout, int writeTimeout)
        {
            Endpoint = endpoint;
            m_socket = null;
            if (Socket.OSSupportsIPv6 && endpoint.AddressFamily != AddressFamily.InterNetwork)
            {
                try
                {
                    m_socket = socketFactory(AddressFamily.InterNetworkV6);
                    Connect(m_socket, endpoint, connectionTimeout);
                }
                catch (ConnectFailureException) // could not connect using IPv6
                {
                    m_socket = null;
                }
                // Mono might raise a SocketException when using IPv4 addresses on
                // an OS that supports IPv6
                catch (SocketException)
                {
                    m_socket = null;
                }
            }
            if (m_socket == null && endpoint.AddressFamily != AddressFamily.InterNetworkV6)
            {
                m_socket = socketFactory(AddressFamily.InterNetwork);
                Connect(m_socket, endpoint, connectionTimeout);
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
            m_reader = new NetworkBinaryReader(new BufferedStream(netstream));
            m_writer = new NetworkBinaryWriter(new BufferedStream(netstream));

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
#pragma warning disable 0168
                catch (SocketException _)
                {
                    // means that the socket is already closed
                }
#pragma warning restore 0168
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
                        try
                        {

                        } catch (ArgumentException)
                        {
                            // ignore, we are closing anyway
                        };
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

        public Frame ReadFrame()
        {
            lock (m_reader)
            {
                return Frame.ReadFrom(m_reader);
            }
        }

        public void SendHeader()
        {
            lock (m_writer)
            {
                m_writer.Write(Encoding.ASCII.GetBytes("AMQP"));
                if (Endpoint.Protocol.Revision != 0)
                {
                    m_writer.Write((byte)0);
                    m_writer.Write((byte)Endpoint.Protocol.MajorVersion);
                    m_writer.Write((byte)Endpoint.Protocol.MinorVersion);
                    m_writer.Write((byte)Endpoint.Protocol.Revision);
                }
                else
                {
                    m_writer.Write((byte)1);
                    m_writer.Write((byte)1);
                    m_writer.Write((byte)Endpoint.Protocol.MajorVersion);
                    m_writer.Write((byte)Endpoint.Protocol.MinorVersion);
                }
                m_writer.Flush();
            }
        }

        public void WriteFrame(Frame frame)
        {
            lock (m_writer)
            {
                m_socket.Client.Poll(m_writeableStateTimeout, SelectMode.SelectWrite);
                frame.WriteTo(m_writer);
                m_writer.Flush();
            }
        }

        public void WriteFrameSet(IList<Frame> frames)
        {
            lock (m_writer)
            {
                m_socket.Client.Poll(m_writeableStateTimeout, SelectMode.SelectWrite);
                foreach(var f in frames)
                {
                    f.WriteTo(m_writer);
                }
                m_writer.Flush();
            }
        }

        public void Flush()
        {
            lock (m_writer)
            {
                m_writer.Flush();
            }
        }

        private void Connect(ITcpClient socket, AmqpTcpEndpoint endpoint, int timeout)
        {
            try
            {
                socket.ConnectAsync(endpoint.HostName, endpoint.Port)
                            .TimeoutAfter(timeout)
                            .ConfigureAwait(false)
                            .GetAwaiter()//this ensures exceptions aren't wrapped in an AggregateException
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
#endif
