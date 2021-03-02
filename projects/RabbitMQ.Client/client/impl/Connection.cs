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
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Logging;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Framing.Impl
{
    internal sealed class Connection : IConnection
    {
        private static readonly string s_version = typeof(Connection).Assembly
                                            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                            .InformationalVersion;

        private bool _disposed;
        private volatile bool _closed;

        private readonly IConnectionFactory _factory;
        private readonly IFrameHandler _frameHandler;
        private readonly ModelBase _model0;
        private readonly MainSession _session0;

        private Guid _id = Guid.NewGuid();
        private volatile bool _running = true;
        private SessionManager _sessionManager;

        //
        // Heartbeats
        //
        private TimeSpan _heartbeat = TimeSpan.Zero;
        private TimeSpan _heartbeatTimeSpan = TimeSpan.FromSeconds(0);
        private int _missedHeartbeats;
        private bool _heartbeatDetected;

        private Timer _heartbeatWriteTimer;
        private Timer _heartbeatReadTimer;

        private Task _mainLoopTask;

        private ShutdownEventArgs _closeReason;
        public ShutdownEventArgs CloseReason => Volatile.Read(ref _closeReason);

        public Connection(IConnectionFactory factory, IFrameHandler frameHandler, string clientProvidedName = null)
        {
            ClientProvidedName = clientProvidedName;
            _factory = factory;
            _frameHandler = frameHandler;

            Action<Exception, string> onException = (exception, context) => OnCallbackException(CallbackExceptionEventArgs.Build(exception, context));
            _callbackExceptionWrapper = new EventingWrapper<CallbackExceptionEventArgs>(string.Empty, (exception, context) => { });
            _connectionBlockedWrapper = new EventingWrapper<ConnectionBlockedEventArgs>("OnConnectionBlocked", onException);
            _connectionUnblockedWrapper = new EventingWrapper<EventArgs>("OnConnectionUnblocked", onException);
            _connectionShutdownWrapper = new EventingWrapper<ShutdownEventArgs>("OnShutdown", onException);

            _sessionManager = new SessionManager(this, 0);
            _session0 = new MainSession(this) { Handler = NotifyReceivedCloseOk };
            _model0 = (ModelBase)Protocol.CreateModel(factory, _session0);

            StartMainLoop();
            Open();
        }

        public Guid Id => _id;

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

        public event EventHandler<ShutdownEventArgs> ConnectionShutdown
        {
            add
            {
                ThrowIfDisposed();
                if (IsOpen)
                {
                    _connectionShutdownWrapper.AddHandler(value);
                }
                else
                {
                    value(this, CloseReason);
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
        public event EventHandler<QueueNameChangedAfterRecoveryEventArgs> QueueNameChangeAfterRecovery
        {
            add { }
            remove { }
        }

        public string ClientProvidedName { get; }

        public ushort ChannelMax => _sessionManager.ChannelMax;

        public IDictionary<string, object> ClientProperties { get; private set; }

        public AmqpTcpEndpoint Endpoint => _frameHandler.Endpoint;

        public uint FrameMax { get; private set; }

        public TimeSpan Heartbeat
        {
            get => _heartbeat;
            set
            {
                _heartbeat = value;
                // timers fire at slightly below half the interval to avoid race
                // conditions
                _heartbeatTimeSpan = TimeSpan.FromMilliseconds(_heartbeat.TotalMilliseconds / 4);
                _frameHandler.ReadTimeout = TimeSpan.FromMilliseconds(_heartbeat.TotalMilliseconds * 2);
            }
        }

        public bool IsOpen => CloseReason is null;

        public int LocalPort => _frameHandler.LocalPort;

        ///<summary>Another overload of a Protocol property, useful
        ///for exposing a tighter type.</summary>
        internal ProtocolBase Protocol => (ProtocolBase)Endpoint.Protocol;

        public int RemotePort => _frameHandler.RemotePort;

        public IDictionary<string, object> ServerProperties { get; private set; }

        public IList<ShutdownReportEntry> ShutdownReport => _shutdownReport;
        private ShutdownReportEntry[] _shutdownReport = Array.Empty<ShutdownReportEntry>();

        ///<summary>Explicit implementation of IConnection.Protocol.</summary>
        IProtocol IConnection.Protocol => Endpoint.Protocol;

        internal static IDictionary<string, object> DefaultClientProperties()
        {
            var table = new Dictionary<string, object>(5)
            {
                ["product"] = Encoding.UTF8.GetBytes("RabbitMQ"),
                ["version"] = Encoding.UTF8.GetBytes(s_version),
                ["platform"] = Encoding.UTF8.GetBytes(".NET"),
                ["copyright"] = Encoding.UTF8.GetBytes("Copyright (c) 2007-2020 VMware, Inc."),
                ["information"] = Encoding.UTF8.GetBytes("Licensed under the MPL. See https://www.rabbitmq.com/")
            };
            return table;
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
        ///to complete. System.Threading.Timeout.InfiniteTimeSpan value means infinity.
        ///</para>
        ///</remarks>
        internal void Close(ShutdownEventArgs reason, bool abort, TimeSpan timeout)
        {
            if (!SetCloseReason(reason))
            {
                if (!abort)
                {
                    throw new AlreadyClosedException(CloseReason);
                }
            }
            else
            {
                OnShutdown();
                _session0.SetSessionClosing(false);

                try
                {
                    // Try to send connection.close
                    // Wait for CloseOk in the MainLoop
                    _session0.Transmit(new Impl.ConnectionClose(reason.ReplyCode, reason.ReplyText, 0, 0));
                }
                catch (AlreadyClosedException)
                {
                    if (!abort)
                    {
                        throw;
                    }
                }
#pragma warning disable 0168
                catch (NotSupportedException nse)
                {
                    // buffered stream had unread data in it and Flush()
                    // was called, ignore to not confuse the user
                }
#pragma warning restore 0168
                catch (IOException ioe)
                {
                    if (_model0.CloseReason is null)
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
            catch (AggregateException)
            {
                _frameHandler.Close();
            }
        }

        private void ClosingLoop()
        {
            try
            {
                _frameHandler.ReadTimeout = TimeSpan.Zero;
                // Wait for response/socket closure or timeout
                while (!_closed)
                {
                    MainLoopIteration();
                }
            }
            catch (ObjectDisposedException ode)
            {
                if (!_closed)
                {
                    LogCloseError("Connection didn't close cleanly", ode);
                }
            }
            catch (EndOfStreamException eose)
            {
                if (_model0.CloseReason is null)
                {
                    LogCloseError("Connection didn't close cleanly. Socket closed unexpectedly", eose);
                }
            }
            catch (IOException ioe)
            {
                LogCloseError("Connection didn't close cleanly. Socket closed unexpectedly", ioe);
            }
            catch (Exception e)
            {
                LogCloseError("Unexpected exception while closing: ", e);
            }
        }

        internal ISession CreateSession()
        {
            return _sessionManager.Create();
        }

        internal void EnsureIsOpen()
        {
            if (!IsOpen)
            {
                throw new AlreadyClosedException(CloseReason);
            }
        }

        // Only call at the end of the Mainloop or HeartbeatLoop
        private void FinishClose()
        {
            _closed = true;
            MaybeStopHeartbeatTimers();

            _frameHandler.Close();
            _model0.SetCloseReason(CloseReason);
            _model0.FinishClose();
        }

        private void HandleMainLoopException(ShutdownEventArgs reason)
        {
            if (!SetCloseReason(reason))
            {
                LogCloseError("Unexpected Main Loop Exception while closing: " + reason, new Exception(reason.ToString()));
                return;
            }

            OnShutdown();
            LogCloseError($"Unexpected connection closure: {reason}", new Exception(reason.ToString()));
        }

        private bool HardProtocolExceptionHandler(HardProtocolException hpe)
        {
            if (SetCloseReason(hpe.ShutdownReason))
            {
                OnShutdown();
                _session0.SetSessionClosing(false);
                try
                {
                    _session0.Transmit(new ConnectionClose(hpe.ShutdownReason.ReplyCode, hpe.ShutdownReason.ReplyText, 0, 0));
                    return true;
                }
                catch (IOException ioe)
                {
                    LogCloseError("Broker closed socket unexpectedly", ioe);
                }
            }
            else
            {
                LogCloseError("Hard Protocol Exception occured while closing the connection", hpe);
            }

            return false;
        }

        internal void InternalClose(ShutdownEventArgs reason)
        {
            if (!SetCloseReason(reason))
            {
                if (_closed)
                {
                    throw new AlreadyClosedException(CloseReason);
                }
                // We are quiescing, but still allow for server-close
            }

            OnShutdown();
            _session0.SetSessionClosing(true);
            TerminateMainloop();
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

        private void MainLoop()
        {
            bool shutdownCleanly = false;
            try
            {
                while (_running)
                {
                    try
                    {
                        MainLoopIteration();
                    }
                    catch (SoftProtocolException spe)
                    {
                        QuiesceChannel(spe);
                    }
                }
                shutdownCleanly = true;
            }
            catch (EndOfStreamException eose)
            {
                // Possible heartbeat exception
                HandleMainLoopException(new ShutdownEventArgs(
                    ShutdownInitiator.Library,
                    0,
                    "End of stream",
                    eose));
            }
            catch (HardProtocolException hpe)
            {
                shutdownCleanly = HardProtocolExceptionHandler(hpe);
            }
            catch (Exception ex)
            {
                HandleMainLoopException(new ShutdownEventArgs(ShutdownInitiator.Library,
                    Constants.InternalError,
                    "Unexpected Exception",
                    ex));
            }

            // If allowed for clean shutdown, run main loop until the
            // connection closes.
            if (shutdownCleanly)
            {
#pragma warning disable 0168
                try
                {
                    ClosingLoop();
                }
                catch (SocketException)
                {
                    // means that socket was closed when frame handler
                    // attempted to use it. Since we are shutting down,
                    // ignore it.
                }
#pragma warning restore 0168
            }

            FinishClose();
        }

        private void MainLoopIteration()
        {
            InboundFrame frame = _frameHandler.ReadFrame();
            NotifyHeartbeatListener();

            bool shallReturn = true;
            // We have received an actual frame.
            if (frame.Type == FrameType.FrameHeartbeat)
            {
                // Ignore it: we've already just reset the heartbeat
            }
            else if (frame.Channel == 0)
            {
                // In theory, we could get non-connection.close-ok
                // frames here while we're quiescing (m_closeReason !=
                // null). In practice, there's a limited number of
                // things the server can ask of us on channel 0 -
                // essentially, just connection.close. That, combined
                // with the restrictions on pipelining, mean that
                // we're OK here to handle channel 0 traffic in a
                // quiescing situation, even though technically we
                // should be ignoring everything except
                // connection.close-ok.
                shallReturn = _session0.HandleFrame(in frame);
            }
            else
            {
                // If we're still m_running, but have a m_closeReason,
                // then we must be quiescing, which means any inbound
                // frames for non-zero channels (and any inbound
                // commands on channel zero that aren't
                // Connection.CloseOk) must be discarded.
                if (_closeReason is null)
                {
                    // No close reason, not quiescing the
                    // connection. Handle the frame. (Of course, the
                    // Session itself may be quiescing this particular
                    // channel, but that's none of our concern.)
                    shallReturn = _sessionManager.Lookup(frame.Channel).HandleFrame(in frame);
                }
            }

            if (shallReturn)
            {
                frame.ReturnPayload();
            }
        }

        private void NotifyHeartbeatListener()
        {
            _heartbeatDetected = true;
        }

        private void NotifyReceivedCloseOk()
        {
            TerminateMainloop();
            _closed = true;
        }

        internal void OnCallbackException(CallbackExceptionEventArgs args)
        {
            _callbackExceptionWrapper.Invoke(this, args);
        }

        ///<summary>Broadcasts notification of the final shutdown of the connection.</summary>
        private void OnShutdown()
        {
            ThrowIfDisposed();
            _connectionShutdownWrapper.Invoke(this, CloseReason);
        }

        private void Open()
        {
            StartAndTune();
            _model0.ConnectionOpen(_factory.VirtualHost);
        }

        ///<summary>
        /// Sets the channel named in the SoftProtocolException into
        /// "quiescing mode", where we issue a channel.close and
        /// ignore everything except for subsequent channel.close
        /// messages and the channel.close-ok reply that should
        /// eventually arrive.
        ///</summary>
        ///<remarks>
        ///<para>
        /// Since a well-behaved peer will not wait indefinitely before
        /// issuing the close-ok, we don't bother with a timeout here;
        /// compare this to the case of a connection.close-ok, where a
        /// timeout is necessary.
        ///</para>
        ///<para>
        /// We need to send the close method and politely wait for a
        /// reply before marking the channel as available for reuse.
        ///</para>
        ///<para>
        /// As soon as SoftProtocolException is detected, we should stop
        /// servicing ordinary application work, and should concentrate
        /// on bringing down the channel as quickly and gracefully as
        /// possible. The way this is done, as per the close-protocol,
        /// is to signal closure up the stack *before* sending the
        /// channel.close, by invoking ISession.Close. Once the upper
        /// layers have been signalled, we are free to do what we need
        /// to do to clean up and shut down the channel.
        ///</para>
        ///</remarks>
        private void QuiesceChannel(SoftProtocolException pe)
        {
            // Construct the QuiescingSession that we'll use during
            // the quiesce process.

            ISession newSession = new QuiescingSession(this,
                (ushort)pe.Channel,
                pe.ShutdownReason);

            // Here we detach the session from the connection. It's
            // still alive: it just won't receive any further frames
            // from the mainloop (once we return to the mainloop, of
            // course). Instead, those frames will be directed at the
            // new QuiescingSession.
            ISession oldSession = _sessionManager.Swap(pe.Channel, newSession);

            // Now we have all the information we need, and the event
            // flow of the *lower* layers is set up properly for
            // shutdown. Signal channel closure *up* the stack, toward
            // the model and application.
            oldSession.Close(pe.ShutdownReason);

            // The upper layers have been signalled. Now we can tell
            // our peer. The peer will respond through the lower
            // layers - specifically, through the QuiescingSession we
            // installed above.
            newSession.Transmit(new Impl.ChannelClose(pe.ReplyCode, pe.Message, 0, 0));
        }

        private bool SetCloseReason(ShutdownEventArgs reason)
        {
            return Interlocked.CompareExchange(ref _closeReason, reason, null) is null;
        }

        private void MaybeStartHeartbeatTimers()
        {
            if (Heartbeat != TimeSpan.Zero)
            {
                _heartbeatWriteTimer ??= new Timer(HeartbeatWriteTimerCallback, null, 200, Timeout.Infinite);
                _heartbeatReadTimer ??= new Timer(HeartbeatReadTimerCallback, null, 300, Timeout.Infinite);
            }
        }

        private void StartMainLoop()
        {
            _mainLoopTask = Task.Factory.StartNew(MainLoop, TaskCreationOptions.LongRunning);
        }

        private void HeartbeatReadTimerCallback(object state)
        {
            if (_heartbeatReadTimer is null)
            {
                return;
            }

            bool shouldTerminate = false;

            try
            {
                if (!_closed)
                {
                    if (_heartbeatDetected)
                    {
                        _heartbeatDetected = false;
                        _missedHeartbeats = 0;
                    }
                    else
                    {
                        _missedHeartbeats++;
                    }

                    // We check against 8 = 2 * 4 because we need to wait for at
                    // least two complete heartbeat setting intervals before
                    // complaining, and we've set the socket timeout to a quarter
                    // of the heartbeat setting in setHeartbeat above.
                    if (_missedHeartbeats > 2 * 4)
                    {
                        var eose = new EndOfStreamException($"Heartbeat missing with heartbeat == {_heartbeat} seconds");
                        LogCloseError(eose.Message, eose);
                        HandleMainLoopException(new ShutdownEventArgs(ShutdownInitiator.Library, 0, "End of stream", eose));
                        shouldTerminate = true;
                    }
                }

                if (shouldTerminate)
                {
                    TerminateMainloop();
                    FinishClose();
                }
                else
                {
                    _heartbeatReadTimer?.Change((int)Heartbeat.TotalMilliseconds, Timeout.Infinite);
                }
            }
            catch (ObjectDisposedException)
            {
                // timer is already disposed,
                // e.g. due to shutdown
            }
            catch (NullReferenceException)
            {
                // timer has already been disposed from a different thread after null check
                // this event should be rare
            }
        }

        private void HeartbeatWriteTimerCallback(object state)
        {
            if (_heartbeatWriteTimer is null)
            {
                return;
            }

            try
            {
                if (!_closed)
                {
                    Write(Client.Impl.Framing.Heartbeat.GetHeartbeatFrame());
                    _heartbeatWriteTimer?.Change((int)_heartbeatTimeSpan.TotalMilliseconds, Timeout.Infinite);
                }
            }
            catch (ObjectDisposedException)
            {
                // timer is already disposed,
                // e.g. due to shutdown
            }
            catch (Exception)
            {
                // ignore, let the read callback detect
                // peer unavailability. See rabbitmq/rabbitmq-dotnet-client#638 for details.
            }
        }

        private void MaybeStopHeartbeatTimers()
        {
            NotifyHeartbeatListener();
            _heartbeatReadTimer?.Dispose();
            _heartbeatWriteTimer?.Dispose();
        }

        ///<remarks>
        /// May be called more than once. Should therefore be idempotent.
        ///</remarks>
        private void TerminateMainloop()
        {
            MaybeStopHeartbeatTimers();
            _running = false;
        }

        public override string ToString()
        {
            return $"Connection({_id},{Endpoint})";
        }

        internal void Write(ReadOnlyMemory<byte> memory)
        {
            _frameHandler.Write(memory);
        }

        public void UpdateSecret(string newSecret, string reason)
        {
            _model0.UpdateSecret(newSecret, reason);
        }

        ///<summary>API-side invocation of connection.close with timeout.</summary>
        public void Close(ushort reasonCode, string reasonText, TimeSpan timeout, bool abort)
        {
            Close(new ShutdownEventArgs(ShutdownInitiator.Application, reasonCode, reasonText), abort, timeout);
        }

        public IModel CreateModel()
        {
            EnsureIsOpen();
            ISession session = CreateSession();
            var model = (ModelBase)Protocol.CreateModel(_factory, session);
            model.ContinuationTimeout = _factory.ContinuationTimeout;
            model._Private_ChannelOpen();
            return model;
        }

        internal void HandleConnectionBlocked(string reason)
        {
            if (!_connectionBlockedWrapper.IsEmpty)
            {
                _connectionBlockedWrapper.Invoke(this, new ConnectionBlockedEventArgs(reason));
            }
        }

        internal void HandleConnectionUnblocked()
        {
            if (!_connectionUnblockedWrapper.IsEmpty)
            {
                _connectionUnblockedWrapper.Invoke(this, EventArgs.Empty);
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
                this.Abort();
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

        private void StartAndTune()
        {
            var connectionStartCell = new BlockingCell<ConnectionStartDetails>();
            _model0.m_connectionStartCell = connectionStartCell;
            _model0.HandshakeContinuationTimeout = _factory.HandshakeContinuationTimeout;
            _frameHandler.ReadTimeout = _factory.HandshakeContinuationTimeout;
            _frameHandler.SendHeader();

            ConnectionStartDetails connectionStart = connectionStartCell.WaitForValue();

            if (connectionStart is null)
            {
                throw new IOException("connection.start was never received, likely due to a network timeout");
            }

            ServerProperties = connectionStart.m_serverProperties;

            var serverVersion = new AmqpVersion(connectionStart.m_versionMajor, connectionStart.m_versionMinor);
            if (!serverVersion.Equals(Protocol.Version))
            {
                TerminateMainloop();
                FinishClose();
                throw new ProtocolVersionMismatchException(Protocol.MajorVersion, Protocol.MinorVersion, serverVersion.Major, serverVersion.Minor);
            }

            ClientProperties = new Dictionary<string, object>(_factory.ClientProperties)
            {
                ["capabilities"] = Protocol.Capabilities,
                ["connection_name"] = ClientProvidedName
            };

            // FIXME: parse out locales properly!
            ConnectionTuneDetails connectionTune = default;
            bool tuned = false;
            try
            {
                string mechanismsString = Encoding.UTF8.GetString(connectionStart.m_mechanisms, 0, connectionStart.m_mechanisms.Length);
                string[] mechanisms = mechanismsString.Split(' ');
                IAuthMechanismFactory mechanismFactory = _factory.AuthMechanismFactory(mechanisms);
                if (mechanismFactory is null)
                {
                    throw new IOException($"No compatible authentication mechanism found - server offered [{mechanismsString}]");
                }
                IAuthMechanism mechanism = mechanismFactory.GetInstance();
                byte[] challenge = null;
                do
                {
                    byte[] response = mechanism.handleChallenge(challenge, _factory);
                    ConnectionSecureOrTune res;
                    if (challenge is null)
                    {
                        res = _model0.ConnectionStartOk(ClientProperties,
                            mechanismFactory.Name,
                            response,
                            "en_US");
                    }
                    else
                    {
                        res = _model0.ConnectionSecureOk(response);
                    }

                    if (res.m_challenge is null)
                    {
                        connectionTune = res.m_tuneDetails;
                        tuned = true;
                    }
                    else
                    {
                        challenge = res.m_challenge;
                    }
                }
                while (!tuned);
            }
            catch (OperationInterruptedException e)
            {
                if (e.ShutdownReason != null && e.ShutdownReason.ReplyCode == Constants.AccessRefused)
                {
                    throw new AuthenticationFailureException(e.ShutdownReason.ReplyText);
                }
                throw new PossibleAuthenticationFailureException(
                    "Possibly caused by authentication failure", e);
            }

            ushort channelMax = (ushort)NegotiatedMaxValue(_factory.RequestedChannelMax, connectionTune.m_channelMax);
            _sessionManager = new SessionManager(this, channelMax);

            uint frameMax = NegotiatedMaxValue(_factory.RequestedFrameMax, connectionTune.m_frameMax);
            FrameMax = frameMax;

            TimeSpan requestedHeartbeat = _factory.RequestedHeartbeat;
            uint heartbeatInSeconds = NegotiatedMaxValue((uint)requestedHeartbeat.TotalSeconds, (uint)connectionTune.m_heartbeatInSeconds);
            Heartbeat = TimeSpan.FromSeconds(heartbeatInSeconds);

            _model0.ConnectionTuneOk(channelMax, frameMax, (ushort)Heartbeat.TotalSeconds);

            // now we can start heartbeat timers
            MaybeStartHeartbeatTimers();
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        private static uint NegotiatedMaxValue(uint clientValue, uint serverValue)
        {
            return (clientValue == 0 || serverValue == 0) ?
                Math.Max(clientValue, serverValue) :
                Math.Min(clientValue, serverValue);
        }

        internal void TakeOver(Connection other)
        {
            _callbackExceptionWrapper.Takeover(other._callbackExceptionWrapper);
            _connectionBlockedWrapper.Takeover(other._connectionBlockedWrapper);
            _connectionUnblockedWrapper.Takeover(other._connectionUnblockedWrapper);
            _connectionShutdownWrapper.Takeover(other._connectionShutdownWrapper);
        }
    }
}
