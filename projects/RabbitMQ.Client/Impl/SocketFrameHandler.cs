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
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.Impl
{
    internal sealed class SocketFrameHandler : IFrameHandler
    {
        private readonly AmqpTcpEndpoint _amqpTcpEndpoint;
        private readonly ITcpClient _socket;
        // Captured once: the write loop can close the socket while tracing and shutdown handlers read them. #2108
        private readonly EndPoint? _localEndPoint;
        private readonly EndPoint? _remoteEndPoint;
        private readonly Stream _stream;

        private readonly ChannelWriter<OutgoingFrame> _channelWriter;
        private readonly ChannelReader<OutgoingFrame> _channelReader;
        private readonly SemaphoreSlim _closingSemaphore = new SemaphoreSlim(1, 1);

        private readonly PipeWriter _pipeWriter;
        private readonly PipeReader _pipeReader;
        private Task? _writerTask;
        private volatile Exception? _writeLoopException;

        // volatile for the unsynchronized fast-path reads at the top of
        // CloseAsync and WriteAsync. The read inside CloseAsync's critical
        // section is already ordered by the semaphore.
        private volatile bool _closed;

        private static ReadOnlyMemory<byte> Amqp091ProtocolHeader => new byte[] { (byte)'A', (byte)'M', (byte)'Q', (byte)'P', 0, 0, 9, 1 };

        private SocketFrameHandler(AmqpTcpEndpoint amqpTcpEndpoint, ITcpClient socket, Stream stream)
        {
            _amqpTcpEndpoint = amqpTcpEndpoint;
            _socket = socket;
            _localEndPoint = socket.Client?.LocalEndPoint;
            _remoteEndPoint = socket.Client?.RemoteEndPoint;
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
            get { return _localEndPoint!; }
        }

        public int LocalPort
        {
            get { return ((IPEndPoint)LocalEndPoint).Port; }
        }

        public EndPoint RemoteEndPoint
        {
            get { return _remoteEndPoint!; }
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
                        // Both take int milliseconds, which a larger value overflows. #2029.
                        value = InternalConstants.ClampToCancellationTokenSourceDelay(value);
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
                    // See ReadTimeout.
                    value = InternalConstants.ClampToCancellationTokenSourceDelay(value);
                    _socket.Client.SendTimeout = (int)value.TotalMilliseconds;
                    _stream.WriteTimeout = (int)value.TotalMilliseconds;
                }
            }
        }

        private TimeSpan _flushTimeout;
#if NET
        private CancellationTokenSource? _flushTimeoutCts;
