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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
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
        private bool _disposed = false;
        private readonly object _eventLock = new object();

        ///<summary>Heartbeat frame for transmission. Reusable across connections.</summary>
        private readonly EmptyOutboundFrame _heartbeatFrame = new EmptyOutboundFrame();

        private readonly ManualResetEventSlim _appContinuation = new ManualResetEventSlim(false);

        private volatile ShutdownEventArgs _closeReason = null;
        private volatile bool _closed = false;

        private EventHandler<ShutdownEventArgs> _connectionShutdown;

        private readonly IConnectionFactory _factory;
        private readonly IFrameHandler _frameHandler;

        private Guid _id = Guid.NewGuid();
        private readonly ModelBase _model0;
        private volatile bool _running = true;
        private readonly MainSession _session0;
        private SessionManager _sessionManager;

        //
        // Heartbeats
        //

        private TimeSpan _heartbeat = TimeSpan.Zero;
        private TimeSpan _heartbeatTimeSpan = TimeSpan.FromSeconds(0);
        private int _missedHeartbeats = 0;

        private Timer _heartbeatWriteTimer;
        private Timer _heartbeatReadTimer;
        private readonly AutoResetEvent _heartbeatRead = new AutoResetEvent(false);

        private Task _mainLoopTask;

        private static readonly string s_version = typeof(Connection).Assembly
                                            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                            .InformationalVersion;

        // true if we haven't finished connection negotiation.
        // In this state socket exceptions are treated as fatal connection
        // errors, otherwise as read timeouts
        public ConsumerWorkService ConsumerWorkService { get; private set; }

        public Connection(IConnectionFactory factory, bool insist, IFrameHandler frameHandler, string clientProvidedName = null)
        {
            ClientProvidedName = clientProvidedName;
            KnownHosts = null;
            FrameMax = 0;
            _factory = factory;
            _frameHandler = frameHandler;

            if (factory is IAsyncConnectionFactory asyncConnectionFactory && asyncConnectionFactory.DispatchConsumersAsync)
            {
                ConsumerWorkService = new AsyncConsumerWorkService();
            }
            else
            {
                ConsumerWorkService = new ConsumerWorkService();
            }

            _sessionManager = new SessionManager(this, 0);
            _session0 = new MainSession(this) { Handler = NotifyReceivedCloseOk };
            _model0 = (ModelBase)Protocol.CreateModel(_session0);

            StartMainLoop();
            Open(insist);
        }

        public Guid Id { get { return _id; } }

        public event EventHandler<CallbackExceptionEventArgs> CallbackException;

        public event EventHandler<ConnectionBlockedEventArgs> ConnectionBlocked;

        public event EventHandler<ShutdownEventArgs> ConnectionShutdown
        {
            add
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                bool ok = false;
                lock (_eventLock)
                {
                    if (_closeReason == null)
                    {
                        _connectionShutdown += value;
                        ok = true;
                    }
                }
                if (!ok)
                {
                    value(this, _closeReason);
                }
            }
            remove
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                lock (_eventLock)
                {
                    _connectionShutdown -= value;
                }
            }
        }

        public event EventHandler<EventArgs> ConnectionUnblocked;


        public string ClientProvidedName { get; private set; }

        public ushort ChannelMax
        {
            get { return _sessionManager.ChannelMax; }
        }

        public IDictionary<string, object> ClientProperties { get; set; }

        public ShutdownEventArgs CloseReason
        {
            get { return _closeReason; }
        }

        public AmqpTcpEndpoint Endpoint
        {
            get { return _frameHandler.Endpoint; }
        }

        public uint FrameMax { get; set; }

        public TimeSpan Heartbeat
        {
            get { return _heartbeat; }
            set
            {
                _heartbeat = value;
                // timers fire at slightly below half the interval to avoid race
                // conditions
                _heartbeatTimeSpan = TimeSpan.FromMilliseconds(_heartbeat.TotalMilliseconds / 4);
                _frameHandler.ReadTimeout = TimeSpan.FromMilliseconds(_heartbeat.TotalMilliseconds * 2);
            }
        }

        public bool IsOpen
        {
            get { return CloseReason == null; }
        }

        public AmqpTcpEndpoint[] KnownHosts { get; set; }

        public EndPoint LocalEndPoint
        {
            get { return _frameHandler.LocalEndPoint; }
        }

        public int LocalPort
        {
            get { return _frameHandler.LocalPort; }
        }

        ///<summary>Another overload of a Protocol property, useful
        ///for exposing a tighter type.</summary>
        public ProtocolBase Protocol
        {
            get { return (ProtocolBase)Endpoint.Protocol; }
        }

        public EndPoint RemoteEndPoint
        {
            get { return _frameHandler.RemoteEndPoint; }
        }

        public int RemotePort
        {
            get { return _frameHandler.RemotePort; }
        }

        public IDictionary<string, object> ServerProperties { get; set; }

        public IList<ShutdownReportEntry> ShutdownReport { get; } = new SynchronizedList<ShutdownReportEntry>(new List<ShutdownReportEntry>());

        ///<summary>Explicit implementation of IConnection.Protocol.</summary>
        IProtocol IConnection.Protocol
        {
            get { return Endpoint.Protocol; }
        }

        public static IDictionary<string, object> DefaultClientProperties()
        {
            IDictionary<string, object> table = new Dictionary<string, object>
            {
                ["product"] = Encoding.UTF8.GetBytes("RabbitMQ"),
                ["version"] = Encoding.UTF8.GetBytes(s_version),
                ["platform"] = Encoding.UTF8.GetBytes(".NET"),
                ["copyright"] = Encoding.UTF8.GetBytes("Copyright (c) 2007-2020 VMware, Inc."),
                ["information"] = Encoding.UTF8.GetBytes("Licensed under the MPL. See https://www.rabbitmq.com/")
            };
            return table;
        }

        public void Abort(ushort reasonCode, string reasonText, ShutdownInitiator initiator, TimeSpan timeout)
        {
            Close(new ShutdownEventArgs(initiator, reasonCode, reasonText), true, timeout);
        }

        public void Close(ShutdownEventArgs reason)
        {
            Close(reason, false, Timeout.InfiniteTimeSpan);
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
        public void Close(ShutdownEventArgs reason, bool abort, TimeSpan timeout)
        {
            if (!SetCloseReason(reason))
            {
                if (!abort)
                {
                    throw new AlreadyClosedException(_closeReason);
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
                    _session0.Transmit(ConnectionCloseWrapper(reason.ReplyCode, reason.ReplyText));
                }
                catch (AlreadyClosedException ace)
                {
                    if (!abort)
                    {
                        throw ace;
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
                    if (_model0.CloseReason == null)
                    {
                        if (!abort)
                        {
                            throw ioe;
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

            bool receivedSignal = _appContinuation.Wait(timeout);
            if (!receivedSignal)
            {
                _frameHandler.Close();
            }
        }

        ///<remarks>
        /// Loop only used while quiescing. Use only to cleanly close connection
        ///</remarks>
        public void ClosingLoop()
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
                if (_model0.CloseReason == null)
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

        public Command ConnectionCloseWrapper(ushort reasonCode, string reasonText)
        {
            Protocol.CreateConnectionClose(reasonCode, reasonText, out Command request, out _, out _);
            return request;
        }

        public ISession CreateSession()
        {
            return _sessionManager.Create();
        }

        public ISession CreateSession(int channelNumber)
        {
            return _sessionManager.Create(channelNumber);
        }

        public void EnsureIsOpen()
        {
            if (!IsOpen)
            {
                throw new AlreadyClosedException(CloseReason);
            }
        }

        // Only call at the end of the Mainloop or HeartbeatLoop
        public void FinishClose()
        {
            _closed = true;
            MaybeStopHeartbeatTimers();

            _frameHandler.Close();
            _model0.SetCloseReason(_closeReason);
            _model0.FinishClose();
        }

        /// <remarks>
        /// We need to close the socket, otherwise attempting to unload the domain
        /// could cause a CannotUnloadAppDomainException
        /// </remarks>
        public void HandleDomainUnload(object sender, EventArgs ea)
        {
            Abort(Constants.InternalError, "Domain Unload");
        }

        public void HandleMainLoopException(ShutdownEventArgs reason)
        {
            if (!SetCloseReason(reason))
            {
                LogCloseError("Unexpected Main Loop Exception while closing: "
                              + reason, new Exception(reason.ToString()));
                return;
            }

            OnShutdown();
            LogCloseError($"Unexpected connection closure: {reason}", new Exception(reason.ToString()));
        }

        public bool HardProtocolExceptionHandler(HardProtocolException hpe)
        {
            if (SetCloseReason(hpe.ShutdownReason))
            {
                OnShutdown();
                _session0.SetSessionClosing(false);
                try
                {
                    _session0.Transmit(ConnectionCloseWrapper(
                        hpe.ShutdownReason.ReplyCode,
                        hpe.ShutdownReason.ReplyText));
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

        public void InternalClose(ShutdownEventArgs reason)
        {
            if (!SetCloseReason(reason))
            {
                if (_closed)
                {
                    throw new AlreadyClosedException(_closeReason);
                }
                // We are quiescing, but still allow for server-close
            }

            OnShutdown();
            _session0.SetSessionClosing(true);
            TerminateMainloop();
        }

        public void LogCloseError(string error, Exception ex)
        {
            ESLog.Error(error, ex);
            ShutdownReport.Add(new ShutdownReportEntry(error, ex));
        }

        public void MainLoop()
        {
            try
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
            finally
            {
                _appContinuation.Set();
            }
        }

        public void MainLoopIteration()
        {
            using (InboundFrame frame = _frameHandler.ReadFrame())
            {
                NotifyHeartbeatListener();
                // We have received an actual frame.
                if (frame.IsHeartbeat())
                {
                    // Ignore it: we've already just reset the heartbeat
                    // latch.
                    return;
                }

                if (frame.Channel == 0)
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
                    _session0.HandleFrame(in frame);
                }
                else
                {
                    // If we're still m_running, but have a m_closeReason,
                    // then we must be quiescing, which means any inbound
                    // frames for non-zero channels (and any inbound
                    // commands on channel zero that aren't
                    // Connection.CloseOk) must be discarded.
                    if (_closeReason == null)
                    {
                        // No close reason, not quiescing the
                        // connection. Handle the frame. (Of course, the
                        // Session itself may be quiescing this particular
                        // channel, but that's none of our concern.)
                        ISession session = _sessionManager.Lookup(frame.Channel);
                        if (session == null)
                        {
                            throw new ChannelErrorException(frame.Channel);
                        }
                        else
                        {
                            session.HandleFrame(in frame);
                        }
                    }
                }
            }
        }

        public void NotifyHeartbeatListener()
        {
            if (_heartbeat != TimeSpan.Zero)
            {
                _heartbeatRead.Set();
            }
        }

        public void NotifyReceivedCloseOk()
        {
            TerminateMainloop();
            _closed = true;
        }

        public void OnCallbackException(CallbackExceptionEventArgs args)
        {
            foreach (EventHandler<CallbackExceptionEventArgs> h in CallbackException?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try
                {
                    h(this, args);
                }
                catch
                {
                    // Exception in
                    // Callback-exception-handler. That was the
                    // app's last chance. Swallow the exception.
                    // FIXME: proper logging
                }
            }
        }

        public void OnConnectionBlocked(ConnectionBlockedEventArgs args)
        {
            foreach (EventHandler<ConnectionBlockedEventArgs> h in ConnectionBlocked?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try
                {
                    h(this, args);
                }
                catch (Exception e)
                {
                    OnCallbackException(CallbackExceptionEventArgs.Build(e, new Dictionary<string, object> { { "context", "OnConnectionBlocked" } }));
                }
            }
        }

        public void OnConnectionUnblocked()
        {
            foreach (EventHandler<EventArgs> h in ConnectionUnblocked?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try
                {
                    h(this, EventArgs.Empty);
                }
                catch (Exception e)
                {
                    OnCallbackException(CallbackExceptionEventArgs.Build(e, new Dictionary<string, object> { { "context", "OnConnectionUnblocked" } }));
                }
            }
        }

        ///<summary>Broadcasts notification of the final shutdown of the connection.</summary>
        public void OnShutdown()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            EventHandler<ShutdownEventArgs> handler;
            ShutdownEventArgs reason;
            lock (_eventLock)
            {
                handler = _connectionShutdown;
                reason = _closeReason;
                _connectionShutdown = null;
            }
            if (handler != null)
            {
                foreach (EventHandler<ShutdownEventArgs> h in handler.GetInvocationList())
                {
                    try
                    {
                        h(this, reason);
                    }
                    catch (Exception e)
                    {
                        OnCallbackException(CallbackExceptionEventArgs.Build(e,
                            new Dictionary<string, object>
                            {
                                {"context", "OnShutdown"}
                            }));
                    }
                }
            }
        }

        public void Open(bool insist)
        {
            StartAndTune();
            _model0.ConnectionOpen(_factory.VirtualHost, string.Empty, false);
        }

        public void PrettyPrintShutdownReport()
        {
            if (ShutdownReport.Count == 0)
            {
                Console.Error.WriteLine(
"No errors reported when closing connection {0}", this);
            }
            else
            {
                Console.Error.WriteLine(
"Log of errors while closing connection {0}:", this);
                foreach (ShutdownReportEntry entry in ShutdownReport)
                {
                    Console.Error.WriteLine(
entry.ToString());
                }
            }
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
        public void QuiesceChannel(SoftProtocolException pe)
        {
            // Construct the QuiescingSession that we'll use during
            // the quiesce process.

            ISession newSession = new QuiescingSession(this,
                pe.Channel,
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
            newSession.Transmit(ChannelCloseWrapper(pe.ReplyCode, pe.Message));
        }

        public bool SetCloseReason(ShutdownEventArgs reason)
        {
            lock (_eventLock)
            {
                if (_closeReason == null)
                {
                    _closeReason = reason;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public void MaybeStartHeartbeatTimers()
        {
            if (Heartbeat != TimeSpan.Zero)
            {
                if (_heartbeatWriteTimer == null)
                {
                    _heartbeatWriteTimer = new Timer(HeartbeatWriteTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
                    _heartbeatWriteTimer.Change(200, Timeout.Infinite);
                }

                if (_heartbeatReadTimer == null)
                {
                    _heartbeatReadTimer = new Timer(HeartbeatReadTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
                    _heartbeatReadTimer.Change(300, Timeout.Infinite);
                }
            }
        }

        public void StartMainLoop()
        {
            _mainLoopTask = Task.Run((Action)MainLoop);
        }

        public void HeartbeatReadTimerCallback(object state)
        {
            if (_heartbeatReadTimer == null)
            {
                return;
            }

            bool shouldTerminate = false;

            try
            {
                if (!_closed)
                {
                    if (!_heartbeatRead.WaitOne(0))
                    {
                        _missedHeartbeats++;
                    }
                    else
                    {
                        _missedHeartbeats = 0;
                    }

                    // We check against 8 = 2 * 4 because we need to wait for at
                    // least two complete heartbeat setting intervals before
                    // complaining, and we've set the socket timeout to a quarter
                    // of the heartbeat setting in setHeartbeat above.
                    if (_missedHeartbeats > 2 * 4)
                    {
                        string description = string.Format("Heartbeat missing with heartbeat == {0} seconds", _heartbeat);
                        var eose = new EndOfStreamException(description);
                        ESLog.Error(description, eose);
                        ShutdownReport.Add(new ShutdownReportEntry(description, eose));
                        HandleMainLoopException(
                            new ShutdownEventArgs(ShutdownInitiator.Library, 0, "End of stream", eose));
                        shouldTerminate = true;
                    }
                }

                if (shouldTerminate)
                {
                    TerminateMainloop();
                    FinishClose();
                }
                else if (_heartbeatReadTimer != null)
                {
                    _heartbeatReadTimer.Change((int)Heartbeat.TotalMilliseconds, Timeout.Infinite);
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

        public void HeartbeatWriteTimerCallback(object state)
        {
            if (_heartbeatWriteTimer == null)
            {
                return;
            }

            try
            {
                if (!_closed)
                {
                    WriteFrame(_heartbeatFrame);
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

        void MaybeStopHeartbeatTimers()
        {
            NotifyHeartbeatListener();
            _heartbeatReadTimer?.Dispose();
            _heartbeatWriteTimer?.Dispose();
        }

        ///<remarks>
        /// May be called more than once. Should therefore be idempotent.
        ///</remarks>
        public void TerminateMainloop()
        {
            MaybeStopHeartbeatTimers();
            _running = false;
        }

        public override string ToString()
        {
            return string.Format("Connection({0},{1})", _id, Endpoint);
        }

        public void WriteFrame(OutboundFrame f)
        {
            _frameHandler.WriteFrame(f);
        }

        public void WriteFrameSet(IList<OutboundFrame> f)
        {
            _frameHandler.WriteFrameSet(f);
        }

        public void UpdateSecret(string newSecret, string reason)
        {
            _model0.UpdateSecret(newSecret, reason);
        }

        ///<summary>API-side invocation of connection abort.</summary>
        public void Abort()
        {
            Abort(Timeout.InfiniteTimeSpan);
        }

        ///<summary>API-side invocation of connection abort.</summary>
        public void Abort(ushort reasonCode, string reasonText)
        {
            Abort(reasonCode, reasonText, Timeout.InfiniteTimeSpan);
        }

        ///<summary>API-side invocation of connection abort with timeout.</summary>
        public void Abort(TimeSpan timeout)
        {
            Abort(Constants.ReplySuccess, "Connection close forced", timeout);
        }

        ///<summary>API-side invocation of connection abort with timeout.</summary>
        public void Abort(ushort reasonCode, string reasonText, TimeSpan timeout)
        {
            Abort(reasonCode, reasonText, ShutdownInitiator.Application, timeout);
        }

        ///<summary>API-side invocation of connection.close.</summary>
        public void Close()
        {
            Close(Constants.ReplySuccess, "Goodbye", Timeout.InfiniteTimeSpan);
        }

        ///<summary>API-side invocation of connection.close.</summary>
        public void Close(ushort reasonCode, string reasonText)
        {
            Close(reasonCode, reasonText, Timeout.InfiniteTimeSpan);
        }

        ///<summary>API-side invocation of connection.close with timeout.</summary>
        public void Close(TimeSpan timeout)
        {
            Close(Constants.ReplySuccess, "Goodbye", timeout);
        }

        ///<summary>API-side invocation of connection.close with timeout.</summary>
        public void Close(ushort reasonCode, string reasonText, TimeSpan timeout)
        {
            Close(new ShutdownEventArgs(ShutdownInitiator.Application, reasonCode, reasonText), false, timeout);
        }

        public IModel CreateModel()
        {
            EnsureIsOpen();
            ISession session = CreateSession();
            var model = (IFullModel)Protocol.CreateModel(session, ConsumerWorkService);
            model.ContinuationTimeout = _factory.ContinuationTimeout;
            model._Private_ChannelOpen("");
            return model;
        }

        public void HandleConnectionBlocked(string reason)
        {
            var args = new ConnectionBlockedEventArgs(reason);
            OnConnectionBlocked(args);
        }

        public void HandleConnectionUnblocked()
        {
            OnConnectionUnblocked();
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // dispose managed resources
                try
                {
                    Abort();
                    _mainLoopTask.Wait();
                }
                catch (OperationInterruptedException)
                {
                    // ignored, see rabbitmq/rabbitmq-dotnet-client#133
                }
                finally
                {
                    _connectionShutdown = null;
                    _disposed = true;
                }
            }

            // dispose unmanaged resources
        }

        Command ChannelCloseWrapper(ushort reasonCode, string reasonText)
        {
            Protocol.CreateChannelClose(reasonCode, reasonText, out Command request, out _, out _);
            return request;
        }

        void StartAndTune()
        {
            var connectionStartCell = new BlockingCell<ConnectionStartDetails>();
            _model0.m_connectionStartCell = connectionStartCell;
            _model0.HandshakeContinuationTimeout = _factory.HandshakeContinuationTimeout;
            _frameHandler.ReadTimeout = _factory.HandshakeContinuationTimeout;
            _frameHandler.SendHeader();

            ConnectionStartDetails connectionStart = connectionStartCell.WaitForValue();

            if (connectionStart == null)
            {
                throw new IOException("connection.start was never received, likely due to a network timeout");
            }

            ServerProperties = connectionStart.m_serverProperties;

            var serverVersion = new AmqpVersion(connectionStart.m_versionMajor,
                connectionStart.m_versionMinor);
            if (!serverVersion.Equals(Protocol.Version))
            {
                TerminateMainloop();
                FinishClose();
                throw new ProtocolVersionMismatchException(Protocol.MajorVersion,
                    Protocol.MinorVersion,
                    serverVersion.Major,
                    serverVersion.Minor);
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
                if (mechanismFactory == null)
                {
                    throw new IOException($"No compatible authentication mechanism found - server offered [{mechanismsString}]");
                }
                IAuthMechanism mechanism = mechanismFactory.GetInstance();
                byte[] challenge = null;
                do
                {
                    byte[] response = mechanism.handleChallenge(challenge, _factory);
                    ConnectionSecureOrTune res;
                    if (challenge == null)
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

                    if (res.m_challenge == null)
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

            ushort channelMax = (ushort)NegotiatedMaxValue(_factory.RequestedChannelMax,
                connectionTune.m_channelMax);
            _sessionManager = new SessionManager(this, channelMax);

            uint frameMax = NegotiatedMaxValue(_factory.RequestedFrameMax,
                connectionTune.m_frameMax);
            FrameMax = frameMax;

            TimeSpan requestedHeartbeat = _factory.RequestedHeartbeat;
            uint heartbeatInSeconds = NegotiatedMaxValue((uint)requestedHeartbeat.TotalSeconds,
                (uint)connectionTune.m_heartbeatInSeconds);
            Heartbeat = TimeSpan.FromSeconds(heartbeatInSeconds);

            _model0.ConnectionTuneOk(channelMax, frameMax, (ushort)Heartbeat.TotalSeconds);

            // now we can start heartbeat timers
            MaybeStartHeartbeatTimers();
        }

        private static uint NegotiatedMaxValue(uint clientValue, uint serverValue)
        {
            return (clientValue == 0 || serverValue == 0) ?
                Math.Max(clientValue, serverValue) :
                Math.Min(clientValue, serverValue);
        }
    }
}
