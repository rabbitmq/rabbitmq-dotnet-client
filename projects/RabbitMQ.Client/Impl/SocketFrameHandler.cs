// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.Impl
{
    internal sealed class SocketFrameHandler : IFrameHandler
    {
        private readonly AmqpTcpEndpoint _amqpTcpEndpoint;
        private readonly ITcpClient _socket;
        private readonly Stream _stream;

        private readonly ChannelWriter<OutgoingFrame> _channelWriter;
        private readonly ChannelReader<OutgoingFrame> _channelReader;
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

            var channel = System.Threading.Channels.Channel.CreateBounded<OutgoingFrame>(
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
                    if (value != default)
                    {
                        _socket.ReceiveTimeout = value;
                        _stream.ReadTimeout = (int)value.TotalMilliseconds;
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
                if (value != default)
                {
                    _socket.Client.SendTimeout = (int)value.TotalMilliseconds;
                    _stream.WriteTimeout = (int)value.TotalMilliseconds;
                }
            }
        }

        public static async Task<SocketFrameHandler> CreateAsync(AmqpTcpEndpoint amqpTcpEndpoint, Func<AddressFamily, ITcpClient> socketFactory,
            TimeSpan connectionTimeout, CancellationToken cancellationToken)
        {
            ITcpClient socket = await SocketFactory.OpenAsync(amqpTcpEndpoint, socketFactory, connectionTimeout, cancellationToken)
                .ConfigureAwait(false);
            Stream stream = socket.GetStream();

            if (amqpTcpEndpoint.Ssl.Enabled)
            {
                try
                {
                    stream = await SslHelper.TcpUpgradeAsync(stream, amqpTcpEndpoint.Ssl, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch
                {
                    socket.Close();
                    throw;
                }
            }

            SocketFrameHandler socketFrameHandler = new(amqpTcpEndpoint, socket, stream);
            socketFrameHandler._writerTask = Task.Run(socketFrameHandler.WriteLoopAsync, cancellationToken);
            return socketFrameHandler;
        }

        public async ValueTask CloseAsync(CancellationToken cancellationToken)
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
                    // TryComplete rather than Complete: WriteLoopAsync may have
                    // already completed the writer with an exception (see
                    // issue #1930). Complete() would throw InvalidOperationException
                    // in that race, the outer catch would swallow it, and
                    // `await _writerTask` below would be skipped, leaving the
                    // write-loop exception unobserved.
                    _channelWriter.TryComplete();
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

        public async ValueTask SendProtocolHeaderAsync(CancellationToken cancellationToken)
        {
            await _pipeWriter.WriteAsync(Amqp091ProtocolHeader, cancellationToken)
                .ConfigureAwait(false);
            await _pipeWriter.FlushAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public ValueTask WriteAsync(OutgoingFrame frames, CancellationToken cancellationToken)
        {
            if (_closed)
            {
                frames.Dispose();
                return default;
            }

            return WriteAsyncCore(frames, cancellationToken);
        }

        private ValueTask WriteAsyncCore(OutgoingFrame frames, CancellationToken cancellationToken)
        {
            ValueTask writeTask = _channelWriter.WriteAsync(frames, cancellationToken);
            if (writeTask.IsCompletedSuccessfully)
            {
                return default;
            }

            // The channel accepted the frame asynchronously, or it rejected the
            // write (e.g. because WriteLoopAsync completed the writer with an
            // exception). In the rejection case `frames` never reached the
            // reader, so this method owns the dispose. See issue #1930.
            return AwaitAndDisposeOnException(writeTask, frames);

            static async ValueTask AwaitAndDisposeOnException(ValueTask task, OutgoingFrame frames)
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch
                {
                    frames.Dispose();
                    throw;
                }
            }
        }

        private async Task WriteLoopAsync()
        {
            try
            {
                while (await _channelReader.WaitToReadAsync().ConfigureAwait(false))
                {
                    while (_channelReader.TryRead(out OutgoingFrame frames))
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

                // Fail both already-blocked and subsequent WriteAsync callers
                // with ChannelClosedException(innerException: ex). Without
                // this, producers block indefinitely on a full channel whose
                // reader has died, and any holder of a publisher-confirm
                // semaphore (Channel.BasicPublish.cs) blocks the shutdown
                // handler in turn. See issue #1930.
                //
                // TryComplete rather than Complete: CloseAsync may have raced
                // us and already completed the channel.
                _channelWriter.TryComplete(ex);
                throw;
            }
            finally
            {
                // Drain any leftover frames so their pooled buffers return to
                // the array pool rather than waiting for GC.
                while (_channelReader.TryRead(out OutgoingFrame leftover))
                {
                    leftover.Dispose();
                }
            }
        }
    }
}