#endif

        internal TimeSpan FlushTimeout
        {
            set => _flushTimeout = value;
        }

        public static async Task<SocketFrameHandler> CreateAsync(AmqpTcpEndpoint amqpTcpEndpoint, Func<AddressFamily, ITcpClient> socketFactory,
            TimeSpan connectionTimeout, CancellationToken cancellationToken)
        {
            ITcpClient socket = await SocketFactory.OpenAsync(amqpTcpEndpoint, socketFactory, connectionTimeout, cancellationToken)
                .ConfigureAwait(false);
            Stream stream = socket.GetStream();

            if (amqpTcpEndpoint.Ssl.Enabled)
            {
                // The TLS handshake shares the connect timeout, or a host that accepts TCP and never
                // answers hangs the attempt and recovery with it. Closing the socket is what ends it on
                // netstandard2.0, whose handshake takes no token. #1871.
                using var timeoutTokenSource = new CancellationTokenSource(
                    InternalConstants.ClampToCancellationTokenSourceDelay(connectionTimeout));
                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    timeoutTokenSource.Token, cancellationToken);
                using CancellationTokenRegistration closeOnCancel = linkedTokenSource.Token.Register(
                    static state => ((ITcpClient)state!).Close(), socket);
                try
                {
                    stream = await SslHelper.TcpUpgradeAsync(stream, amqpTcpEndpoint.Ssl, linkedTokenSource.Token)
                        .ConfigureAwait(false);
                }
                catch (Exception e) when (timeoutTokenSource.IsCancellationRequested && false == cancellationToken.IsCancellationRequested)
                {
                    socket.Close();
                    string msg = $"TLS handshake with {amqpTcpEndpoint} timed out";
                    throw new ConnectFailureException(msg, new TimeoutException(msg, e));
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
            }
            catch
            {
                // The caller's token fired, so this must not fall through without the semaphore.
                // It must not assume another closer will finish either: see #2022.
                return;
            }

            try
            {
                if (_closed)
                {
                    // A concurrent closer finished while we waited.
                    return;
                }

                _channelWriter.TryComplete();
                try
                {
                    if (_writerTask != null)
                    {
                        await _writerTask.ConfigureAwait(false);
                    }
                }
                catch
                {
                    // A failed writer still leaves both pipes to complete. #2108.
                }

                try
                {
                    await _pipeWriter.CompleteAsync().ConfigureAwait(false);
                }
                catch
                {
                    // The socket may already be closed, so a final flush can fail.
                }

                try
                {
                    await _pipeReader.CompleteAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Reader disposal must not skip closing the socket.
                }

                CloseSocket();

#if NET
                _flushTimeoutCts?.Dispose();
#endif
            }
            finally
            {
                /*
                 * Set _closed before releasing so the next waiter sees the close
                 * as done and returns rather than repeating it.
                 *
                 * The semaphore is deliberately NOT disposed. Disposing it while a
                 * concurrent CloseAsync is parked in WaitAsync leaves that waiter
                 * pending forever -- not even its own cancellation token releases
                 * it, which was verified directly. MainLoop's FinishCloseAsync is
                 * one such concurrent closer, so a frame-handler close racing it
                 * stranded MainLoop, _mainLoopTask never completed, and
                 * Connection.CloseAsync burned its full close timeout before
                 * surfacing a bare OperationCanceledException. See issue #1968.
                 *
                 * SemaphoreSlim only needs disposal when AvailableWaitHandle has
                 * been read, and this one never exposes it, so there is nothing
                 * to reclaim.
                 */
                _closed = true;
                _closingSemaphore.Release();
            }
        }

        public Exception? WriteLoopException => _writeLoopException;

        public void CloseSocket()
        {
            // Must not set _closed: CloseAsync fast-paths on it, so the authoritative
            // FinishCloseAsync close would become a no-op and leak the write loop.
            // #2022, docs/internal/connection-shutdown-and-cancellation.md.
            try
            {
                _socket.Close();
            }
            catch
            {
                // ignore, we are closing anyway
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
                            await FlushPipeAsync()
                                .ConfigureAwait(false);
                            RabbitMqClientEventSource.Log.CommandSent(frames.Size);
                        }
                        finally
                        {
                            frames.Dispose();
                        }
                    }

                    await FlushPipeAsync()
                        .ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                // Release blocked publishers before receive-side shutdown needs their locks. #1930.
                _channelWriter.TryComplete(ex);
                _writeLoopException = ex;
                // A failed serialization can leave a partial frame in the pipe. #2108.
                CloseSocket();
                ESLog.Error("Background socket write loop has crashed", ex);
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

        // Flush the pipe writer, applying a cancellation timeout when configured.
        // A non-default _flushTimeout allows tests to bound how long an async socket
        // write can stall before the write loop is crashed and blocked publishers are
        // unblocked. See issue #1930.
        private async ValueTask FlushPipeAsync()
        {
            if (_flushTimeout == default)
            {
                await _pipeWriter.FlushAsync().ConfigureAwait(false);
                return;
            }

#if NET
            // Reuse a single CTS across flushes to avoid allocating a new CTS and
            // backing timer on every call. TryReset() recycles it when the previous
            // flush completed before the timeout; if the timeout fired (CTS already
            // cancelled) TryReset() returns false and we allocate a fresh one.
            if (_flushTimeoutCts is null || !_flushTimeoutCts.TryReset())
            {
                _flushTimeoutCts?.Dispose();
                _flushTimeoutCts = new CancellationTokenSource();
            }
            _flushTimeoutCts.CancelAfter(_flushTimeout);
            await _pipeWriter.FlushAsync(_flushTimeoutCts.Token).ConfigureAwait(false);
#else
            using var cts = new CancellationTokenSource(_flushTimeout);
            await _pipeWriter.FlushAsync(cts.Token).ConfigureAwait(false);
#endif
        }
    }
}
