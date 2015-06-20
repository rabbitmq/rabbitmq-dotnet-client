// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2014 GoPivotal, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
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
//  at http://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Impl;
using RabbitMQ.Util;
using System;
using System.Collections.Generic;
using System.IO;

#if NETFX_CORE

using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.ApplicationModel;

#else
using System.Net;
using System.Net.Sockets;
#endif

using System.Reflection;
using System.Text;
using System.Threading;

namespace RabbitMQ.Client.Framing.Impl
{
    public class Connection : IConnection
    {
        public readonly object m_eventLock = new object();

        ///<summary>Heartbeat frame for transmission. Reusable across connections.</summary>
        public readonly Frame m_heartbeatFrame = new Frame(Constants.FrameHeartbeat, 0, new byte[0]);

        ///<summary>Timeout used while waiting for AMQP handshaking to
        ///complete (milliseconds)</summary>
        public const int HandshakeTimeout = 10000;

        public ManualResetEvent m_appContinuation = new ManualResetEvent(false);
        public EventHandler<CallbackExceptionEventArgs> m_callbackException;

        public IDictionary<string, object> m_clientProperties;

        public volatile ShutdownEventArgs m_closeReason = null;
        public volatile bool m_closed = false;

        public EventHandler<ConnectionBlockedEventArgs> m_connectionBlocked;
        public EventHandler<ShutdownEventArgs> m_connectionShutdown;
        public EventHandler<EventArgs> m_connectionUnblocked;
        public IConnectionFactory m_factory;
        public IFrameHandler m_frameHandler;

        public Guid m_id = Guid.NewGuid();
        public ModelBase m_model0;
        public volatile bool m_running = true;
        public MainSession m_session0;
        public SessionManager m_sessionManager;

        public IList<ShutdownReportEntry> m_shutdownReport = new SynchronizedList<ShutdownReportEntry>(new List<ShutdownReportEntry>());

        //
        // Heartbeats
        //

        public ushort m_heartbeat = 0;
        public TimeSpan m_heartbeatTimeSpan = TimeSpan.FromSeconds(0);
        public int m_missedHeartbeats = 0;

        private Timer _heartbeatWriteTimer;
        private Timer _heartbeatReadTimer;
        public AutoResetEvent m_heartbeatRead = new AutoResetEvent(false);
        public AutoResetEvent m_heartbeatWrite = new AutoResetEvent(false);


        // true if we haven't finished connection negotiation.
        // In this state socket exceptions are treated as fatal connection
        // errors, otherwise as read timeouts
        private bool m_inConnectionNegotiation;

        public ConsumerWorkService ConsumerWorkService { get; private set; }

        public Connection(IConnectionFactory factory, bool insist, IFrameHandler frameHandler)
        {
            KnownHosts = null;
            FrameMax = 0;
            m_factory = factory;
            m_frameHandler = frameHandler;
            this.ConsumerWorkService = new ConsumerWorkService(factory.TaskScheduler);

            m_sessionManager = new SessionManager(this, 0);
            m_session0 = new MainSession(this) { Handler = NotifyReceivedCloseOk };
            m_model0 = (ModelBase)Protocol.CreateModel(m_session0);

            StartMainLoop(factory.UseBackgroundThreadsForIO);
            Open(insist);

#if NETFX_CORE
#pragma warning disable 0168
            try
            {
                Windows.UI.Xaml.Application.Current.Suspending += this.HandleApplicationSuspend;
            }
            catch (Exception ex)
            {
                // If called from a desktop app (i.e. unit tests), then there is no current application
            }
#pragma warning restore 0168
#else
            AppDomain.CurrentDomain.DomainUnload += HandleDomainUnload;
#endif
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

        public bool AutoClose
        {
            get { return m_sessionManager.AutoClose; }
            set { m_sessionManager.AutoClose = value; }
        }

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

        public ushort Heartbeat
        {
            get { return m_heartbeat; }
            set
            {
                m_heartbeat = value;
                // timers fire at slightly below half the interval to avoid race
                // conditions
                m_heartbeatTimeSpan = TimeSpan.FromMilliseconds((value * 1000) / 4);
                m_frameHandler.Timeout = value * 1000 * 2;
            }
        }

        public bool IsOpen
        {
            get { return CloseReason == null; }
        }

        public AmqpTcpEndpoint[] KnownHosts { get; set; }

#if !NETFX_CORE
        public EndPoint LocalEndPoint
        {
            get { return m_frameHandler.LocalEndPoint; }
        }
#endif

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

#if !NETFX_CORE
        public EndPoint RemoteEndPoint
        {
            get { return m_frameHandler.RemoteEndPoint; }
        }
#endif

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
            Assembly assembly =
#if NETFX_CORE
 System.Reflection.IntrospectionExtensions.GetTypeInfo(typeof(Connection)).Assembly;
#else
                System.Reflection.Assembly.GetAssembly(typeof(Connection));
#endif

            string version = assembly.GetName().Version.ToString();
            //TODO: Get the rest of this data from the Assembly Attributes
            IDictionary<string, object> table = new Dictionary<string, object>();
            table["product"] = Encoding.UTF8.GetBytes("RabbitMQ");
            table["version"] = Encoding.UTF8.GetBytes(version);
            table["platform"] = Encoding.UTF8.GetBytes(".NET");
            table["copyright"] = Encoding.UTF8.GetBytes("Copyright (C) 2007-2014 GoPivotal, Inc.");
            table["information"] = Encoding.UTF8.GetBytes("Licensed under the MPL.  " +
                                                          "See http://www.rabbitmq.com/");
            return table;
        }

