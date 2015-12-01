// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2015 Pivotal Software, Inc.
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
//  Copyright (c) 2007-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.Sockets;

namespace RabbitMQ.Client.Impl
{
    /// <summary>
    /// Implement <see cref="IFrameHandler"/> for WinRT.  The significant
    /// difference is that TcpClient is not available and the new
    /// <see cref="StreamSocket"/> needs to be used.
    /// </summary>
    public class SocketFrameHandler : IFrameHandler
    {
        // Timeout in seconds to wait for a clean socket close.
        public const int SOCKET_CLOSING_TIMEOUT = 1;

        public StreamSocket m_socket;
        public NetworkBinaryReader m_reader;
        public NetworkBinaryWriter m_writer;
        private bool _closed = false;
        private readonly object _semaphore = new object();

        private int? defaultTimeout;

        public SocketFrameHandler(AmqpTcpEndpoint endpoint,
            Func<StreamSocket> socketFactory,
            int connectionTimeout,
            int _readTimeout,
            int _writeTimeout)
        {
            Endpoint = endpoint;

            m_socket = socketFactory();
            Connect(m_socket, endpoint, connectionTimeout);

            if (endpoint.Ssl.Enabled)
            {
                IAsyncAction ar = null;
                try
                {
                    var cts = new CancellationTokenSource();
                    if (this.defaultTimeout.HasValue)
                        cts.CancelAfter(this.defaultTimeout.Value);

                    ar = this.m_socket.UpgradeToSslAsync(
                        SocketProtectionLevel.Ssl, new HostName(endpoint.Ssl.ServerName));
                    ar.AsTask(cts.Token).Wait();
                    ar.GetResults();
                }
                catch (Exception)
                {
                    Close();
                    throw;
                }
                finally
                {
                    if (ar != null)
                        ar.Close();
                }
            }
            m_reader = new NetworkBinaryReader(m_socket.InputStream.AsStreamForRead());
            m_writer = new NetworkBinaryWriter(m_socket.OutputStream.AsStreamForWrite());
        }

        public AmqpTcpEndpoint Endpoint { get; set; }

        public int LocalPort
        {
            get
            {
                // the port string may be empty, so TryParse will set to 0;
                int port;
                Int32.TryParse(m_socket.Information.LocalPort, out port);
                return port;
            }
        }

        public int RemotePort
        {
            get
            {
                // the port string may be empty, so TryParse will set to 0;
                int port;
                Int32.TryParse(m_socket.Information.RemotePort, out port);
                return port;
            }
        }

        public int ReadTimeout
        {
            set
            {
                // Ignored, timeouts over streams on WinRT
                // are much trickier to get right.
                //
                // See heartbeats implementation is in Connection.
            }
        }

        public int WriteTimeout
        {
            set
            {
                // Ignored, timeouts over streams on WinRT
                // are much trickier to get right.
            }
        }

        public void Close()
        {
            lock (_semaphore)
            {
                if (!_closed)
                {
                    // TODO: Figure out the equivalent for LingerState
                    //m_socket.LingerState = new LingerOption(true, SOCKET_CLOSING_TIMEOUT);
                    m_socket.Dispose();
                    _closed = true;
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
                m_writer.Write(Encoding.UTF8.GetBytes("AMQP"));
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

        public void WriteFrameSet(IList<Frame> frames)
        {
            lock (m_writer)
            {
                foreach (var f in frames)
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

        private void Connect(StreamSocket socket, AmqpTcpEndpoint endpoint, int timeout)
        {
            IAsyncAction ar = null;
            try
            {
                var cts = new CancellationTokenSource();
                if (this.defaultTimeout.HasValue)
                    cts.CancelAfter(this.defaultTimeout.Value);

                ar = socket.ConnectAsync(new HostName(endpoint.HostName), endpoint.Port.ToString(), SocketProtectionLevel.PlainSocket);
                if (!ar.AsTask(cts.Token).Wait(timeout))
                {
                    socket.Dispose();
                    throw new TimeoutException("Connection to " + endpoint + " timed out");
                }
                ar.GetResults();
            }
            catch (ArgumentException e)
            {
                throw new ConnectFailureException("Connection failed", e);
            }
            catch (Exception e)
            {
                // If this is an unknown status it means that the error is fatal and retry will likely fail.
                if (SocketError.GetStatus(e.HResult) == SocketErrorStatus.Unknown)
                {
                    throw;
                }

                throw new ConnectFailureException("Connection failed", e);
            }
            finally
            {
                if (ar != null)
                {
                    ar.Close();
                }
            }
        }
    }
}