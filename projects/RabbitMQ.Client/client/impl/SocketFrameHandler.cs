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
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.Impl
{
    internal sealed class SocketFrameHandler : IFrameHandler
    {
        private readonly AmqpTcpEndpoint _amqpTcpEndpoint;
        private readonly Func<AddressFamily, ITcpClient> _socketFactory;
        private readonly TimeSpan _connectionTimeout;

        private readonly ChannelWriter<RentedOutgoingMemory> _channelWriter;
        private readonly ChannelReader<RentedOutgoingMemory> _channelReader;
        private readonly SemaphoreSlim _closingSemaphore = new SemaphoreSlim(1, 1);

        private IPAddress[] _amqpTcpEndpointAddresses;
        private PipeWriter _pipeWriter;
        private PipeReader _pipeReader;
        private Task _writerTask;
        private ITcpClient _socket;

        private TimeSpan _readTimeout;
        private TimeSpan _writeTimeout;

        private bool _connected;
        private bool _closed;

        private static ReadOnlyMemory<byte> Amqp091ProtocolHeader => new byte[] { (byte)'A', (byte)'M', (byte)'Q', (byte)'P', 0, 0, 9, 1 };

        public SocketFrameHandler(AmqpTcpEndpoint amqpTcpEndpoint,
            Func<AddressFamily, ITcpClient> socketFactory,
            TimeSpan connectionTimeout, TimeSpan readTimeout, TimeSpan writeTimeout)
        {
            _amqpTcpEndpoint = amqpTcpEndpoint;
            _socketFactory = socketFactory;
            _connectionTimeout = connectionTimeout;
            _readTimeout = readTimeout;
            _writeTimeout = writeTimeout;

            var channel = Channel.CreateBounded<RentedOutgoingMemory>(
                new BoundedChannelOptions(128)
                {
                    AllowSynchronousContinuations = false,
                    SingleReader = true,
                    SingleWriter = false
                });

            _channelWriter = channel.Writer;
            _channelReader = channel.Reader;
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
                    _readTimeout = value;
                    SetSocketReceiveTimeout();
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
                _writeTimeout = value;
                SetSocketSendTimout();
            }
        }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (_connected)
            {
                if (_socket is null)
                {
                    throw new InvalidOperationException();
                }

                if (false == _socket.Connected)
                {
                    throw new InvalidOperationException();
                }

                return;
            }

#if NET6_0_OR_GREATER
            _amqpTcpEndpointAddresses = await Dns.GetHostAddressesAsync(_amqpTcpEndpoint.HostName, cancellationToken)
                .ConfigureAwait(false);
#else
            _amqpTcpEndpointAddresses = await Dns.GetHostAddressesAsync(_amqpTcpEndpoint.HostName)
                .ConfigureAwait(false);
#endif
            IPAddress ipv6 = TcpClientAdapter.GetMatchingHost(_amqpTcpEndpointAddresses, AddressFamily.InterNetworkV6);

            if (ipv6 == default(IPAddress))
            {
                if (_amqpTcpEndpoint.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    throw new ConnectFailureException($"Connection failed, host {_amqpTcpEndpoint}",
                        new ArgumentException($"No IPv6 address could be resolved for {_amqpTcpEndpoint}"));
                }
            }
            else if (ShouldTryIPv6(_amqpTcpEndpoint))
            {
                try
                {
                    var ipep = new IPEndPoint(ipv6, _amqpTcpEndpoint.Port);
                    _socket = await ConnectUsingIPv6Async(ipep, _socketFactory, _connectionTimeout, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (ConnectFailureException)
                {
                    // We resolved to a ipv6 address and tried it but it still didn't connect, try IPv4
                    _socket = null;
                }
            }

            if (_socket is null)
            {
                IPAddress ipv4 = TcpClientAdapter.GetMatchingHost(_amqpTcpEndpointAddresses, AddressFamily.InterNetwork);
                if (ipv4 == default(IPAddress))
                {
                    throw new ConnectFailureException($"Connection failed, host {_amqpTcpEndpoint}",
                        new ArgumentException($"No ip address could be resolved for {_amqpTcpEndpoint}"));
                }
                var ipep = new IPEndPoint(ipv4, _amqpTcpEndpoint.Port);
                _socket = await ConnectUsingIPv4Async(ipep, _socketFactory, _connectionTimeout, cancellationToken)
                    .ConfigureAwait(false);
            }

            SetSocketReceiveTimeout();
            SetSocketSendTimout();

            Stream netstream = _socket.GetStream();
            netstream.ReadTimeout = (int)_readTimeout.TotalMilliseconds;
            netstream.WriteTimeout = (int)_writeTimeout.TotalMilliseconds;

            if (_amqpTcpEndpoint.Ssl.Enabled)
            {
                try
                {
                    netstream = await SslHelper.TcpUpgradeAsync(netstream, _amqpTcpEndpoint.Ssl, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch
                {
                    await CloseAsync()
                        .ConfigureAwait(false);
                    throw;
                }
            }

            _pipeWriter = PipeWriter.Create(netstream);
            _pipeReader = PipeReader.Create(netstream);

            _writerTask = Task.Run(WriteLoop, cancellationToken);
            _connected = true;
        }

        public void Close()
        {
            CloseAsync().EnsureCompleted();
        }

        public async Task CloseAsync()
        {
            if (_closed || _socket == null)
            {
                return;
            }

            await _closingSemaphore.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                try
                {
                    _channelWriter.Complete();
                    if (_writerTask is not null)
                    {
                        await _writerTask.ConfigureAwait(false);
                    }
                    await _pipeWriter.CompleteAsync()
                        .ConfigureAwait(false);
                    await _pipeReader.CompleteAsync()
                        .ConfigureAwait(false);
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
            }
            finally
            {
                _closingSemaphore.Release();
                _closed = true;
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

        public async Task SendProtocolHeaderAsync(CancellationToken cancellationToken)
        {
            await _pipeWriter.WriteAsync(Amqp091ProtocolHeader, cancellationToken)
                .ConfigureAwait(false);
            await _pipeWriter.FlushAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async ValueTask WriteAsync(RentedOutgoingMemory frames)
        {
            if (_closed)
            {
                frames.Dispose();
                await Task.Yield();
            }
            else
            {
                await _channelWriter.WriteAsync(frames)
                    .ConfigureAwait(false);

                bool didSend = await frames.WaitForDataSendAsync()
                    .ConfigureAwait(false);

                if (didSend)
                {
                    frames.Dispose();
                }
            }
        }

        private void SetSocketReceiveTimeout()
        {
            if (_socket != null && _socket.Connected)
            {
                _socket.ReceiveTimeout = _readTimeout;
            }
        }

        private void SetSocketSendTimout()
        {
            if (_socket != null)
            {
                _socket.Client.SendTimeout = (int)_writeTimeout.TotalMilliseconds;
            }
        }

        private async Task WriteLoop()
        {
            try
            {
                while (await _channelReader.WaitToReadAsync().ConfigureAwait(false))
                {
                    while (_channelReader.TryRead(out RentedOutgoingMemory frames))
                    {
                        try
                        {
                            frames.WriteTo(_pipeWriter);
                            await _pipeWriter.FlushAsync()
                                .ConfigureAwait(false);
                            RabbitMqClientEventSource.Log.CommandSent(frames.Size);
                        }
                        finally
                        {
                            if (frames.DidSend())
                            {
                                frames.Dispose();
                            }
                        }
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

        private static ValueTask<ITcpClient> ConnectUsingIPv6Async(IPEndPoint endpoint,
            Func<AddressFamily, ITcpClient> socketFactory,
            TimeSpan connectionTimeout, CancellationToken cancellationToken)
        {
            return ConnectUsingAddressFamilyAsync(endpoint, socketFactory, AddressFamily.InterNetworkV6,
                connectionTimeout, cancellationToken);
        }

        private static ValueTask<ITcpClient> ConnectUsingIPv4Async(IPEndPoint endpoint,
            Func<AddressFamily, ITcpClient> socketFactory,
            TimeSpan connectionTimeout, CancellationToken cancellationToken)
        {
            return ConnectUsingAddressFamilyAsync(endpoint, socketFactory, AddressFamily.InterNetwork,
                connectionTimeout, cancellationToken);
        }

        private static async ValueTask<ITcpClient> ConnectUsingAddressFamilyAsync(IPEndPoint endpoint,
            Func<AddressFamily, ITcpClient> socketFactory, AddressFamily family,
            TimeSpan connectionTimeout, CancellationToken cancellationToken)
        {
            ITcpClient socket = socketFactory(family);
            try
            {
                await ConnectOrFailAsync(socket, endpoint, connectionTimeout, cancellationToken)
                    .ConfigureAwait(false);
                return socket;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        private static async Task ConnectOrFailAsync(ITcpClient tcpClient, IPEndPoint endpoint,
            TimeSpan connectionTimeout, CancellationToken externalCancellationToken)
        {
            string msg = $"Connection failed, host {endpoint}";

            /*
             * Create linked cancellation token that incldes the connection timeout value
             * https://learn.microsoft.com/en-us/dotnet/standard/threading/how-to-listen-for-multiple-cancellation-requests
             */
            using var timeoutTokenSource = new CancellationTokenSource(connectionTimeout);
            CancellationToken timeoutToken = timeoutTokenSource.Token;
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutToken, externalCancellationToken);

            try
            {
                await tcpClient.ConnectAsync(endpoint.Address, endpoint.Port, linkedTokenSource.Token)
                    .ConfigureAwait(false);
            }
            catch (ArgumentException e)
            {
                throw new ConnectFailureException(msg, e);
            }
            catch (SocketException e)
            {
                throw new ConnectFailureException(msg, e);
            }
            catch (NotSupportedException e)
            {
                throw new ConnectFailureException(msg, e);
            }
            catch (OperationCanceledException e)
            {
                if (timeoutToken.IsCancellationRequested)
                {
                    var timeoutException = new TimeoutException(msg, e);
                    throw new ConnectFailureException(msg, timeoutException);
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
