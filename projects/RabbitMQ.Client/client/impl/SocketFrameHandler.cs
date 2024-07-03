// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using RabbitMQ.Client.client.impl;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.Impl
{
    internal sealed class SocketFrameHandler : IFrameHandler
    {
        private readonly AmqpTcpEndpoint _amqpTcpEndpoint;
        private readonly ITcpClient _socket;
        private readonly Stream _stream;

        private readonly ChannelWriter<RentedMemory> _channelWriter;
        private readonly ChannelReader<RentedMemory> _channelReader;
        private readonly SemaphoreSlim _closingSemaphore = new SemaphoreSlim(1, 1);

        private readonly PipeWriter _pipeWriter;
        private readonly PipeReader _pipeReader;
        private Task? _writerTask;

        private bool _closed;

        private static ReadOnlyMemory<byte> Amqp091ProtocolHeader => new byte[] { (byte)'A', (byte)'M', (byte)'Q', (byte)'P', 0, 0, 9, 1 };

        private SocketFrameHandler(AmqpTcpEndpoint amqpTcpEndpoint, ITcpClient socket, Stream stream)
        {
            _amqpTcpEndpoint = amqpTcpEndpoint;
            _socket = socket;
            _stream = stream;

            var channel = Channel.CreateBounded<RentedMemory>(
                new BoundedChannelOptions(128)
                {
                    AllowSynchronousContinuations = false,
                    SingleReader = true,
                    SingleWriter = false
                });

            _channelWriter = channel.Writer;
            _channelReader = channel.Reader;

            _pipeWriter = PipeWriter.Create(stream);
            _pipeReader = PipeReader.Create(stream);
        }

        public AmqpTcpEndpoint Endpoint
        {
            get { return _amqpTcpEndpoint; }
        }

        public EndPoint LocalEndPoint
        {
            get { return _socket.Client.LocalEndPoint!; }
        }

        public int LocalPort
        {
            get { return ((IPEndPoint)LocalEndPoint).Port; }
        }

        public EndPoint RemoteEndPoint
        {
            get { return _socket.Client.RemoteEndPoint!; }
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
                    _socket.ReceiveTimeout = value;
                    _stream.ReadTimeout = (int)value.TotalMilliseconds;
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
                _stream.WriteTimeout = (int)value.TotalMilliseconds;
            }
        }

        public static async Task<SocketFrameHandler> CreateAsync(AmqpTcpEndpoint amqpTcpEndpoint, Func<AddressFamily, ITcpClient> socketFactory,
            TimeSpan connectionTimeout, CancellationToken cancellationToken)
        {
            ITcpClient socket = await SocketFactory.OpenAsync(amqpTcpEndpoint, socketFactory, connectionTimeout, cancellationToken).ConfigureAwait(false);
            Stream stream = socket.GetStream();

            if (amqpTcpEndpoint.Ssl.Enabled)
            {
                try
                {
                    stream = await SslHelper.TcpUpgradeAsync(stream, amqpTcpEndpoint.Ssl, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    socket.Close();
                    throw;
                }
            }

            SocketFrameHandler socketFrameHandler = new(amqpTcpEndpoint, socket, stream);
            socketFrameHandler._writerTask = Task.Run(socketFrameHandler.WriteLoop, cancellationToken);
            return socketFrameHandler;
        }

        public async Task CloseAsync(CancellationToken cancellationToken)
        {
            if (_closed)
            {
                return;
            }

            try
            {
                await _closingSemaphore.WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    _channelWriter.Complete();
                    if (_writerTask != null)
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
            catch
            {
            }
            finally
            {
                _closingSemaphore.Dispose();
                _closed = true;
            }
        }

        public ValueTask ReadFrameAsync(InboundFrame frame, CancellationToken mainLoopCancellationToken)
        {
            return InboundFrame.ReadFromPipeAsync(_pipeReader,
                _amqpTcpEndpoint.MaxInboundMessageBodySize, frame, mainLoopCancellationToken);
        }

        public bool TryReadFrame(InboundFrame frame)
        {
            return InboundFrame.TryReadFrameFromPipe(_pipeReader,
                _amqpTcpEndpoint.MaxInboundMessageBodySize, frame);
        }

        public async Task SendProtocolHeaderAsync(CancellationToken cancellationToken)
        {
            await _pipeWriter.WriteAsync(Amqp091ProtocolHeader, cancellationToken)
                .ConfigureAwait(false);
            await _pipeWriter.FlushAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public ValueTask WriteAsync(RentedMemory frames, CancellationToken cancellationToken)
        {
            if (_closed)
            {
                frames.Dispose();
                return default;
            }
            else
            {
                return _channelWriter.WriteAsync(frames, cancellationToken);
            }
        }

        private async Task WriteLoop()
        {
            try
            {
                while (await _channelReader.WaitToReadAsync().ConfigureAwait(false))
                {
                    while (_channelReader.TryRead(out RentedMemory frames))
                    {
                        try
                        {
                            await _pipeWriter.WriteAsync(frames.Memory)
                                .ConfigureAwait(false);
                            RabbitMqClientEventSource.Log.CommandSent(frames.Size);
                        }
                        finally
                        {
                            frames.Dispose();
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
    }
}
