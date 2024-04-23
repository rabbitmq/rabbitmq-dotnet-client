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
using System.Diagnostics;
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
        private readonly ChannelBase _channel0; // FUTURE Note: this is not disposed
        private readonly MainSession _session0;

        private Guid _id = Guid.NewGuid();
        private SessionManager _sessionManager;

        private ShutdownEventArgs? _closeReason;
        public ShutdownEventArgs? CloseReason => Volatile.Read(ref _closeReason);

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
                ShutdownEventArgs? reason = CloseReason;
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

        public event AsyncEventHandler<ShutdownEventArgs> ConnectionShutdownAsync
        {
            add
            {
                ThrowIfDisposed();
                ShutdownEventArgs? reason = CloseReason;
                if (reason is null)
                {
                    _connectionShutdownWrapperAsync.AddHandler(value);
                }
                else
                {
                    value(this, reason);
                }
            }
            remove
            {
                ThrowIfDisposed();
                _connectionShutdownWrapperAsync.RemoveHandler(value);
            }
        }
        private AsyncEventingWrapper<ShutdownEventArgs> _connectionShutdownWrapperAsync;

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

        internal async ValueTask<IConnection> OpenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                RabbitMqClientEventSource.Log.ConnectionOpened();

                cancellationToken.ThrowIfCancellationRequested();

                await _frameHandler.ConnectAsync(cancellationToken)
                    .ConfigureAwait(false);

                // Note: this must happen *after* the frame handler is started
                _mainLoopTask = Task.Run(MainLoop, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                await StartAndTuneAsync(cancellationToken)
                    .ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                await _channel0.ConnectionOpenAsync(_config.VirtualHost, cancellationToken)
                    .ConfigureAwait(false);

                return this;
            }
            catch
            {
                try
                {
                    var ea = new ShutdownEventArgs(ShutdownInitiator.Library, Constants.InternalError, "FailedOpen");
                    await CloseAsync(ea, true,
                        InternalConstants.DefaultConnectionAbortTimeout,
                        cancellationToken).ConfigureAwait(false);
                }
                catch { }

                throw;
            }
        }

        public Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
        {
            EnsureIsOpen();
            ISession session = CreateSession();
            var channel = new Channel(_config, session);
            return channel.OpenAsync(cancellationToken);
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

        ///<summary>Asynchronous API-side invocation of connection.close with timeout.</summary>
        public Task CloseAsync(ushort reasonCode, string reasonText, TimeSpan timeout, bool abort,
            CancellationToken cancellationToken = default)
        {
            var reason = new ShutdownEventArgs(ShutdownInitiator.Application, reasonCode, reasonText);
            return CloseAsync(reason, abort, timeout, cancellationToken);
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
        internal async Task CloseAsync(ShutdownEventArgs reason, bool abort, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (false == SetCloseReason(reason))
            {
                // close reason is already set
                if (false == abort)
                {
                    ThrowAlreadyClosedException(CloseReason!);
                }
            }
            else
            {
                cancellationToken.ThrowIfCancellationRequested();

                await OnShutdownAsync(reason, cancellationToken)
                    .ConfigureAwait(false);
                await _session0.SetSessionClosingAsync(false)
                    .ConfigureAwait(false);

                try
                {
                    // Try to send connection.close wait for CloseOk in the MainLoop
                    if (false == _closed)
                    {
                        var method = new ConnectionClose(reason.ReplyCode, reason.ReplyText, 0, 0);
                        await _session0.TransmitAsync(method, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                catch (AlreadyClosedException)
                {
                    if (false == abort)
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
                    /*
                     * Note:
                     * NotifyReceivedCloseOk will cancel the main loop
                     */
                    MaybeTerminateMainloopAndStopHeartbeatTimers();
                }
            }

            try
            {
                await _mainLoopTask.WaitAsync(timeout, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                try
                {
                    await _frameHandler.CloseAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                catch
                {
                }

                if (false == abort)
                {
                    throw;
                }
            }
        }

        internal async Task ClosedViaPeerAsync(ShutdownEventArgs reason, CancellationToken cancellationToken)
        {
            if (false == SetCloseReason(reason))
            {
                if (_closed)
                {
                    ThrowAlreadyClosedException(CloseReason!);
                }
                // We are quiescing, but still allow for server-close
            }

            await OnShutdownAsync(reason, cancellationToken)
                .ConfigureAwait(false);
            _session0.SetSessionClosing(true);
            MaybeTerminateMainloopAndStopHeartbeatTimers(cancelMainLoop: true);
        }

        // Only call at the end of the Mainloop or HeartbeatLoop
        private async Task FinishCloseAsync(CancellationToken cancellationToken)
        {
            _mainLoopCts.Cancel();
            _closed = true;
            MaybeStopHeartbeatTimers();

            await _frameHandler.CloseAsync(cancellationToken)
                .ConfigureAwait(false);
            _channel0.SetCloseReason(CloseReason);
            await _channel0.FinishCloseAsync(cancellationToken)
                .ConfigureAwait(false);
            RabbitMqClientEventSource.Log.ConnectionClosed();
        }

        ///<summary>Broadcasts notification of the final shutdown of the connection.</summary>
        private Task OnShutdownAsync(ShutdownEventArgs reason, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            _connectionShutdownWrapper.Invoke(this, reason);
            return _connectionShutdownWrapperAsync.InvokeAsync(this, reason, cancellationToken);
        }

        private bool SetCloseReason(ShutdownEventArgs reason)
        {
            if (reason is null)
            {
                throw new ArgumentNullException(nameof(reason));
            }

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

        internal ValueTask WriteAsync(RentedMemory frames, CancellationToken cancellationToken)
        {
            Activity.Current.SetNetworkTags(_frameHandler);
            return _frameHandler.WriteAsync(frames, cancellationToken);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (IsOpen)
                {
                    this.AbortAsync().GetAwaiter().GetResult();
                }

                _session0.Dispose();
                _mainLoopCts.Dispose();
                _sessionManager.Dispose();
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
