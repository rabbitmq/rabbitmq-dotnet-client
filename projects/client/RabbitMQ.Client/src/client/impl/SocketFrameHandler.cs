// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
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
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
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

        public static async Task TimeoutAfter(this Task task, TimeSpan timeout)
        {
            if (task == await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false))
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
        // Socket poll timeout in ms. If the socket does not
        // become writeable in this amount of time, we throw
        // an exception.
        private TimeSpan m_writeableStateTimeout = TimeSpan.FromSeconds(30);
        private readonly ITcpClient _socket;
        private readonly IDuplexPipe _duplexPipe;
        private readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1);
        private readonly PipelineBinaryReader _reader;
        private readonly PipelineBinaryWriter _writer;
        private readonly CancellationTokenSource _connectionCancellation = new CancellationTokenSource();

        public SocketFrameHandler(AmqpTcpEndpoint endpoint,
            Func<AddressFamily, ITcpClient> socketFactory,
            TimeSpan connectionTimeout, TimeSpan readTimeout, TimeSpan writeTimeout)
        {
            Endpoint = endpoint;

            if (ShouldTryIPv6(endpoint))
            {
                try
                {
                    _socket = ConnectUsingIPv6(endpoint, socketFactory, connectionTimeout);
                }
                catch (ConnectFailureException)
                {
                    _socket = null;
                }
            }

            if (_socket == null && endpoint.AddressFamily != AddressFamily.InterNetworkV6)
            {
                _socket = ConnectUsingIPv4(endpoint, socketFactory, connectionTimeout);
            }

            if (endpoint.Ssl.Enabled)
            {
                try
                {
                    Stream netstream = _socket.GetStream();
                    netstream.ReadTimeout = (int)readTimeout.TotalMilliseconds;
                    netstream.WriteTimeout = (int)writeTimeout.TotalMilliseconds;
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
                _duplexPipe = SocketConnection.Create(_socket.Client);
            }

            _reader = new PipelineBinaryReader(_duplexPipe.Input);
            _writer = new PipelineBinaryWriter(_duplexPipe.Output);
            WriteTimeout = writeTimeout;
        }

        public AmqpTcpEndpoint Endpoint { get; set; }

        public EndPoint LocalEndPoint
        {
            get { return _socket.Client.LocalEndPoint; }
        }

        public int LocalPort
        {
            get { return ((IPEndPoint)LocalEndPoint).Port; }
        }

        public EndPoint RemoteEndPoint
        {
            get { return _socket.Client.RemoteEndPoint; }
        }

        public int RemotePort
        {
            get { return ((IPEndPoint)LocalEndPoint).Port; }
        }

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
            get => m_writeableStateTimeout;
            set
            {
                m_writeableStateTimeout = value;
                _socket.Client.SendTimeout = (int)m_writeableStateTimeout.TotalMilliseconds;
            }
        }

        public void Close()
        {
            if (!_connectionCancellation.IsCancellationRequested)
            {
                _connectionCancellation.Cancel();
            }

            _socket.Close();
            _connectionCancellation.Dispose();
        }

        public ValueTask<InboundFrame> ReadFrameAsync()
        {
            return InboundFrame.ReadFromAsync(_reader, _connectionCancellation.Token);
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
            _writer.Write(amqp);
            _writer.Write(versionArray, 0, 4);
            _writer.Flush();
            ArrayPool<byte>.Shared.Return(versionArray);
        }

        public void WriteFrame(OutboundFrame frame)
        {
            _writeSemaphore.Wait();
            frame.WriteTo(_writer);
            _writer.Flush();
            _writeSemaphore.Release();
        }

        public void WriteFrameSet(IList<OutboundFrame> frames)
        {
            _writeSemaphore.Wait();
            foreach (OutboundFrame frame in frames)
            {
                frame.WriteTo(_writer);
            }
            _writer.Flush();
            _writeSemaphore.Release();
        }

        private bool ShouldTryIPv6(AmqpTcpEndpoint endpoint)
        {
            return (Socket.OSSupportsIPv6 && endpoint.AddressFamily != AddressFamily.InterNetwork);
        }

        private ITcpClient ConnectUsingIPv6(AmqpTcpEndpoint endpoint,
                                            Func<AddressFamily, ITcpClient> socketFactory,
                                            TimeSpan timeout)
        {
            return ConnectUsingAddressFamily(endpoint, socketFactory, timeout, AddressFamily.InterNetworkV6);
        }

        private ITcpClient ConnectUsingIPv4(AmqpTcpEndpoint endpoint,
                                            Func<AddressFamily, ITcpClient> socketFactory,
                                            TimeSpan timeout)
        {
            return ConnectUsingAddressFamily(endpoint, socketFactory, timeout, AddressFamily.InterNetwork);
        }

        private ITcpClient ConnectUsingAddressFamily(AmqpTcpEndpoint endpoint,
                                                    Func<AddressFamily, ITcpClient> socketFactory,
                                                    TimeSpan timeout, AddressFamily family)
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

        private void ConnectOrFail(ITcpClient socket, AmqpTcpEndpoint endpoint, TimeSpan timeout)
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
    }
}
