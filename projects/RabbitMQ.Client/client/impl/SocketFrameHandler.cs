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
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.Impl
{
    internal static class TaskExtensions
    {
        public static async Task TimeoutAfter(this Task task, TimeSpan timeout)
        {
#if NET6_0_OR_GREATER
            await task.WaitAsync(timeout).ConfigureAwait(false);
#else
            if (task == await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false))
            {
                await task.ConfigureAwait(false);
            }
            else
            {
                Task supressErrorTask = task.ContinueWith((t, s) => t.Exception.Handle(e => true), null, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                throw new TimeoutException();
            }
#endif
        }
    }

    internal sealed class SocketFrameHandler : IFrameHandler
    {
        private readonly AmqpTcpEndpoint _amqpTcpEndpoint;
        private readonly ITcpClient _socket;
        private readonly ChannelWriter<ReadOnlyMemory<byte>> _channelWriter;
        private readonly ChannelReader<ReadOnlyMemory<byte>> _channelReader;
        private readonly PipeWriter _pipeWriter;
        private readonly PipeReader _pipeReader;
        private readonly Task _writerTask;
        private readonly object _semaphore = new object();
        private bool _closed;
        private static ReadOnlySpan<byte> ProtocolHeader => new byte[] { (byte)'A', (byte)'M', (byte)'Q', (byte)'P', 0, 0, 9, 1 };

        public SocketFrameHandler(AmqpTcpEndpoint endpoint,
            Func<AddressFamily, ITcpClient> socketFactory,
            TimeSpan connectionTimeout, TimeSpan readTimeout, TimeSpan writeTimeout)
        {
            _amqpTcpEndpoint = endpoint;
            var channel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(
                new UnboundedChannelOptions
                {
                    AllowSynchronousContinuations = false,
                    SingleReader = true,
                    SingleWriter = false
                });

            _channelReader = channel.Reader;
            _channelWriter = channel.Writer;

            // Resolve the hostname to know if it's even possible to even try IPv6
            IPAddress[] adds = Dns.GetHostAddresses(endpoint.HostName);
            IPAddress ipv6 = TcpClientAdapterHelper.GetMatchingHost(adds, AddressFamily.InterNetworkV6);

            if (ipv6 == default(IPAddress))
            {
                if (endpoint.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    throw new ConnectFailureException("Connection failed", new ArgumentException($"No IPv6 address could be resolved for {endpoint.HostName}"));
                }
            }
            else if (ShouldTryIPv6(endpoint))
            {
                try
                {
                    _socket = ConnectUsingIPv6(new IPEndPoint(ipv6, endpoint.Port), socketFactory, connectionTimeout);
                }
                catch (ConnectFailureException)
                {
                    // We resolved to a ipv6 address and tried it but it still didn't connect, try IPv4
                    _socket = null;
                }
            }

            if (_socket is null)
            {
                IPAddress ipv4 = TcpClientAdapterHelper.GetMatchingHost(adds, AddressFamily.InterNetwork);
                if (ipv4 == default(IPAddress))
                {
                    throw new ConnectFailureException("Connection failed", new ArgumentException($"No ip address could be resolved for {endpoint.HostName}"));
                }
                _socket = ConnectUsingIPv4(new IPEndPoint(ipv4, endpoint.Port), socketFactory, connectionTimeout);
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

            _pipeWriter = PipeWriter.Create(netstream);
            _pipeReader = PipeReader.Create(netstream);

            WriteTimeout = writeTimeout;
            _writerTask = Task.Run(WriteLoop);
        }

        public AmqpTcpEndpoint Endpoint
        {
            get { return _amqpTcpEndpoint; }
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
                _socket.Client.SendTimeout = (int)value.TotalMilliseconds;
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

        public ValueTask<InboundFrame> ReadFrameAsync()
        {
            return InboundFrame.ReadFromPipeAsync(_pipeReader, _amqpTcpEndpoint.MaxMessageSize);
        }

        public bool TryReadFrame(out InboundFrame frame)
        {
            return InboundFrame.TryReadFrameFromPipe(_pipeReader, _amqpTcpEndpoint.MaxMessageSize, out frame);
        }

        public void SendHeader()
        {
#if NET
            _pipeWriter.AsStream().Write(ProtocolHeader);
#else
            _pipeWriter.AsStream().Write(ProtocolHeader.ToArray(), 0, ProtocolHeader.Length);
#endif
        }

        public void Write(ReadOnlyMemory<byte> memory)
        {
            if (_closed)
            {
                return;

            }

            if (!_channelWriter.TryWrite(memory))
            {
                throw new ApplicationException("Please report this bug here: https://github.com/rabbitmq/rabbitmq-dotnet-client/issues");
            }
        }

        private async Task WriteLoop()
        {
            try
            {
                while (await _channelReader.WaitToReadAsync().ConfigureAwait(false))
                {
                    while (_channelReader.TryRead(out ReadOnlyMemory<byte> memory))
                    {
                        MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment);
                        _pipeWriter.Write(memory.Span);
                        RabbitMqClientEventSource.Log.CommandSent(segment.Count);
                        ArrayPool<byte>.Shared.Return(segment.Array);
                    }

                    await _pipeWriter.FlushAsync()
                       .ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                ESLog.Error("Background socket write loop has crashed", ex);
                throw;
            }
        }

        private static bool ShouldTryIPv6(AmqpTcpEndpoint endpoint)
        {
            return Socket.OSSupportsIPv6 && endpoint.AddressFamily != AddressFamily.InterNetwork;
        }

        private ITcpClient ConnectUsingIPv6(IPEndPoint endpoint,
                                            Func<AddressFamily, ITcpClient> socketFactory,
                                            TimeSpan timeout)
        {
            return ConnectUsingAddressFamily(endpoint, socketFactory, timeout, AddressFamily.InterNetworkV6);
        }

        private ITcpClient ConnectUsingIPv4(IPEndPoint endpoint,
                                            Func<AddressFamily, ITcpClient> socketFactory,
                                            TimeSpan timeout)
        {
            return ConnectUsingAddressFamily(endpoint, socketFactory, timeout, AddressFamily.InterNetwork);
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
