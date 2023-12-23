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
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.Framing.Impl
{
#nullable enable
    internal sealed partial class Connection : IConnection
    {
        private bool _disposed;
        private volatile bool _closed;

        private readonly ConnectionConfig _config;
        private readonly ChannelBase _channel0;
        private readonly MainSession _session0;

        private Guid _id = Guid.NewGuid();
        private SessionManager _sessionManager;

        private ShutdownEventArgs? _closeReason;
        public ShutdownEventArgs? CloseReason => Volatile.Read(ref _closeReason);

        internal bool TrackRentedBytes = false;
        internal uint RentedBytes;

        internal Connection(ConnectionConfig config, IFrameHandler frameHandler)
        {
            _config = config;
            _frameHandler = frameHandler;

            Action<Exception, string> onException = (exception, context) => OnCallbackException(CallbackExceptionEventArgs.Build(exception, context));
            _callbackExceptionWrapper = new EventingWrapper<CallbackExceptionEventArgs>(string.Empty, (exception, context) => { });
            _connectionBlockedWrapper = new EventingWrapper<ConnectionBlockedEventArgs>("OnConnectionBlocked", onException);
            _connectionUnblockedWrapper = new EventingWrapper<EventArgs>("OnConnectionUnblocked", onException);
            _connectionShutdownWrapper = new EventingWrapper<ShutdownEventArgs>("OnShutdown", onException);

            _sessionManager = new SessionManager(this, 0);
            _session0 = new MainSession(this);
            _channel0 = new Channel(_config, _session0); ;

            ClientProperties = new Dictionary<string, object?>(_config.ClientProperties)
            {
                ["capabilities"] = Protocol.Capabilities,
                ["connection_name"] = ClientProvidedName
            };

            _mainLoopTask = Task.CompletedTask;
        }

        public Guid Id => _id;

        public string? ClientProvidedName => _config.ClientProvidedName;

        public ushort ChannelMax => _sessionManager.ChannelMax;

        public IDictionary<string, object?> ClientProperties { get; private set; }

        public AmqpTcpEndpoint Endpoint => _frameHandler.Endpoint;

        public uint FrameMax { get; private set; }

        public bool IsOpen => CloseReason is null;

        public int LocalPort => _frameHandler.LocalPort;
        public int RemotePort => _frameHandler.RemotePort;

        public IDictionary<string, object?>? ServerProperties { get; private set; }

        public int CopyBodyToMemoryThreshold => _config.CopyBodyToMemoryThreshold;

        public IEnumerable<ShutdownReportEntry> ShutdownReport => _shutdownReport;
        private ShutdownReportEntry[] _shutdownReport = Array.Empty<ShutdownReportEntry>();

        ///<summary>Explicit implementation of IConnection.Protocol.</summary>
        IProtocol IConnection.Protocol => Endpoint.Protocol;

        ///<summary>Another overload of a Protocol property, useful
        ///for exposing a tighter type.</summary>
        internal ProtocolBase Protocol => (ProtocolBase)Endpoint.Protocol;

        ///<summary>Used for testing only.</summary>
        internal IFrameHandler FrameHandler
        {
            get { return _frameHandler; }
        }

        public event EventHandler<CallbackExceptionEventArgs> CallbackException
        {
            add => _callbackExceptionWrapper.AddHandler(value);
            remove => _callbackExceptionWrapper.RemoveHandler(value);
        }
        private EventingWrapper<CallbackExceptionEventArgs> _callbackExceptionWrapper;

        public event EventHandler<ConnectionBlockedEventArgs> ConnectionBlocked
        {
            add => _connectionBlockedWrapper.AddHandler(value);
            remove => _connectionBlockedWrapper.RemoveHandler(value);
        }
        private EventingWrapper<ConnectionBlockedEventArgs> _connectionBlockedWrapper;

        public event EventHandler<EventArgs> ConnectionUnblocked
        {
            add => _connectionUnblockedWrapper.AddHandler(value);
            remove => _connectionUnblockedWrapper.RemoveHandler(value);
        }
        private EventingWrapper<EventArgs> _connectionUnblockedWrapper;

        public event EventHandler<RecoveringConsumerEventArgs> RecoveringConsumer
        {
            add => _consumerAboutToBeRecovered.AddHandler(value);
            remove => _consumerAboutToBeRecovered.RemoveHandler(value);
        }
        private EventingWrapper<RecoveringConsumerEventArgs> _consumerAboutToBeRecovered;

        public event EventHandler<ShutdownEventArgs> ConnectionShutdown
        {
            add
            {
                ThrowIfDisposed();
                var reason = CloseReason;
                if (reason is null)
                {
                    _connectionShutdownWrapper.AddHandler(value);
                }
                else
                {
                    value(this, reason);
                }
            }
            remove
            {
                ThrowIfDisposed();
                _connectionShutdownWrapper.RemoveHandler(value);
            }
        }
        private EventingWrapper<ShutdownEventArgs> _connectionShutdownWrapper;

        /// <summary>
        /// This event is never fired by non-recovering connections but it is a part of the <see cref="IConnection"/> interface.
        /// </summary>
        public event EventHandler<EventArgs> RecoverySucceeded
        {
            add { }
            remove { }
        }

        /// <summary>
        /// This event is never fired by non-recovering connections but it is a part of the <see cref="IConnection"/> interface.
        /// </summary>
        public event EventHandler<ConnectionRecoveryErrorEventArgs> ConnectionRecoveryError
        {
            add { }
            remove { }
        }

        /// <summary>
        /// This event is never fired by non-recovering connections but it is a part of the <see cref="IConnection"/> interface.
        /// </summary>
        public event EventHandler<ConsumerTagChangedAfterRecoveryEventArgs> ConsumerTagChangeAfterRecovery
        {
            add { }
            remove { }
        }

        /// <summary>
        /// This event is never fired by non-recovering connections but it is a part of the <see cref="IConnection"/> interface.
        /// </summary>
        public event EventHandler<QueueNameChangedAfterRecoveryEventArgs> QueueNameChangedAfterRecovery
        {
            add { }
            remove { }
        }

        internal void TakeOver(Connection other)
        {
            _callbackExceptionWrapper.Takeover(other._callbackExceptionWrapper);
            _connectionBlockedWrapper.Takeover(other._connectionBlockedWrapper);
            _connectionUnblockedWrapper.Takeover(other._connectionUnblockedWrapper);
            _connectionShutdownWrapper.Takeover(other._connectionShutdownWrapper);
        }

        internal IConnection Open()
        {
            return OpenAsync(CancellationToken.None).EnsureCompleted();
        }

        // TODO cancellationToken
        internal async ValueTask<IConnection> OpenAsync(CancellationToken cancellationToken)
        {
            try
            {
                RabbitMqClientEventSource.Log.ConnectionOpened();

                await _frameHandler.ConnectAsync(cancellationToken)
                    .ConfigureAwait(false);

                // Note: this must happen *after* the frame handler is started
                _mainLoopTask = Task.Run(MainLoop, cancellationToken);

                await StartAndTuneAsync(cancellationToken)
                    .ConfigureAwait(false);

                await _channel0.ConnectionOpenAsync(_config.VirtualHost, cancellationToken)
                    .ConfigureAwait(false);

                return this;
            }
            catch // TODO - evaluate all "catch all" clauses to ensure correct exception is eventually thrown
            {
                try
                {
                    var ea = new ShutdownEventArgs(ShutdownInitiator.Library, Constants.InternalError, "FailedOpen");
                    // TODO linked cancellation token?
                    await CloseAsync(ea, true, TimeSpan.FromSeconds(5))
                        .ConfigureAwait(false);
                }
                catch { }

                throw;
            }
        }

        public IChannel CreateChannel()
        {
            EnsureIsOpen();
            ISession session = CreateSession();
            var channel = new Channel(_config, session);
            channel._Private_ChannelOpen();
            return channel;
        }

        public ValueTask<IChannel> CreateChannelAsync()
        {
            EnsureIsOpen();
            ISession session = CreateSession();
            var channel = new Channel(_config, session);
            return channel.OpenAsync();
        }

        internal ISession CreateSession()
        {
            return _sessionManager.Create();
        }

        /// <summary>
        /// The maximum payload size for this connection.
        /// </summary>
        /// <remarks>Compared to <see cref="FrameMax"/> unlimited, unlimited means here <see cref="int.MaxValue"/>.
        /// Also it is reduced by the required framing bytes as in <see cref="RabbitMQ.Client.Impl.Framing.BaseFrameSize"/>.</remarks>
        internal int MaxPayloadSize { get; private set; }

        internal void EnsureIsOpen()
        {
            if (!IsOpen)
            {
                ThrowAlreadyClosedException(CloseReason!);
            }
        }

        ///<summary>API-side invocation of connection.close with timeout.</summary>
        public void Close(ushort reasonCode, string reasonText, TimeSpan timeout, bool abort)
        {
            Close(new ShutdownEventArgs(ShutdownInitiator.Application, reasonCode, reasonText), abort, timeout);
        }

        ///<summary>Asynchronous API-side invocation of connection.close with timeout.</summary>
        public ValueTask CloseAsync(ushort reasonCode, string reasonText, TimeSpan timeout, bool abort)
        {
            return CloseAsync(new ShutdownEventArgs(ShutdownInitiator.Application, reasonCode, reasonText), abort, timeout);
        }

        ///<summary>Try to close connection in a graceful way</summary>
        ///<remarks>
        ///<para>
        ///Shutdown reason contains code and text assigned when closing the connection,
        ///as well as the information about what initiated the close
        ///</para>
        ///<para>
        ///Abort flag, if true, signals to close the ongoing connection immediately
        ///and do not report any errors if it was already closed.
        ///</para>
        ///<para>
        ///Timeout determines how much time internal close operations should be given
        ///to complete.
        ///</para>
        ///</remarks>
        internal void Close(ShutdownEventArgs reason, bool abort, TimeSpan timeout)
        {
            if (!SetCloseReason(reason))
            {
                if (!abort)
                {
                    ThrowAlreadyClosedException(CloseReason!);
                }
            }
            else
            {
                OnShutdown(reason);
                _session0.SetSessionClosing(false);

                try
                {
                    // Try to send connection.close wait for CloseOk in the MainLoop
                    if (!_closed)
                    {
                        _session0.Transmit(new ConnectionClose(reason.ReplyCode, reason.ReplyText, 0, 0));
                    }
                }
                catch (AlreadyClosedException)
                {
                    if (!abort)
                    {
                        throw;
                    }
                }
                catch (NotSupportedException)
                {
                    // buffered stream had unread data in it and Flush()
                    // was called, ignore to not confuse the user
                }
                catch (IOException ioe)
                {
                    if (_channel0.CloseReason is null)
                    {
                        if (!abort)
                        {
                            throw;
                        }
                        else
                        {
                            LogCloseError("Couldn't close connection cleanly. Socket closed unexpectedly", ioe);
                        }
                    }
                }
                finally
                {
                    TerminateMainloop();
                }
            }

            try
            {
                if (!_mainLoopTask.Wait(timeout))
                {
                    _frameHandler.Close();
                }
            }
            catch (AggregateException) // TODO this could be more than just a timeout
            {
            }
        }

        ///<summary>Asychronously try to close connection in a graceful way</summary>
        ///<remarks>
        ///<para>
        ///Shutdown reason contains code and text assigned when closing the connection,
        ///as well as the information about what initiated the close
        ///</para>
        ///<para>
        ///Abort flag, if true, signals to close the ongoing connection immediately
        ///and do not report any errors if it was already closed.
        ///</para>
        ///<para>
        ///Timeout determines how much time internal close operations should be given
        ///to complete.
        ///</para>
        ///</remarks>
        // TODO cancellation token
        internal async ValueTask CloseAsync(ShutdownEventArgs reason, bool abort, TimeSpan timeout)
        {
            // TODO CloseAsync and Close share a lot of code
            if (!SetCloseReason(reason))
            {
                if (!abort)
                {
                    ThrowAlreadyClosedException(CloseReason!);
                }
            }
            else
            {
                OnShutdown(reason);
                _session0.SetSessionClosing(false);

                try
                {
                    // Try to send connection.close wait for CloseOk in the MainLoop
                    if (!_closed)
                    {
                        var method = new ConnectionClose(reason.ReplyCode, reason.ReplyText, 0, 0);
                        await _session0.TransmitAsync(method)
                            .ConfigureAwait(false);
                    }
                }
                catch (AlreadyClosedException)
                {
                    if (!abort)
                    {
                        throw;
                    }
                }
                catch (NotSupportedException)
                {
                    // buffered stream had unread data in it and Flush()
                    // was called, ignore to not confuse the user
                }
                catch (IOException ioe)
                {
                    if (_channel0.CloseReason is null)
                    {
                        if (!abort)
                        {
                            throw;
                        }
                        else
                        {
                            LogCloseError("Couldn't close connection cleanly. Socket closed unexpectedly", ioe);
                        }
                    }
                }
                finally
                {
                    TerminateMainloop();
                }
            }

            try
            {
                await _mainLoopTask.TimeoutAfter(timeout)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
            }
            catch (AggregateException)
            {
            }
            finally
            {
                try
                {
                    await _frameHandler.CloseAsync()
                        .ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }

        internal void InternalClose(ShutdownEventArgs reason)
        {
            if (!SetCloseReason(reason))
            {
                if (_closed)
                {
                    ThrowAlreadyClosedException(CloseReason!);
                }
                // We are quiescing, but still allow for server-close
            }

            OnShutdown(reason);
            _session0.SetSessionClosing(true);
            TerminateMainloop();
        }

        // Only call at the end of the Mainloop or HeartbeatLoop
        // TODO async
        private void FinishClose()
        {
            _closed = true;
            MaybeStopHeartbeatTimers();

            _frameHandler.Close();
            _channel0.SetCloseReason(CloseReason);
            _channel0.FinishClose();
            RabbitMqClientEventSource.Log.ConnectionClosed();
        }

        ///<summary>Broadcasts notification of the final shutdown of the connection.</summary>
        private void OnShutdown(ShutdownEventArgs reason)
        {
            ThrowIfDisposed();
            _connectionShutdownWrapper.Invoke(this, reason);
        }

        private bool SetCloseReason(ShutdownEventArgs reason)
        {
            return Interlocked.CompareExchange(ref _closeReason, reason, null) is null;
        }

        private void LogCloseError(string error, Exception ex)
        {
            ESLog.Error(error, ex);

            lock (_shutdownReport)
            {
                var replacement = new ShutdownReportEntry[_shutdownReport.Length + 1];
                replacement[replacement.Length - 1] = new ShutdownReportEntry(error, ex);
                _shutdownReport.CopyTo(replacement.AsSpan());
                _shutdownReport = replacement;
            }
        }

        internal void OnCallbackException(CallbackExceptionEventArgs args)
        {
            _callbackExceptionWrapper.Invoke(this, args);
        }

        internal void Write(RentedOutgoingMemory frames)
        {
            ValueTask task = _frameHandler.WriteAsync(frames);
            if (!task.IsCompletedSuccessfully)
            {
                task.EnsureCompleted();
            }
        }

        internal ValueTask WriteAsync(RentedOutgoingMemory frames)
        {
            TrackRented(frames.RentedArraySize);

            return _frameHandler.WriteAsync(frames);
        }

        private void TrackRented(int size)
        {
            if (TrackRentedBytes && size > 0)
            {
#if NET
                Interlocked.Add(ref RentedBytes, (uint)size);
#else
                Interlocked.Add(ref Unsafe.As<uint, int>(ref RentedBytes), size);
#endif
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                this.Abort(InternalConstants.DefaultConnectionAbortTimeout);
                _mainLoopTask.Wait();
            }
            catch (OperationInterruptedException)
            {
                // ignored, see rabbitmq/rabbitmq-dotnet-client#133
            }
            finally
            {
                _disposed = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                ThrowObjectDisposedException();
            }

            static void ThrowObjectDisposedException()
            {
                throw new ObjectDisposedException(typeof(Connection).FullName);
            }
        }

        public override string ToString()
        {
            return $"Connection({_id},{Endpoint})";
        }

        private static void ThrowAlreadyClosedException(ShutdownEventArgs closeReason)
        {
            throw new AlreadyClosedException(closeReason);
        }
    }
}
