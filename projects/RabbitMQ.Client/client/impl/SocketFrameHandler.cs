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
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using RabbitMQ.Client.Exceptions;

namespace RabbitMQ.Client.Impl
{
    internal static class TaskExtensions
    {
        public static async Task TimeoutAfter(this Task task, TimeSpan timeout)
        {
            if (task == await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false))
            {
                await task;
            }
            else
            {
                Task supressErrorTask = task.ContinueWith(t => t.Exception.Handle(e => true), TaskContinuationOptions.OnlyOnFaulted);
                throw new TimeoutException();
            }
        }
    }

    class SocketFrameHandler : IFrameHandler
    {
        private readonly AmqpTcpEndpoint _endpoint;
        // Socket poll timeout in ms. If the socket does not
        // become writeable in this amount of time, we throw
        // an exception.
        private TimeSpan _writeableStateTimeout = TimeSpan.FromSeconds(30);
        private int _writeableStateTimeoutMicroSeconds;
        private readonly ITcpClient _socket;
        private readonly Stream _reader;
        private readonly Stream _writer;
        private readonly ChannelWriter<ReadOnlyMemory<byte>> _channelWriter;
        private readonly ChannelReader<ReadOnlyMemory<byte>> _channelReader;
        private readonly Task _writerTask;
        private readonly object _semaphore = new object();
        private readonly byte[] _frameHeaderBuffer;
        private bool _closed;
        private ArrayPool<byte> _pool = ArrayPool<byte>.Shared;
        private readonly bool _enableSynchronousWriteLoop;

        public SocketFrameHandler(AmqpTcpEndpoint endpoint,
            Func<AddressFamily, ITcpClient> socketFactory,
            TimeSpan connectionTimeout, TimeSpan readTimeout, TimeSpan writeTimeout, bool enableSynchronousWriteLoop)
        {
            _endpoint = endpoint;
            _enableSynchronousWriteLoop = enableSynchronousWriteLoop;
            _frameHeaderBuffer = new byte[6];
            var channel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(
                new UnboundedChannelOptions
                {
                    AllowSynchronousContinuations = false,
                    SingleReader = true,
                    SingleWriter = false
                });

            _channelReader = channel.Reader;
            _channelWriter = channel.Writer;

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

            Stream netstream = _socket.GetStream();
            netstream.ReadTimeout = (int)readTimeout.TotalMilliseconds;
            netstream.WriteTimeout = (int)writeTimeout.TotalMilliseconds;

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

            _reader = new BufferedStream(netstream, _socket.Client.ReceiveBufferSize);
            _writer = new BufferedStream(netstream, _socket.Client.SendBufferSize);

            WriteTimeout = writeTimeout;
            if (_enableSynchronousWriteLoop)
            {
                TaskCreationOptions tco = TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach;
                _writerTask = Task.Factory.StartNew(SynchronousWriteLoop, CancellationToken.None, tco, TaskScheduler.Default);
            }
            else
            {
                _writerTask = Task.Run(WriteLoop, CancellationToken.None);
            }
        }

        public AmqpTcpEndpoint Endpoint
        {
            get { return _endpoint; }
        }

        internal ArrayPool<byte> MemoryPool
        {
            get => _pool;
            set => _pool = value ?? ArrayPool<byte>.Shared; // TODO: Change to init accessor
        }

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
            get { return ((IPEndPoint)RemoteEndPoint).Port; }
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
            set
            {
                _writeableStateTimeout = value;
                _socket.Client.SendTimeout = (int)_writeableStateTimeout.TotalMilliseconds;
                _writeableStateTimeoutMicroSeconds = _socket.Client.SendTimeout * 1000;
            }
        }

        public void Close()
        {
            lock (_semaphore)
            {
                if (_closed || _socket == null)
                {
                    return;
                }
                else
                {
                    try
                    {
                        _channelWriter.Complete();
                        _writerTask?.GetAwaiter().GetResult();
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
                        _closed = true;
                    }
                }
            }
        }

        public InboundFrame ReadFrame()
        {
            return InboundFrame.ReadFrom(_reader, _frameHeaderBuffer, MemoryPool, _endpoint.MaxMessageSize);
        }

        public void SendHeader()
        {
            byte[] headerBytes = new byte[8];
            Encoding.ASCII.GetBytes("AMQP", 0, 4, headerBytes, 0);
            if (Endpoint.Protocol.Revision != 0)
            {
                headerBytes[4] = 0;
                headerBytes[5] = (byte)Endpoint.Protocol.MajorVersion;
                headerBytes[6] = (byte)Endpoint.Protocol.MinorVersion;
                headerBytes[7] = (byte)Endpoint.Protocol.Revision;
            }
            else
            {
                headerBytes[4] = 1;
                headerBytes[5] = 1;
                headerBytes[6] = (byte)Endpoint.Protocol.MajorVersion;
                headerBytes[7] = (byte)Endpoint.Protocol.MinorVersion;
            }

            _writer.Write(headerBytes, 0, 8);
            _writer.Flush();
        }

        public void Write(ReadOnlyMemory<byte> memory)
        {
            _channelWriter.TryWrite(memory);
        }

        private async Task WriteLoop()
        {
            while (await _channelReader.WaitToReadAsync().ConfigureAwait(false))
            {
                _socket.Client.Poll(_writeableStateTimeoutMicroSeconds, SelectMode.SelectWrite);
                while (_channelReader.TryRead(out ReadOnlyMemory<byte> memory))
                {
                    if (MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment))
                    {
                        if (segment.Array != null)
                        {
                            await _writer.WriteAsync(segment.Array, segment.Offset, segment.Count).ConfigureAwait(false);
                            MemoryPool.Return(segment.Array);
                        }
                    }
                }
                await _writer.FlushAsync().ConfigureAwait(false);
            }
        }

        private void SynchronousWriteLoop()
        {
            while (_channelReader.WaitToReadAsync().AsTask().Result)
            {
                _socket.Client.Poll(_writeableStateTimeoutMicroSeconds, SelectMode.SelectWrite);
                while (_channelReader.TryRead(out ReadOnlyMemory<byte> memory))
                {
                    if (MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment))
                    {
                        if (segment.Array != null)
                        {
                            _writer.Write(segment.Array, segment.Offset, segment.Count);
                            MemoryPool.Return(segment.Array);
                        }
                    }
                }
                _writer.Flush();
            }
        }

        private static bool ShouldTryIPv6(AmqpTcpEndpoint endpoint)
        {
            return Socket.OSSupportsIPv6 && endpoint.AddressFamily != AddressFamily.InterNetwork;
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
            catch (ConnectFailureException)
            {
                socket.Dispose();
                throw;
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