        public void Abort(ushort reasonCode, string reasonText,
            ShutdownInitiator initiator, int timeout)
        {
            Close(new ShutdownEventArgs(initiator, reasonCode, reasonText),
                true, timeout);
        }

        public void Close(ShutdownEventArgs reason)
        {
            Close(reason, false, Timeout.Infinite);
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
        ///to complete. Negative or Timeout.Infinite value mean infinity.
        ///</para>
        ///</remarks>
        public void Close(ShutdownEventArgs reason, bool abort, int timeout)
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
                    m_session0.Transmit(ConnectionCloseWrapper(reason.ReplyCode,
                        reason.ReplyText));
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

#if NETFX_CORE
            var receivedSignal = m_appContinuation.WaitOne(BlockingCell.validatedTimeout(timeout));
#else
            var receivedSignal = m_appContinuation.WaitOne(BlockingCell.validatedTimeout(timeout), true);
#endif

            if (!receivedSignal)
            {
                m_frameHandler.Close();
            }
        }

        ///<remarks>
        /// Loop only used while quiescing. Use only to cleanly close connection
        ///</remarks>
        public void ClosingLoop()
        {
            try
            {
                m_frameHandler.Timeout = 0;
                // Wait for response/socket closure or timeout
                while (!m_closed)
                {
                    MainLoopIteration();
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
            int replyClassId, replyMethodId;
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
            // Notify hearbeat loops that they can leave
            m_heartbeatRead.Set();
            m_heartbeatWrite.Set();
            m_closed = true;
            MaybeStopHeartbeatTimers();

            m_frameHandler.Close();
            m_model0.SetCloseReason(m_closeReason);
            m_model0.FinishClose();
        }

#if NETFX_CORE

        /// <remarks>
        /// We need to close the socket, otherwise suspending the application will take the maximum time allowed
        /// </remarks>
        public void HandleApplicationSuspend(object sender, SuspendingEventArgs suspendingEventArgs)
        {
            Abort(Constants.InternalError, "Application Suspend");
        }

#else
        /// <remarks>
        /// We need to close the socket, otherwise attempting to unload the domain
        /// could cause a CannotUnloadAppDomainException
        /// </remarks>
        public void HandleDomainUnload(object sender, EventArgs ea)
        {
            Abort(Constants.InternalError, "Domain Unload");
        }
#endif

        public void HandleMainLoopException(ShutdownEventArgs reason)
        {
            if (!SetCloseReason(reason))
            {
                LogCloseError("Unexpected Main Loop Exception while closing: "
                              + reason, null);
                return;
            }

            OnShutdown();
            LogCloseError("Unexpected connection closure: " + reason, null);
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
            m_shutdownReport.Add(new ShutdownReportEntry(error, ex));
        }

        public void MainLoop()
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
#if !NETFX_CORE
                catch (Exception ex)
                {
                    HandleMainLoopException(new ShutdownEventArgs(ShutdownInitiator.Library,
                        Constants.InternalError,
                        "Unexpected Exception",
                        ex));
                }
#endif

                // If allowed for clean shutdown, run main loop until the
                // connection closes.
                if (shutdownCleanly)
                {
#pragma warning disable 0168
                    try
                    {
                        ClosingLoop();
                    }
#if NETFX_CORE
                    catch (Exception ex)
                    {
                        if (SocketError.GetStatus(ex.HResult) != SocketErrorStatus.Unknown)
                        {
                            // means that socket was closed when frame handler
                            // attempted to use it. Since we are shutting down,
                            // ignore it.
                        }
                        else
                        {
                            throw ex;
                        }
                    }
#else
                    catch (SocketException se)
                    {
                        // means that socket was closed when frame handler
                        // attempted to use it. Since we are shutting down,
                        // ignore it.
                    }
#endif
#pragma warning restore 0168
                }

                FinishClose();
            }
            finally
            {
                m_appContinuation.Set();
            }
        }

