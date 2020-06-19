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
    internal sealed partial class Connection : IConnection
    {
        private bool _disposed = false;
        private readonly object _eventLock = new object();

        ///<summary>Heartbeat frame for transmission. Reusable across connections.</summary>
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private readonly RpcContinuationQueue _rpcContinuationQueue;
        private volatile ShutdownEventArgs _closeReason = null;

        private AsyncEventHandler<ShutdownEventArgs> _connectionShutdown;

        private readonly IConnectionFactory _factory;
        private readonly IFrameHandler _frameHandler;
        private volatile bool _closed = false;
        private volatile bool _running = true;

        private Guid _id = Guid.NewGuid();
        internal readonly Model _model0;
        private readonly MainSession _session0;
        private SessionManager _sessionManager;

        private Task _mainLoopTask;

        private static readonly string s_version = typeof(Connection).Assembly
                                            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                            .InformationalVersion;

        public TimeSpan HandshakeContinuationTimeout { get; set; } = TimeSpan.FromSeconds(10);

        public Connection(IConnectionFactory factory, IFrameHandler frameHandler, string clientProvidedName = null)
        {
            ClientProvidedName = clientProvidedName;
            KnownHosts = null;
            FrameMax = 0;
            _factory = factory;
            _frameHandler = frameHandler;
            _sessionManager = new SessionManager(this, 0);
            _session0 = new MainSession(this) { Handler = NotifyReceivedCloseOk };
            _model0 = (Model)Protocol.CreateModel(_session0);
            _rpcContinuationQueue = new RpcContinuationQueue(_session0);
        }

        public Guid Id => _id;

        public event EventHandler<CallbackExceptionEventArgs> CallbackException;

        public event EventHandler<ConnectionBlockedEventArgs> ConnectionBlocked;

        public event AsyncEventHandler<ShutdownEventArgs> ConnectionShutdown
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

        public ushort ChannelMax => _sessionManager.ChannelMax;

        public IDictionary<string, object> ClientProperties { get; set; }

        public ShutdownEventArgs CloseReason => _closeReason;

        public AmqpTcpEndpoint Endpoint => _frameHandler.Endpoint;

        public uint FrameMax { get; set; }

        public TimeSpan Heartbeat
        {
            get => _heartbeat;
            set
            {
                _heartbeat = value;
                // timers fire at slightly below half the interval to avoid race
                // conditions
                _heartbeatInterval = TimeSpan.FromMilliseconds(_heartbeat.TotalMilliseconds / 2 * 0.9);
                _frameHandler.ReadTimeout = TimeSpan.FromMilliseconds(_heartbeat.TotalMilliseconds * 2);
            }
        }

        public bool IsOpen => CloseReason == null && _mainLoopTask != null;

        public AmqpTcpEndpoint[] KnownHosts { get; set; }

        public EndPoint LocalEndPoint => _frameHandler.LocalEndPoint;

        public int LocalPort => _frameHandler.LocalPort;

        ///<summary>Another overload of a Protocol property, useful
        ///for exposing a tighter type.</summary>
        public ProtocolBase Protocol => (ProtocolBase)Endpoint.Protocol;

        public EndPoint RemoteEndPoint => _frameHandler.RemoteEndPoint;

        public int RemotePort => _frameHandler.RemotePort;

        public IDictionary<string, object> ServerProperties { get; set; }

        public IList<ShutdownReportEntry> ShutdownReport { get; } = new SynchronizedList<ShutdownReportEntry>(new List<ShutdownReportEntry>());

        ///<summary>Explicit implementation of IConnection.Protocol.</summary>
        IProtocol IConnection.Protocol => Endpoint.Protocol;

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

        public ValueTask Abort(ushort reasonCode, string reasonText, ShutdownInitiator initiator, TimeSpan timeout) => Close(new ShutdownEventArgs(initiator, reasonCode, reasonText), true, timeout);

        public ValueTask Close(ShutdownEventArgs reason) => Close(reason, false, Timeout.InfiniteTimeSpan);

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
        public async ValueTask Close(ShutdownEventArgs reason, bool abort, TimeSpan timeout)
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
                await OnShutdown().ConfigureAwait(false);
                _session0.SetSessionClosing(false);

                try
                {
                    // Try to send connection.close
                    // Wait for CloseOk in the MainLoop
                    await _session0.Transmit(ConnectionCloseWrapper(reason.ReplyCode, reason.ReplyText)).ConfigureAwait(false);
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
                    if (_model0.CloseReason == null)
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
                await _mainLoopTask.ConfigureAwait(false);
            }
            catch
            {

            }
        }

        ///<remarks>
        /// Loop only used while quiescing. Use only to cleanly close connection
        ///</remarks>
        public async ValueTask ClosingLoop()
        {
            try
            {
                _frameHandler.ReadTimeout = TimeSpan.Zero;
                // Wait for response/socket closure or timeout
                while (!_closed)
                {
                    await MainLoopIteration().ConfigureAwait(false);
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
            return new Command(new Impl.ConnectionClose(reasonCode, reasonText, 0, 0));
        }

        public ISession CreateSession() => _sessionManager.Create();

        public ISession CreateSession(int channelNumber) => _sessionManager.Create(channelNumber);

        public void EnsureIsOpen()
        {
            if (!IsOpen)
            {
                throw new AlreadyClosedException(CloseReason);
            }
        }

        // Only call at the end of the Mainloop or HeartbeatLoop
        public ValueTask FinishClose()
        {
            _closed = true;
            MaybeStopHeartbeatTimers();
            _frameHandler.Close();
            _model0.SetCloseReason(_closeReason);
            return _model0.FinishClose();
        }

        /// <remarks>
        /// We need to close the socket, otherwise attempting to unload the domain
        /// could cause a CannotUnloadAppDomainException
        /// </remarks>
        public void HandleDomainUnload(object sender, EventArgs ea) => Abort(Constants.InternalError, "Domain Unload");

        public ValueTask HandleMainLoopException(ShutdownEventArgs reason)
        {
            if (!SetCloseReason(reason))
            {
                LogCloseError($"Unexpected Main Loop Exception while closing: {reason}", new Exception(reason.ToString()));
                return default;
            }

            LogCloseError($"Unexpected connection closure: {reason}", new Exception(reason.ToString()));
            return OnShutdown();
        }

        public async ValueTask<bool> HardProtocolExceptionHandler(HardProtocolException hpe)
        {
            if (SetCloseReason(hpe.ShutdownReason))
            {
                await OnShutdown().ConfigureAwait(false);
                _session0.SetSessionClosing(false);
                try
                {
                    await _session0.Transmit(ConnectionCloseWrapper(hpe.ShutdownReason.ReplyCode, hpe.ShutdownReason.ReplyText)).ConfigureAwait(false);
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

        public async ValueTask InternalClose(ShutdownEventArgs reason)
        {
            if (!SetCloseReason(reason))
            {
                if (_closed)
                {
                    throw new AlreadyClosedException(_closeReason);
                }
                // We are quiescing, but still allow for server-close
            }

            await OnShutdown().ConfigureAwait(false);
            _session0.SetSessionClosing(true);
            TerminateMainloop();
        }

        public void LogCloseError(string error, Exception ex)
        {
            ESLog.Error(error, ex);
            ShutdownReport.Add(new ShutdownReportEntry(error, ex));
        }

        public async Task MainLoop()
        {
            bool shutdownCleanly = false;
            try
            {
                while (_running)
                {
                    try
                    {
                        ValueTask task = MainLoopIteration();
                        if (!task.IsCompletedSuccessfully)
                        {
                            await task.ConfigureAwait(false);
                        }
                    }
                    catch (SoftProtocolException spe)
                    {
                        await QuiesceChannel(spe).ConfigureAwait(false);
                    }
                }
                shutdownCleanly = true;
            }
            catch (EndOfStreamException eose)
            {
                // Possible heartbeat exception
                await HandleMainLoopException(new ShutdownEventArgs(
                    ShutdownInitiator.Library,
                    0,
                    "End of stream",
                    eose)).ConfigureAwait(false);
            }
            catch (HardProtocolException hpe)
            {
                shutdownCleanly = await HardProtocolExceptionHandler(hpe).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await HandleMainLoopException(new ShutdownEventArgs(ShutdownInitiator.Library,
                    Constants.InternalError,
                    "Unexpected Exception",
                    ex)).ConfigureAwait(false);
            }

            // If allowed for clean shutdown, run main loop until the
            // connection closes.
            if (shutdownCleanly)
            {
#pragma warning disable 0168
                try
                {
                    await ClosingLoop().ConfigureAwait(false);
                }
                catch (SocketException)
                {
                    // means that socket was closed when frame handler
                    // attempted to use it. Since we are shutting down,
                    // ignore it.
                }
#pragma warning restore 0168
            }

            await FinishClose().ConfigureAwait(false);
        }

        public async ValueTask MainLoopIteration()
        {
            ValueTask<InboundFrame> readFrameTask = _frameHandler.ReadFrame();
            using (InboundFrame frame = readFrameTask.IsCompletedSuccessfully ? readFrameTask.Result : await readFrameTask.ConfigureAwait(false))
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
                    ValueTask session0Task = _session0.HandleFrame(in frame);
                    if (!session0Task.IsCompletedSuccessfully)
                    {
                        await session0Task.ConfigureAwait(false);
                    }
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
                        ISession session = _sessionManager.Lookup(frame.Channel) ?? throw new ChannelErrorException(frame.Channel);
                        ValueTask sessionHandleTask = session.HandleFrame(in frame);
                        if (!sessionHandleTask.IsCompletedSuccessfully)
                        {
                            await sessionHandleTask.ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        public void NotifyReceivedCloseOk()
        {
            _closed = true;
            TerminateMainloop();
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
        public async ValueTask OnShutdown()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            AsyncEventHandler<ShutdownEventArgs> handler;
            ShutdownEventArgs reason;
            lock (_eventLock)
            {
                handler = _connectionShutdown;
                reason = _closeReason;
                _connectionShutdown = null;
            }

            if (handler != null)
            {
                foreach (AsyncEventHandler<ShutdownEventArgs> h in handler.GetInvocationList())
                {
                    try
                    {
                        await h(this, reason).ConfigureAwait(false);
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

        public async ValueTask Open()
        {
            if (!IsOpen)
            {
                await StartAndTune().ConfigureAwait(false);
                await SendAndReceiveAsync<ConnectionOpenOk>(new ConnectionOpen(_factory.VirtualHost, string.Empty, false)).ConfigureAwait(false);

                // now let's also start the heartbeat tasks
                MaybeStartHeartbeatTimers();
            }
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
                for (int index = 0; index < ShutdownReport.Count; index++)
                {
                    Console.Error.WriteLine(ShutdownReport[index].ToString());
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
        public async ValueTask QuiesceChannel(SoftProtocolException pe)
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
            await oldSession.Close(pe.ShutdownReason).ConfigureAwait(false);

            // The upper layers have been signalled. Now we can tell
            // our peer. The peer will respond through the lower
            // layers - specifically, through the QuiescingSession we
            // installed above.
            await newSession.Transmit(ChannelCloseWrapper(pe.ReplyCode, pe.Message)).ConfigureAwait(false);
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

        internal ValueTask Transmit(Command command, int channelNumber)
        {
            // try to get the conch; if not, switch to async
            if (!_writeLock.Wait(0))
            {
                return TransmitImplSlow(command, (ushort)channelNumber);
            }
            bool release = true;
            try
            {
                SerializeCommandAsFrames(command, (ushort)channelNumber);
                ValueTask flush = Flush();
                if (flush.IsCompletedSuccessfully)
                {
                    return default;
                }
                release = false;
                return AwaitFlushAndRelease(flush);
            }
            finally
            {
                if (release)
                {
                    _writeLock.Release();
                }
            }
        }

        internal ValueTask Transmit(IEnumerable<Command> commands, int channelNumber)
        {
            // try to get the conch; if not, switch to async
            if (!_writeLock.Wait(0))
            {
                return TransmitImplSlow(commands, (ushort)channelNumber);
            }
            bool release = true;
            try
            {
                foreach (Command command in commands)
                {
                    SerializeCommandAsFrames(command, (ushort)channelNumber);
                }

                ValueTask flush = Flush();
                if (flush.IsCompletedSuccessfully)
                {
                    return default;
                }
                release = false;
                return AwaitFlushAndRelease(flush);
            }
            finally
            {
                if (release)
                {
                    _writeLock.Release();
                }
            }
        }

        private async ValueTask AwaitFlushAndRelease(ValueTask flush)
        {
            try { await flush.ConfigureAwait(false); }
            finally { _writeLock.Release(); }
        }

        private async ValueTask TransmitImplSlow(Command command, int channelNumber)
        {
            try
            {
                await _writeLock.WaitAsync().ConfigureAwait(false);
                SerializeCommandAsFrames(command, (ushort)channelNumber);
                await Flush().ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private async ValueTask TransmitImplSlow(IEnumerable<Command> commands, int channelNumber)
        {
            try
            {
                await _writeLock.WaitAsync().ConfigureAwait(false);
                foreach (Command command in commands)
                {
                    SerializeCommandAsFrames(command, (ushort)channelNumber);
                }

                await Flush().ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private void SerializeCommandAsFrames(Command command, ushort channelNumber)
        {
            MethodOutboundFrame methodFrame = new MethodOutboundFrame(channelNumber, command.Method);
            _frameHandler.WriteFrame(in methodFrame);
            if (command.Method.HasContent)
            {
                HeaderOutboundFrame headerFrame = new HeaderOutboundFrame(channelNumber, command.Header, command.Body.Length);
                _frameHandler.WriteFrame(in headerFrame);
                int frameMax = (int)Math.Min(int.MaxValue, FrameMax);
                int bodyPayloadMax = (frameMax == 0) ? command.Body.Length : frameMax - Command.EmptyFrameSize;
                for (int offset = 0; offset < command.Body.Length; offset += bodyPayloadMax)
                {
                    int remaining = command.Body.Length - offset;
                    int count = (remaining < bodyPayloadMax) ? remaining : bodyPayloadMax;
                    var bodyFrame = new BodySegmentOutboundFrame(channelNumber, command.Body.Slice(offset, count));
                    _frameHandler.WriteFrame(in bodyFrame);
                }
            }
        }

        private async ValueTask<T> SendAndReceiveAsync<T>(Client.Impl.MethodBase method) where T : Client.Impl.MethodBase => await _rpcContinuationQueue.SendAndReceiveAsync<T>(new Command(method)).ConfigureAwait(false);

        private async ValueTask<Client.Impl.MethodBase> SendAndReceiveAsync(Client.Impl.MethodBase method) => (await _rpcContinuationQueue.SendAndReceiveAsync(new Command(method)).ConfigureAwait(false)).Method;

        ///<remarks>
        /// May be called more than once. Should therefore be idempotent.
        ///</remarks>
        public void TerminateMainloop() => _running = false;

        public override string ToString() => $"Connection({_id},{Endpoint})";

        public ValueTask Flush() => _frameHandler.Flush();

        public async ValueTask UpdateSecret(string newSecret, string reason) => await SendAndReceiveAsync<ConnectionUpdateSecretOk>(new ConnectionUpdateSecret(Encoding.UTF8.GetBytes(newSecret), reason)).ConfigureAwait(false);

        ///<summary>API-side invocation of connection abort.</summary>
        public ValueTask Abort() => Abort(Timeout.InfiniteTimeSpan);

        ///<summary>API-side invocation of connection abort.</summary>
        public ValueTask Abort(ushort reasonCode, string reasonText) => Abort(reasonCode, reasonText, Timeout.InfiniteTimeSpan);

        ///<summary>API-side invocation of connection abort with timeout.</summary>
        public ValueTask Abort(TimeSpan timeout) => Abort(Constants.ReplySuccess, "Connection close forced", timeout);

        ///<summary>API-side invocation of connection abort with timeout.</summary>
        public ValueTask Abort(ushort reasonCode, string reasonText, TimeSpan timeout) => Abort(reasonCode, reasonText, ShutdownInitiator.Application, timeout);

        ///<summary>API-side invocation of connection.close.</summary>
        public ValueTask Close() => Close(Constants.ReplySuccess, "Goodbye", Timeout.InfiniteTimeSpan);

        ///<summary>API-side invocation of connection.close.</summary>
        public ValueTask Close(ushort reasonCode, string reasonText) => Close(reasonCode, reasonText, Timeout.InfiniteTimeSpan);

        ///<summary>API-side invocation of connection.close with timeout.</summary>
        public ValueTask Close(TimeSpan timeout) => Close(Constants.ReplySuccess, "Goodbye", timeout);

        ///<summary>API-side invocation of connection.close with timeout.</summary>
        public ValueTask Close(ushort reasonCode, string reasonText, TimeSpan timeout) => Close(new ShutdownEventArgs(ShutdownInitiator.Application, reasonCode, reasonText), false, timeout);

        public async ValueTask<IModel> CreateModel()
        {
            EnsureIsOpen();
            ISession session = CreateSession();
            var model = (IFullModel)Protocol.CreateModel(session);
            model.ContinuationTimeout = _factory.ContinuationTimeout;
            await (model as Model).TransmitAndEnqueueAsync<ChannelOpenOk>(new ChannelOpen(string.Empty)).ConfigureAwait(false);
            return model;
        }

        void IDisposable.Dispose() => Dispose(true);

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

        private Command ChannelCloseWrapper(ushort reasonCode, string reasonText)
        {
            return new Command(new ChannelClose(reasonCode, reasonText, 0, 0));
        }

        private async ValueTask StartAndTune()
        {
            // Send header
            await _frameHandler.SendHeader().ConfigureAwait(false);

            // We've sent our header bytes so let's start reading frames.
            _mainLoopTask = Task.Run(MainLoop);

            // Wait for Connection.Start
            ConnectionStart connectionStart = await _rpcContinuationQueue.SendAndReceiveAsync<ConnectionStart>(default(Command), HandshakeContinuationTimeout).ConfigureAwait(false);
            ServerProperties = connectionStart._serverProperties;
            var serverVersion = new AmqpVersion(connectionStart._versionMajor, connectionStart._versionMinor);
            if (!serverVersion.Equals(Protocol.Version))
            {
                TerminateMainloop();
                await FinishClose().ConfigureAwait(false);
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
            try
            {
                string mechanismsString = Encoding.UTF8.GetString(connectionStart._mechanisms, 0, connectionStart._mechanisms.Length);
                string[] mechanisms = mechanismsString.Split(' ');
                IAuthMechanismFactory mechanismFactory = _factory.AuthMechanismFactory(mechanisms);
                if (mechanismFactory == null)
                {
                    throw new IOException($"No compatible authentication mechanism found - server offered [{mechanismsString}]");
                }

                IAuthMechanism mechanism = mechanismFactory.GetInstance();
                Client.Impl.MethodBase expectedConnectionSecureOrTune = await SendAndReceiveAsync(new ConnectionStartOk(ClientProperties, mechanismFactory.Name, mechanism.handleChallenge(Array.Empty<byte>(), _factory), "en_US")).ConfigureAwait(false);
                if (expectedConnectionSecureOrTune is ConnectionSecure connectionSecureMethod)
                {
                    // Send Connection.SecureOk and get a Connection.Tune back
                    expectedConnectionSecureOrTune = await SendAndReceiveAsync<ConnectionTune>(new ConnectionSecureOk(mechanism.handleChallenge(connectionSecureMethod._challenge, _factory)));
                }
                else if (expectedConnectionSecureOrTune is ConnectionClose connectionClose)
                {
                    throw new OperationInterruptedException(new ShutdownEventArgs(ShutdownInitiator.Peer, connectionClose._replyCode, connectionClose._replyText));
                }

                // Received a Connection.Tune
                ConnectionTune connectionTuneMethod = expectedConnectionSecureOrTune as ConnectionTune;
                connectionTune = new ConnectionTuneDetails
                {
                    m_channelMax = connectionTuneMethod._channelMax,
                    m_frameMax = connectionTuneMethod._frameMax,
                    m_heartbeatInSeconds = connectionTuneMethod._heartbeat
                };
            }
            catch (OperationInterruptedException e)
            {
                if (e.ShutdownReason != null && e.ShutdownReason.ReplyCode == Constants.AccessRefused)
                {
                    throw new AuthenticationFailureException(e.ShutdownReason.ReplyText);
                }
                throw new PossibleAuthenticationFailureException("Possibly caused by authentication failure", e);
            }

            ushort channelMax = (ushort)NegotiatedMaxValue(_factory.RequestedChannelMax, connectionTune.m_channelMax);
            _sessionManager = new SessionManager(this, channelMax);

            uint frameMax = NegotiatedMaxValue(_factory.RequestedFrameMax, connectionTune.m_frameMax);
            FrameMax = frameMax;

            TimeSpan requestedHeartbeat = _factory.RequestedHeartbeat;
            uint heartbeatInSeconds = NegotiatedMaxValue((uint)requestedHeartbeat.TotalSeconds, (uint)connectionTune.m_heartbeatInSeconds);
            Heartbeat = TimeSpan.FromSeconds(heartbeatInSeconds);
            await Transmit(new Command(new ConnectionTuneOk(channelMax, frameMax, (ushort)Heartbeat.TotalSeconds)), 0).ConfigureAwait(false);
        }

        private static uint NegotiatedMaxValue(uint clientValue, uint serverValue) => (clientValue == 0 || serverValue == 0) ?
                Math.Max(clientValue, serverValue) :
                Math.Min(clientValue, serverValue);
    }
}
