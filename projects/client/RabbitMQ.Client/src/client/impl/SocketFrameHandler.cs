// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2014 GoPivotal, Inc.
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
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace RabbitMQ.Client.Impl
{
    public class SocketFrameHandler : IFrameHandler
    {
        // ^^ System.Net.Sockets.SocketError doesn't exist in .NET 1.1

        // Timeout in seconds to wait for a clean socket close.
        public const int SOCKET_CLOSING_TIMEOUT = 1;

        public const int WSAEWOULDBLOCK = 10035;

        public NetworkBinaryReader m_reader;
        public TcpClient m_socket;
        public NetworkBinaryWriter m_writer;
        private readonly object _semaphore = new object();
        private bool _closed;

        public SocketFrameHandler(AmqpTcpEndpoint endpoint,
            Func<AddressFamily, TcpClient> socketFactory,
            int timeout)
        {
            Endpoint = endpoint;
            m_socket = null;
            if (Socket.OSSupportsIPv6)
            {
                try
                {
                    m_socket = socketFactory(AddressFamily.InterNetworkV6);
                    Connect(m_socket, endpoint, timeout);
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
            if (m_socket == null)
            {
                m_socket = socketFactory(AddressFamily.InterNetwork);
                Connect(m_socket, endpoint, timeout);
            }

            Stream netstream = m_socket.GetStream();
            // make sure the socket timeout is greater than heartbeat timeout
            netstream.ReadTimeout = timeout * 4;
            netstream.WriteTimeout = timeout * 4;

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

        public int Timeout
        {
            set
            {
                try
                {
                    if (m_socket.Connected)
                    {
                        // make sure the socket timeout is greater than heartbeat interval
                        m_socket.ReceiveTimeout = value * 4;
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
                            
                        } catch (ArgumentException _)
                        {
                            // ignore, we are closing anyway
                        };
                        m_socket.Close();
                    }
                    catch (Exception _)
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
                frame.WriteTo(m_writer);
                m_writer.Flush();
            }
        }

        private void Connect(TcpClient socket, AmqpTcpEndpoint endpoint, int timeout)
        {
            IAsyncResult ar = null;
            try
            {
                ar = socket.BeginConnect(endpoint.HostName, endpoint.Port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(timeout, false))
                {
                    socket.Close();
                    throw new TimeoutException("Connection to " + endpoint + " timed out");
                }
                socket.EndConnect(ar);
            }
            catch (ArgumentException e)
            {
                throw new ConnectFailureException("Connection failed", e);
            }
            catch (SocketException e)
            {
                throw new ConnectFailureException("Connection failed", e);
            }
            finally
            {
                if (ar != null)
                {
                    ar.AsyncWaitHandle.Close();
                }
            }
        }
    }
}