        public void MainLoopIteration()
        {
            Frame frame = m_frameHandler.ReadFrame();

            NotifyHeartbeatListener();
            // We have received an actual frame.
            if (frame.Type == Constants.FrameHeartbeat)
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

        public void NotifyHeartbeatListener()
        {
            if (m_heartbeat != 0)
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
#if NETFX_CORE
#pragma warning disable 0168
            try
            {
                Windows.UI.Xaml.Application.Current.Suspending -= this.HandleApplicationSuspend;
            }
            catch (Exception ex)
            {
                // If called from a desktop app (i.e. unit tests), then there is no current application
            }
#pragma warning restore 0168
#else
            AppDomain.CurrentDomain.DomainUnload -= HandleDomainUnload;
#endif
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
#if NETFX_CORE
                System.Diagnostics.Debug.WriteLine(
#else
                Console.Error.WriteLine(
#endif
"No errors reported when closing connection {0}", this);
            }
            else
            {
#if NETFX_CORE
                System.Diagnostics.Debug.WriteLine(
#else
                Console.Error.WriteLine(
#endif
"Log of errors while closing connection {0}:", this);
                foreach (ShutdownReportEntry entry in ShutdownReport)
                {
#if NETFX_CORE
                    System.Diagnostics.Debug.WriteLine(
#else
                    Console.Error.WriteLine(
#endif
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
            if (Heartbeat != 0)
            {
                _heartbeatWriteTimer = new Timer(HeartbeatWriteTimerCallback);
                _heartbeatReadTimer = new Timer(HeartbeatReadTimerCallback);
#if NETFX_CORE
                _heartbeatWriteTimer.Change(200, m_heartbeatTimeSpan.Milliseconds);               
                _heartbeatReadTimer.Change(200, m_heartbeatTimeSpan.Milliseconds);
#else
                _heartbeatWriteTimer.Change(TimeSpan.FromMilliseconds(200), m_heartbeatTimeSpan);
                _heartbeatReadTimer.Change(TimeSpan.FromMilliseconds(200), m_heartbeatTimeSpan);
#endif
            }
        }

        public void StartMainLoop(bool useBackgroundThread)
        {
            var taskName = "AMQP Connection " + Endpoint;

#if NETFX_CORE
            Task.Factory.StartNew(this.MainLoop, TaskCreationOptions.LongRunning);
#else
            var mainLoopThread = new Thread(MainLoop);
            mainLoopThread.Name = taskName;
            mainLoopThread.IsBackground = useBackgroundThread;
            mainLoopThread.Start();
#endif
        }

        public void HeartbeatReadTimerCallback(object state)
        {
            bool shouldTerminate = false;
            if (!m_closed)
            {
                if (!m_heartbeatRead.WaitOne(0))
                {
                    m_missedHeartbeats++;
                }
                else
                {
                    m_missedHeartbeats = 0;
                }

                // We check against 8 = 2 * 4 because we need to wait for at
                // least two complete heartbeat setting intervals before
                // complaining, and we've set the socket timeout to a quarter
                // of the heartbeat setting in setHeartbeat above.
                if (m_missedHeartbeats > 2 * 4)
                {
                    String description = String.Format("Heartbeat missing with heartbeat == {0} seconds", m_heartbeat);
                    var eose = new EndOfStreamException(description);
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
                _heartbeatReadTimer.Change(Heartbeat * 1000, Timeout.Infinite);
            }
        }

        public void HeartbeatWriteTimerCallback(object state)
        {
            bool shouldTerminate = false;
            try
            {
                if (!m_closed)
                {
                    WriteFrame(m_heartbeatFrame);
                    m_frameHandler.Flush();
                }
            }
            catch (Exception e)
            {
                HandleMainLoopException(new ShutdownEventArgs(
                    ShutdownInitiator.Library,
                    0,
                    "End of stream",
                    e));
                shouldTerminate = true;
            }

            if (m_closed || shouldTerminate)
            {
                TerminateMainloop();
                FinishClose();
            }
        }

        protected void MaybeStopHeartbeatTimers()
        {
            if(_heartbeatReadTimer != null)
            {
                _heartbeatReadTimer.Dispose();
            }
            if(_heartbeatWriteTimer != null)
            {
                _heartbeatWriteTimer.Dispose();
            }
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

        public void WriteFrame(Frame f)
        {
            m_frameHandler.WriteFrame(f);
            m_heartbeatWrite.Set();
        }

        ///<summary>API-side invocation of connection abort.</summary>
        public void Abort()
        {
            Abort(Timeout.Infinite);
        }

        ///<summary>API-side invocation of connection abort.</summary>
        public void Abort(ushort reasonCode, string reasonText)
        {
            Abort(reasonCode, reasonText, Timeout.Infinite);
        }

        ///<summary>API-side invocation of connection abort with timeout.</summary>
        public void Abort(int timeout)
        {
            Abort(Constants.ReplySuccess, "Connection close forced", timeout);
        }

        ///<summary>API-side invocation of connection abort with timeout.</summary>
        public void Abort(ushort reasonCode, string reasonText, int timeout)
        {
            Abort(reasonCode, reasonText, ShutdownInitiator.Application, timeout);
        }

        ///<summary>API-side invocation of connection.close.</summary>
        public void Close()
        {
            Close(Constants.ReplySuccess, "Goodbye", Timeout.Infinite);
        }

        ///<summary>API-side invocation of connection.close.</summary>
        public void Close(ushort reasonCode, string reasonText)
        {
            Close(reasonCode, reasonText, Timeout.Infinite);
        }

        ///<summary>API-side invocation of connection.close with timeout.</summary>
        public void Close(int timeout)
        {
            Close(Constants.ReplySuccess, "Goodbye", timeout);
        }

        ///<summary>API-side invocation of connection.close with timeout.</summary>
        public void Close(ushort reasonCode, string reasonText, int timeout)
        {
            Close(new ShutdownEventArgs(ShutdownInitiator.Application, reasonCode, reasonText), false, timeout);
        }

        public IModel CreateModel()
        {
            EnsureIsOpen();
            ISession session = CreateSession();
            var model = (IFullModel)Protocol.CreateModel(session, this.ConsumerWorkService);
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
            MaybeStopHeartbeatTimers();
            Abort();
            if (ShutdownReport.Count > 0)
            {
                foreach (ShutdownReportEntry entry in ShutdownReport)
                {
                    if (entry.Exception != null)
                    {
                        throw entry.Exception;
                    }
                }
                throw new OperationInterruptedException(null);
            }
        }

        protected Command ChannelCloseWrapper(ushort reasonCode, string reasonText)
        {
            Command request;
            int replyClassId, replyMethodId;
            Protocol.CreateChannelClose(reasonCode,
                reasonText,
                out request,
                out replyClassId,
                out replyMethodId);
            return request;
        }

        protected void StartAndTune()
        {
            m_inConnectionNegotiation = true;
            var connectionStartCell = new BlockingCell();
            m_model0.m_connectionStartCell = connectionStartCell;
            m_frameHandler.Timeout = HandshakeTimeout;
            m_frameHandler.SendHeader();

            var connectionStart = (ConnectionStartDetails)
                connectionStartCell.Value;

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

            m_clientProperties = new Dictionary<string, object>(m_factory.ClientProperties);
            m_clientProperties["capabilities"] = Protocol.Capabilities;

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

            var heartbeat = (ushort)NegotiatedMaxValue(m_factory.RequestedHeartbeat,
                connectionTune.m_heartbeat);
            Heartbeat = heartbeat;

            m_model0.ConnectionTuneOk(channelMax,
                frameMax,
                heartbeat);

            m_inConnectionNegotiation = false;
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