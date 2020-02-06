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
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Impl;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Framing.Impl
{
    public class Connection : IConnection
    {
        private readonly object m_eventLock = new object();

        ///<summary>Heartbeat frame for transmission. Reusable across connections.</summary>
        private readonly EmptyOutboundFrame m_heartbeatFrame = new EmptyOutboundFrame();
        private readonly CancellationTokenSource _connectionCancellationToken = new CancellationTokenSource();

        private ManualResetEvent m_appContinuation = new ManualResetEvent(false);
        private EventHandler<CallbackExceptionEventArgs> m_callbackException;
        private EventHandler<EventArgs> m_recoverySucceeded;
        private EventHandler<ConnectionRecoveryErrorEventArgs> connectionRecoveryFailure;

        private IDictionary<string, object> m_clientProperties;

        private volatile ShutdownEventArgs m_closeReason = null;
        private volatile bool m_closed = false;

        private EventHandler<ConnectionBlockedEventArgs> m_connectionBlocked;
        private EventHandler<ShutdownEventArgs> m_connectionShutdown;
        private EventHandler<EventArgs> m_connectionUnblocked;

        private IConnectionFactory m_factory;
        private IFrameHandler m_frameHandler;

        private Guid m_id = Guid.NewGuid();
        private ModelBase m_model0;
        private volatile bool m_running = true;
        private MainSession m_session0;
        private SessionManager m_sessionManager;

        private IList<ShutdownReportEntry> m_shutdownReport = new SynchronizedList<ShutdownReportEntry>(new List<ShutdownReportEntry>());

        //
        // Heartbeats
        //

        private TimeSpan m_heartbeat = TimeSpan.Zero;
        private TimeSpan m_heartbeatTimeSpan = TimeSpan.FromSeconds(0);
        private int m_missedHeartbeats = 0;

        private Task _heartbeatWriteTask;
        private Task _heartbeatReadTask;
        private AutoResetEvent m_heartbeatRead = new AutoResetEvent(false);

        private Task _mainLoopTask;

        private static string version = typeof(Connection).Assembly
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
            m_factory = factory;
            m_frameHandler = frameHandler;

            var asyncConnectionFactory = factory as IAsyncConnectionFactory;
            if (asyncConnectionFactory != null && asyncConnectionFactory.DispatchConsumersAsync)
            {
                ConsumerWorkService = new AsyncConsumerWorkService();
            }
            else
            {
                ConsumerWorkService = new ConsumerWorkService();
            }

            m_sessionManager = new SessionManager(this, 0);
            m_session0 = new MainSession(this) { Handler = NotifyReceivedCloseOk };
            m_model0 = (ModelBase)Protocol.CreateModel(m_session0);

            StartMainLoop(factory.UseBackgroundThreadsForIO);
            Open(insist);
        }

        public Guid Id { get { return m_id; } }

        public event EventHandler<EventArgs> RecoverySucceeded
        {
            add
            {
                lock (m_eventLock)
                {
                    m_recoverySucceeded += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_recoverySucceeded -= value;
                }
            }
        }

        public event EventHandler<CallbackExceptionEventArgs> CallbackException
        {
            add
            {
                lock (m_eventLock)
                {
                    m_callbackException += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_callbackException -= value;
                }
            }
        }

        public event EventHandler<ConnectionBlockedEventArgs> ConnectionBlocked
        {
            add
            {
                lock (m_eventLock)
                {
                    m_connectionBlocked += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_connectionBlocked -= value;
                }
            }
        }

        public event EventHandler<ShutdownEventArgs> ConnectionShutdown
        {
            add
            {
                bool ok = false;
                lock (m_eventLock)
                {
                    if (m_closeReason == null)
                    {
                        m_connectionShutdown += value;
                        ok = true;
                    }
                }
                if (!ok)
                {
                    value(this, m_closeReason);
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_connectionShutdown -= value;
                }
            }
        }

        public event EventHandler<EventArgs> ConnectionUnblocked
        {
            add
            {
                lock (m_eventLock)
                {
                    m_connectionUnblocked += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_connectionUnblocked -= value;
                }
            }
        }

        public event EventHandler<ConnectionRecoveryErrorEventArgs> ConnectionRecoveryError
        {
            add
            {
                lock (m_eventLock)
                {
                    connectionRecoveryFailure += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    connectionRecoveryFailure -= value;
                }
            }
        }

        public string ClientProvidedName { get; private set; }

        public ushort ChannelMax
        {
            get { return m_sessionManager.ChannelMax; }
        }

        public IDictionary<string, object> ClientProperties
        {
            get { return m_clientProperties; }
            set { m_clientProperties = value; }
        }

        public ShutdownEventArgs CloseReason
        {
            get { return m_closeReason; }
        }

        public AmqpTcpEndpoint Endpoint
        {
            get { return m_frameHandler.Endpoint; }
        }

        public uint FrameMax { get; set; }

        public TimeSpan Heartbeat
        {
            get { return m_heartbeat; }
            set
            {
                m_heartbeat = value;
                // timers fire at slightly below half the interval to avoid race
                // conditions
                m_heartbeatTimeSpan = TimeSpan.FromMilliseconds(m_heartbeat.TotalMilliseconds / 4);
                m_frameHandler.ReadTimeout = TimeSpan.FromMilliseconds(m_heartbeat.TotalMilliseconds * 2);
            }
        }

        public bool IsOpen
        {
            get { return CloseReason == null; }
        }

        public AmqpTcpEndpoint[] KnownHosts { get; set; }

        public EndPoint LocalEndPoint
        {
            get { return m_frameHandler.LocalEndPoint; }
        }

        public int LocalPort
        {
            get { return m_frameHandler.LocalPort; }
        }

        ///<summary>Another overload of a Protocol property, useful
        ///for exposing a tighter type.</summary>
        public ProtocolBase Protocol
        {
            get { return (ProtocolBase)Endpoint.Protocol; }
        }

        public EndPoint RemoteEndPoint
        {
            get { return m_frameHandler.RemoteEndPoint; }
        }

        public int RemotePort
        {
            get { return m_frameHandler.RemotePort; }
        }

        public IDictionary<string, object> ServerProperties { get; set; }

        public IList<ShutdownReportEntry> ShutdownReport
        {
            get { return m_shutdownReport; }
        }

        ///<summary>Explicit implementation of IConnection.Protocol.</summary>
        IProtocol IConnection.Protocol
        {
            get { return Endpoint.Protocol; }
        }

        public static IDictionary<string, object> DefaultClientProperties()
        {
            IDictionary<string, object> table = new Dictionary<string, object>();
            table["product"] = Encoding.UTF8.GetBytes("RabbitMQ");
            table["version"] = Encoding.UTF8.GetBytes(version);
            table["platform"] = Encoding.UTF8.GetBytes(".NET");
            table["copyright"] = Encoding.UTF8.GetBytes("Copyright (c) 2007-2020 VMware, Inc.");
            table["information"] = Encoding.UTF8.GetBytes("Licensed under the MPL.  " +
                                                          "See https://www.rabbitmq.com/");
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
                    throw new AlreadyClosedException(m_closeReason);
                }
            }
            else
            {
                OnShutdown();
                m_session0.SetSessionClosing(false);

                try
                {
                    // Try to send connection.close
                    // Wait for CloseOk in the MainLoop
                    m_session0.Transmit(ConnectionCloseWrapper(reason.ReplyCode, reason.ReplyText));
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
                    if (m_model0.CloseReason == null)
                    {
                        if (!abort)
                        {
                            throw ioe;
                        }
                        else
                        {
                            LogCloseError("Couldn't close connection cleanly. "
                                          + "Socket closed unexpectedly", ioe);
                        }
                    }
                }
                finally
                {
                    TerminateMainloop();
                }
            }

            var receivedSignal = m_appContinuation.WaitOne(timeout);

            if (!receivedSignal)
            {
                m_frameHandler.Close();
            }
        }

        ///<remarks>
        /// Loop only used while quiescing. Use only to cleanly close connection
        ///</remarks>
        public async Task ClosingLoop()
        {
            try
            {
                m_frameHandler.ReadTimeout = TimeSpan.Zero;
                // Wait for response/socket closure or timeout
                while (!m_closed)
                {
                    await MainLoopIteration();
                }
            }
            catch (ObjectDisposedException ode)
            {
                if (!m_closed)
                {
                    LogCloseError("Connection didn't close cleanly", ode);
                }
            }
            catch (EndOfStreamException eose)
            {
                if (m_model0.CloseReason == null)
                {
                    LogCloseError("Connection didn't close cleanly. "
                                  + "Socket closed unexpectedly", eose);
                }
            }
            catch (IOException ioe)
            {
                LogCloseError("Connection didn't close cleanly. "
                              + "Socket closed unexpectedly", ioe);
            }
            catch (Exception e)
            {
                LogCloseError("Unexpected exception while closing: ", e);
            }
        }

        public Command ConnectionCloseWrapper(ushort reasonCode, string reasonText)
        {
            Command request;
            ushort replyClassId;
            ushort replyMethodId;
            Protocol.CreateConnectionClose(reasonCode,
                reasonText,
                out request,
                out replyClassId,
                out replyMethodId);
            return request;
        }

        public ISession CreateSession()
        {
            return m_sessionManager.Create();
        }

        public ISession CreateSession(int channelNumber)
        {
            return m_sessionManager.Create(channelNumber);
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
            m_closed = true;
            MaybeStopHeartbeatTimers();

            m_frameHandler.Close();
            m_model0.SetCloseReason(m_closeReason);
            m_model0.FinishClose();
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
            LogCloseError("Unexpected connection closure: " + reason, new Exception(reason.ToString()));
        }

        public bool HardProtocolExceptionHandler(HardProtocolException hpe)
        {
            if (SetCloseReason(hpe.ShutdownReason))
            {
                OnShutdown();
                m_session0.SetSessionClosing(false);
                try
                {
                    m_session0.Transmit(ConnectionCloseWrapper(
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
                LogCloseError("Hard Protocol Exception occured "
                              + "while closing the connection", hpe);
            }

            return false;
        }

        public void InternalClose(ShutdownEventArgs reason)
        {
            if (!SetCloseReason(reason))
            {
                if (m_closed)
                {
                    throw new AlreadyClosedException(m_closeReason);
                }
                // We are quiescing, but still allow for server-close
            }

            OnShutdown();
            m_session0.SetSessionClosing(true);
            TerminateMainloop();
        }

        public void LogCloseError(String error, Exception ex)
        {
            ESLog.Error(error, ex);
            m_shutdownReport.Add(new ShutdownReportEntry(error, ex));
        }

        public async Task MainLoop()
        {
            try
            {
                bool shutdownCleanly = false;
                try
                {
                    while (m_running)
                    {
                        try
                        {
                            await MainLoopIteration();
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
                        await ClosingLoop();
                    }
                    catch (SocketException se)
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
                m_appContinuation.Set();
            }
        }

        public async Task MainLoopIteration()
        {
            using (InboundFrame frame = await m_frameHandler.ReadFrameAsync().ConfigureAwait(false))
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
                    m_session0.HandleFrame(frame);
                }
                else
                {
                    // If we're still m_running, but have a m_closeReason,
                    // then we must be quiescing, which means any inbound
                    // frames for non-zero channels (and any inbound
                    // commands on channel zero that aren't
                    // Connection.CloseOk) must be discarded.
                    if (m_closeReason == null)
                    {
                        // No close reason, not quiescing the
                        // connection. Handle the frame. (Of course, the
                        // Session itself may be quiescing this particular
                        // channel, but that's none of our concern.)
                        ISession session = m_sessionManager.Lookup(frame.Channel);
                        if (session == null)
                        {
                            throw new ChannelErrorException(frame.Channel);
                        }
                        else
                        {
                            session.HandleFrame(frame);
                        }
                    }
                }
            }
        }

        public void NotifyHeartbeatListener()
        {
            if (m_heartbeat != TimeSpan.Zero)
            {
                m_heartbeatRead.Set();
            }
        }

        public void NotifyReceivedCloseOk()
        {
            TerminateMainloop();
            m_closed = true;
        }

        public void OnCallbackException(CallbackExceptionEventArgs args)
        {
            EventHandler<CallbackExceptionEventArgs> handler;
            lock (m_eventLock)
            {
                handler = m_callbackException;
            }
            if (handler != null)
            {
                foreach (EventHandler<CallbackExceptionEventArgs> h in handler.GetInvocationList())
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
        }

        public void OnConnectionBlocked(ConnectionBlockedEventArgs args)
        {
            EventHandler<ConnectionBlockedEventArgs> handler;
            lock (m_eventLock)
            {
                handler = m_connectionBlocked;
            }
            if (handler != null)
            {
                foreach (EventHandler<ConnectionBlockedEventArgs> h in handler.GetInvocationList())
                {
                    try
                    {
                        h(this, args);
                    }
                    catch (Exception e)
                    {
                        OnCallbackException(CallbackExceptionEventArgs.Build(e,
                            new Dictionary<string, object>
                            {
                                {"context", "OnConnectionBlocked"}
                            }));
                    }
                }
            }
        }

        public void OnConnectionUnblocked()
        {
            EventHandler<EventArgs> handler;
            lock (m_eventLock)
            {
                handler = m_connectionUnblocked;
            }
            if (handler != null)
            {
                foreach (EventHandler<EventArgs> h in handler.GetInvocationList())
                {
                    try
                    {
                        h(this, EventArgs.Empty);
                    }
                    catch (Exception e)
                    {
                        OnCallbackException(CallbackExceptionEventArgs.Build(e,
                            new Dictionary<string, object>
                            {
                                {"context", "OnConnectionUnblocked"}
                            }));
                    }
                }
            }
        }

        ///<summary>Broadcasts notification of the final shutdown of the connection.</summary>
        public void OnShutdown()
        {
            EventHandler<ShutdownEventArgs> handler;
            ShutdownEventArgs reason;
            lock (m_eventLock)
            {
                handler = m_connectionShutdown;
                reason = m_closeReason;
                m_connectionShutdown = null;
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
            m_model0.ConnectionOpen(m_factory.VirtualHost, String.Empty, false);
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
            ISession oldSession = m_sessionManager.Swap(pe.Channel, newSession);

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
            lock (m_eventLock)
            {
                if (m_closeReason == null)
                {
                    m_closeReason = reason;
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
                _heartbeatWriteTask = Task.Run((Func<ValueTask>)HeartbeatWriteTimerCallback, _connectionCancellationToken.Token);
                _heartbeatReadTask = Task.Run((Func<ValueTask>)HeartbeatReadTimerCallback, _connectionCancellationToken.Token);
            }
        }

        public void StartMainLoop(bool useBackgroundThread)
        {
            _mainLoopTask = Task.Run(MainLoop);
        }

        public async ValueTask HeartbeatReadTimerCallback()
        {
            try
            {
                await Task.Delay(200, _connectionCancellationToken.Token).ConfigureAwait(false);
                while (!_connectionCancellationToken.IsCancellationRequested)
                {
                    bool shouldTerminate = false;

                    if (!m_closed)
                    {
                        if (!m_heartbeatRead.WaitOne(0))
                        {
                            Debug.WriteLine("Heartbeat missed!");
                            m_missedHeartbeats++;
                        }
                        else
                        {
                            Debug.WriteLine("Heartbeat check succeeded!");
                            m_missedHeartbeats = 0;
                            m_heartbeatRead.Reset();
                        }

                        // We check against 8 = 2 * 4 because we need to wait for at
                        // least two complete heartbeat setting intervals before
                        // complaining, and we've set the socket timeout to a quarter
                        // of the heartbeat setting in setHeartbeat above.
                        if (m_missedHeartbeats > 2 * 4)
                        {
                            string description = $"Heartbeat missing with heartbeat == {m_heartbeat} seconds";
                            var eose = new EndOfStreamException(description);
                            ESLog.Error(description, eose);
                            m_shutdownReport.Add(new ShutdownReportEntry(description, eose));
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
                    else
                    {
                        await Task.Delay((int)Heartbeat.TotalMilliseconds, _connectionCancellationToken.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // Let's swallow the exception when the connection is being closed
            }
        }

        public async ValueTask HeartbeatWriteTimerCallback()
        {
            try
            {
                await Task.Delay(200).ConfigureAwait(false);
                while (!_connectionCancellationToken.IsCancellationRequested)
                {
                    WriteFrame(m_heartbeatFrame);
                    await Task.Delay(m_heartbeatTimeSpan, _connectionCancellationToken.Token);
                }
            }
            catch (TaskCanceledException)
            {
                // Do nothing when the connection is being closed
            }
        }

        void MaybeStopHeartbeatTimers()
        {
            NotifyHeartbeatListener();
        }

        ///<remarks>
        /// May be called more than once. Should therefore be idempotent.
        ///</remarks>
        public void TerminateMainloop()
        {
            MaybeStopHeartbeatTimers();
            m_running = false;
        }

        public override string ToString()
        {
            return string.Format("Connection({0},{1})", m_id, Endpoint);
        }

        public void WriteFrame(OutboundFrame f)
        {
            m_frameHandler.WriteFrame(f);
        }

        public void WriteFrameSet(IList<OutboundFrame> f)
        {
            m_frameHandler.WriteFrameSet(f);
        }

        public void UpdateSecret(string newSecret, string reason)
        {
            m_model0.UpdateSecret(newSecret, reason);
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
            var model = (IFullModel)Protocol.CreateModel(session, this.ConsumerWorkService);
            model.ContinuationTimeout = m_factory.ContinuationTimeout;
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
                catch (AggregateException ae) when (ae.Flatten().InnerException is ChannelClosedException)
                {

                }
                finally
                {
                    m_callbackException = null;
                    m_recoverySucceeded = null;
                    m_connectionShutdown = null;
                    m_connectionUnblocked = null;
                }
            }

            // dispose unmanaged resources
        }

        Command ChannelCloseWrapper(ushort reasonCode, string reasonText)
        {
            Command request;
            ushort replyClassId;
            ushort replyMethodId;
            Protocol.CreateChannelClose(reasonCode,
                reasonText,
                out request,
                out replyClassId,
                out replyMethodId);
            return request;
        }

        void StartAndTune()
        {
            m_model0.m_connectionStartCell = new TaskCompletionSource<ConnectionStartDetails>(TaskCreationOptions.RunContinuationsAsynchronously);
            m_model0.HandshakeContinuationTimeout = m_factory.HandshakeContinuationTimeout;
            m_frameHandler.ReadTimeout = m_factory.HandshakeContinuationTimeout;
            m_frameHandler.SendHeader();
            try
            {
                m_model0.m_connectionStartCell.Task.Wait(m_factory.HandshakeContinuationTimeout);
            }
            catch (Exception)
            {
                m_model0.m_connectionStartCell.TrySetCanceled();
            }

            if (m_model0.m_connectionStartCell.Task.IsCanceled)
            {
                throw new IOException("connection.start was never received, likely due to a network timeout");
            }

            var connectionStart = m_model0.m_connectionStartCell.Task.Result;
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

            m_clientProperties = new Dictionary<string, object>(m_factory.ClientProperties);
            m_clientProperties["capabilities"] = Protocol.Capabilities;
            m_clientProperties["connection_name"] = this.ClientProvidedName;

            // FIXME: parse out locales properly!
            ConnectionTuneDetails connectionTune = default(ConnectionTuneDetails);
            bool tuned = false;
            try
            {
                string mechanismsString = Encoding.UTF8.GetString(connectionStart.m_mechanisms, 0, connectionStart.m_mechanisms.Length);
                string[] mechanisms = mechanismsString.Split(' ');
                AuthMechanismFactory mechanismFactory = m_factory.AuthMechanismFactory(mechanisms);
                if (mechanismFactory == null)
                {
                    throw new IOException("No compatible authentication mechanism found - " +
                                          "server offered [" + mechanismsString + "]");
                }
                AuthMechanism mechanism = mechanismFactory.GetInstance();
                byte[] challenge = null;
                do
                {
                    byte[] response = mechanism.handleChallenge(challenge, m_factory);
                    ConnectionSecureOrTune res;
                    if (challenge == null)
                    {
                        res = m_model0.ConnectionStartOk(m_clientProperties,
                            mechanismFactory.Name,
                            response,
                            "en_US");
                    }
                    else
                    {
                        res = m_model0.ConnectionSecureOk(response);
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

            var channelMax = (ushort)NegotiatedMaxValue(m_factory.RequestedChannelMax,
                connectionTune.m_channelMax);
            m_sessionManager = new SessionManager(this, channelMax);

            uint frameMax = NegotiatedMaxValue(m_factory.RequestedFrameMax,
                connectionTune.m_frameMax);
            FrameMax = frameMax;

            TimeSpan requestedHeartbeat = m_factory.RequestedHeartbeat;
            var heartbeatInSeconds = NegotiatedMaxValue((uint)requestedHeartbeat.TotalSeconds,
                (uint)connectionTune.m_heartbeatInSeconds);
            Heartbeat = TimeSpan.FromSeconds(heartbeatInSeconds);

            m_model0.ConnectionTuneOk(channelMax, frameMax, (ushort)Heartbeat.TotalSeconds);

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
