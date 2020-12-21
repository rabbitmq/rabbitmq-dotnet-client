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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Threading.Tasks;
using RabbitMQ.Client.client.impl.Channel;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Logging;
using Channel = System.Threading.Channels.Channel;

namespace RabbitMQ.Client.Framing.Impl
{
    internal sealed class AutorecoveringConnection : IConnection
    {
        private Connection _delegate;
        private readonly object _eventLock = new object();
        private readonly ConnectionFactory _factory;

        // list of endpoints provided on initial connection.
        // on re-connection, the next host in the line is chosen using
        // IHostnameSelector
        private IEndpointResolver _endpoints;

        private readonly object _recordedEntitiesLock = new object();

        private readonly Dictionary<string, RecordedExchange> _recordedExchanges = new Dictionary<string, RecordedExchange>();
        private readonly Dictionary<string, RecordedQueue> _recordedQueues = new Dictionary<string, RecordedQueue>();
        private readonly Dictionary<RecordedBinding, byte> _recordedBindings = new Dictionary<RecordedBinding, byte>();
        private readonly Dictionary<string, RecordedConsumer> _recordedConsumers = new Dictionary<string, RecordedConsumer>();
        private readonly List<AutorecoveringChannel> _channels = new List<AutorecoveringChannel>();

        private EventHandler<ConnectionBlockedEventArgs> _recordedBlockedEventHandlers;
        private EventHandler<ShutdownEventArgs> _recordedShutdownEventHandlers;
        private EventHandler<EventArgs> _recordedUnblockedEventHandlers;

        private bool _disposed;

        public AutorecoveringConnection(ConnectionFactory factory, string clientProvidedName = null)
        {
            _factory = factory;
            ClientProvidedName = clientProvidedName;
        }

        private Connection Delegate => !_disposed ? _delegate : throw new ObjectDisposedException(GetType().FullName);

        public event EventHandler<EventArgs> RecoverySucceeded;
        public event EventHandler<ConnectionRecoveryErrorEventArgs> ConnectionRecoveryError;
        public event EventHandler<CallbackExceptionEventArgs> CallbackException
        {
            add
            {
                ThrowIfDisposed();
                lock (_eventLock)
                {
                    _delegate.CallbackException += value;
                }
            }
            remove
            {
                ThrowIfDisposed();
                lock (_eventLock)
                {
                    _delegate.CallbackException -= value;
                }
            }
        }

        public event EventHandler<ConnectionBlockedEventArgs> ConnectionBlocked
        {
            add
            {
                ThrowIfDisposed();
                lock (_eventLock)
                {
                    _recordedBlockedEventHandlers += value;
                    _delegate.ConnectionBlocked += value;
                }
            }
            remove
            {
                ThrowIfDisposed();
                lock (_eventLock)
                {
                    _recordedBlockedEventHandlers -= value;
                    _delegate.ConnectionBlocked -= value;
                }
            }
        }

        public event EventHandler<ShutdownEventArgs> ConnectionShutdown
        {
            add
            {
                ThrowIfDisposed();
                lock (_eventLock)
                {
                    _recordedShutdownEventHandlers += value;
                    _delegate.ConnectionShutdown += value;
                }
            }
            remove
            {
                ThrowIfDisposed();
                lock (_eventLock)
                {
                    _recordedShutdownEventHandlers -= value;
                    _delegate.ConnectionShutdown -= value;
                }
            }
        }

        public event EventHandler<EventArgs> ConnectionUnblocked
        {
            add
            {
                ThrowIfDisposed();
                lock (_eventLock)
                {
                    _recordedUnblockedEventHandlers += value;
                    _delegate.ConnectionUnblocked += value;
                }
            }
            remove
            {
                ThrowIfDisposed();
                lock (_eventLock)
                {
                    _recordedUnblockedEventHandlers -= value;
                    _delegate.ConnectionUnblocked -= value;
                }
            }
        }

        public event EventHandler<ConsumerTagChangedAfterRecoveryEventArgs> ConsumerTagChangeAfterRecovery;
        public event EventHandler<QueueNameChangedAfterRecoveryEventArgs> QueueNameChangeAfterRecovery;

        public string ClientProvidedName { get; }

        public ushort ChannelMax => Delegate.ChannelMax;

        public ConsumerWorkService ConsumerWorkService => Delegate.ConsumerWorkService;

        public IDictionary<string, object> ClientProperties => Delegate.ClientProperties;

        public ShutdownEventArgs CloseReason => Delegate.CloseReason;

        public AmqpTcpEndpoint Endpoint => Delegate.Endpoint;

        public uint FrameMax => Delegate.FrameMax;

        public TimeSpan Heartbeat => Delegate.Heartbeat;

        public bool IsOpen => _delegate?.IsOpen ?? false;

        public AmqpTcpEndpoint[] KnownHosts
        {
            get => Delegate.KnownHosts;
            set => Delegate.KnownHosts = value;
        }

        public int LocalPort => Delegate.LocalPort;

        public ProtocolBase Protocol => Delegate.Protocol;

        public int RemotePort => Delegate.RemotePort;

        public IDictionary<string, object> ServerProperties => Delegate.ServerProperties;

        public IList<ShutdownReportEntry> ShutdownReport => Delegate.ShutdownReport;

        IProtocol IConnection.Protocol => Endpoint.Protocol;

        private bool TryPerformAutomaticRecovery()
        {
            ESLog.Info("Performing automatic recovery");

            try
            {
                if (TryRecoverConnectionDelegate())
                {
                    RecoverConnectionShutdownHandlers();
                    RecoverConnectionBlockedHandlers();
                    RecoverConnectionUnblockedHandlers();

                    RecoverChannelsAsync().GetAwaiter().GetResult();
                    if (_factory.TopologyRecoveryEnabled)
                    {
                        RecoverEntitiesAsync().GetAwaiter().GetResult();
                        RecoverConsumersAsync().GetAwaiter().GetResult();
                    }

                    ESLog.Info("Connection recovery completed");
                    RunRecoveryEventHandlers();

                    return true;
                }
                else
                {
                    ESLog.Warn("Connection delegate was manually closed. Aborted recovery.");
                }
            }
            catch (Exception e)
            {
                ESLog.Error("Exception when recovering connection. Will try again after retry interval.", e);
            }

            return false;
        }

        public void Close(ShutdownEventArgs reason) => Delegate.Close(reason);

        internal async ValueTask<RecoveryAwareChannel> CreateNonRecoveringChannelAsync()
        {
            var channel = new RecoveryAwareChannel(Delegate.CreateSession(), ConsumerWorkService) { ContinuationTimeout = _factory.ContinuationTimeout };
            await channel.OpenChannelAsync().ConfigureAwait(false);
            return channel;
        }

        public void DeleteRecordedBinding(RecordedBinding rb)
        {
            lock (_recordedEntitiesLock)
            {
                _recordedBindings.Remove(rb);
            }
        }

        public RecordedConsumer DeleteRecordedConsumer(string consumerTag)
        {
            RecordedConsumer rc;
            lock (_recordedEntitiesLock)
            {
                if (_recordedConsumers.TryGetValue(consumerTag, out rc))
                {
                    _recordedConsumers.Remove(consumerTag);
                }
            }

            return rc;
        }

        public void DeleteRecordedExchange(string name)
        {
            lock (_recordedEntitiesLock)
            {
                _recordedExchanges.Remove(name);

                // find bindings that need removal, check if some auto-delete exchanges
                // might need the same
                IEnumerable<RecordedBinding> bs = _recordedBindings.Keys.Where(b => name.Equals(b.Destination)).ToArray();
                foreach (RecordedBinding b in bs)
                {
                    DeleteRecordedBinding(b);
                    MaybeDeleteRecordedAutoDeleteExchange(b.Source);
                }
            }
        }

        public void DeleteRecordedQueue(string name)
        {
            lock (_recordedEntitiesLock)
            {
                _recordedQueues.Remove(name);
                // find bindings that need removal, check if some auto-delete exchanges
                // might need the same
                IEnumerable<RecordedBinding> bs = _recordedBindings.Keys.Where(b => name.Equals(b.Destination)).ToArray();
                foreach (RecordedBinding b in bs)
                {
                    DeleteRecordedBinding(b);
                    MaybeDeleteRecordedAutoDeleteExchange(b.Source);
                }
            }
        }

        public bool HasMoreConsumersOnQueue(ICollection<RecordedConsumer> consumers, string queue)
        {
            var cs = new List<RecordedConsumer>(consumers);
            return cs.Exists(c => c.Queue.Equals(queue));
        }

        public bool HasMoreDestinationsBoundToExchange(ICollection<RecordedBinding> bindings, string exchange)
        {
            var bs = new List<RecordedBinding>(bindings);
            return bs.Exists(b => b.Source.Equals(exchange));
        }

        public void MaybeDeleteRecordedAutoDeleteExchange(string exchange)
        {
            lock (_recordedEntitiesLock)
            {
                if (!HasMoreDestinationsBoundToExchange(_recordedBindings.Keys, exchange))
                {
                    _recordedExchanges.TryGetValue(exchange, out RecordedExchange rx);
                    // last binding where this exchange is the source is gone,
                    // remove recorded exchange
                    // if it is auto-deleted. See bug 26364.
                    if (rx != null && rx.IsAutoDelete)
                    {
                        _recordedExchanges.Remove(exchange);
                    }
                }
            }
        }

        public void MaybeDeleteRecordedAutoDeleteQueue(string queue)
        {
            lock (_recordedEntitiesLock)
            {
                if (!HasMoreConsumersOnQueue(_recordedConsumers.Values, queue))
                {
                    _recordedQueues.TryGetValue(queue, out RecordedQueue rq);
                    // last consumer on this connection is gone, remove recorded queue
                    // if it is auto-deleted. See bug 26364.
                    if (rq != null && rq.IsAutoDelete)
                    {
                        _recordedQueues.Remove(queue);
                    }
                }
            }
        }

        public void RecordBinding(RecordedBinding rb)
        {
            lock (_recordedEntitiesLock)
            {
                if (!_recordedBindings.ContainsKey(rb))
                {
                    _recordedBindings.Add(rb, 0);
                }
            }
        }

        public void RecordConsumer(string name, RecordedConsumer c)
        {
            lock (_recordedEntitiesLock)
            {
                if (!_recordedConsumers.ContainsKey(name))
                {
                    _recordedConsumers.Add(name, c);
                }
            }
        }

        public void RecordExchange(string name, RecordedExchange x)
        {
            lock (_recordedEntitiesLock)
            {
                _recordedExchanges[name] = x;
            }
        }

        public void RecordQueue(string name, RecordedQueue q)
        {
            lock (_recordedEntitiesLock)
            {
                _recordedQueues[name] = q;
            }
        }

        public override string ToString() => $"AutorecoveringConnection({Delegate.Id},{Endpoint},{GetHashCode()})";

        public void UnregisterChannel(AutorecoveringChannel channel)
        {
            lock (_channels)
            {
                _channels.Remove(channel);
            }
        }

        public void Init() => Init(_factory.EndpointResolverFactory(new List<AmqpTcpEndpoint> { _factory.Endpoint }));

        public void Init(IEndpointResolver endpoints)
        {
            _endpoints = endpoints;
            IFrameHandler fh = endpoints.SelectOne(_factory.CreateFrameHandler);
            Init(fh);
        }

        private void Init(IFrameHandler fh)
        {
            ThrowIfDisposed();
            _delegate = new Connection(_factory, fh, ClientProvidedName);

            _recoveryTask = Task.Run(MainRecoveryLoop);

            EventHandler<ShutdownEventArgs> recoveryListener = (_, args) =>
            {
                if (ShouldTriggerConnectionRecovery(args))
                {
                    _recoveryLoopCommandQueue.Writer.TryWrite(RecoveryCommand.BeginAutomaticRecovery);
                }
            };
            lock (_eventLock)
            {
                ConnectionShutdown += recoveryListener;
                _recordedShutdownEventHandlers += recoveryListener;
            }
        }

        ///<summary>API-side invocation of updating the secret.</summary>
        public void UpdateSecret(string newSecret, string reason)
        {
            ThrowIfDisposed();
            EnsureIsOpen();
            _delegate.UpdateSecret(newSecret, reason);
            _factory.Password = newSecret;
        }

        ///<summary>API-side invocation of connection abort.</summary>
        public void Abort()
        {
            ThrowIfDisposed();
            StopRecoveryLoop();
            if (_delegate.IsOpen)
            {
                _delegate.Abort();
            }
        }

        ///<summary>API-side invocation of connection abort.</summary>
        public void Abort(ushort reasonCode, string reasonText)
        {
            ThrowIfDisposed();
            StopRecoveryLoop();
            if (_delegate.IsOpen)
            {
                _delegate.Abort(reasonCode, reasonText);
            }
        }

        ///<summary>API-side invocation of connection abort with timeout.</summary>
        public void Abort(TimeSpan timeout)
        {
            ThrowIfDisposed();
            StopRecoveryLoop();
            if (_delegate.IsOpen)
            {
                _delegate.Abort(timeout);
            }
        }

        ///<summary>API-side invocation of connection abort with timeout.</summary>
        public void Abort(ushort reasonCode, string reasonText, TimeSpan timeout)
        {
            ThrowIfDisposed();
            StopRecoveryLoop();
            if (_delegate.IsOpen)
            {
                _delegate.Abort(reasonCode, reasonText, timeout);
            }
        }

        ///<summary>API-side invocation of connection.close.</summary>
        public void Close()
        {
            ThrowIfDisposed();
            StopRecoveryLoop();
            if (_delegate.IsOpen)
            {
                _delegate.Close();
            }
        }

        ///<summary>API-side invocation of connection.close.</summary>
        public void Close(ushort reasonCode, string reasonText)
        {
            ThrowIfDisposed();
            StopRecoveryLoop();
            if (_delegate.IsOpen)
            {
                _delegate.Close(reasonCode, reasonText);
            }
        }

        ///<summary>API-side invocation of connection.close with timeout.</summary>
        public void Close(TimeSpan timeout)
        {
            ThrowIfDisposed();
            StopRecoveryLoop();
            if (_delegate.IsOpen)
            {
                _delegate.Close(timeout);
            }
        }

        ///<summary>API-side invocation of connection.close with timeout.</summary>
        public void Close(ushort reasonCode, string reasonText, TimeSpan timeout)
        {
            ThrowIfDisposed();
            StopRecoveryLoop();
            if (_delegate.IsOpen)
            {
                _delegate.Close(reasonCode, reasonText, timeout);
            }
        }

        public async ValueTask<IChannel> CreateChannelAsync()
        {
            EnsureIsOpen();
            AutorecoveringChannel channel = new AutorecoveringChannel(this, await CreateNonRecoveringChannelAsync().ConfigureAwait(false));
            lock (_channels)
            {
                _channels.Add(channel);
            }
            return channel;
        }

        void IDisposable.Dispose() => Dispose(true);

        public void HandleConnectionBlocked(string reason) => Delegate.HandleConnectionBlocked(reason);

        public void HandleConnectionUnblocked() => Delegate.HandleConnectionUnblocked();

        internal int RecordedExchangesCount
        {
            get
            {
                lock (_recordedEntitiesLock)
                {
                    return _recordedExchanges.Count;
                }
            }
        }

        internal int RecordedQueuesCount
        {
            get
            {
                lock (_recordedEntitiesLock)
                {
                    return _recordedExchanges.Count;
                }
            }
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                try
                {
                    Abort();
                }
                catch (Exception)
                {
                    // TODO: log
                }
                finally
                {
                    _channels.Clear();
                    _delegate = null;
                    _recordedBlockedEventHandlers = null;
                    _recordedShutdownEventHandlers = null;
                    _recordedUnblockedEventHandlers = null;

                    _disposed = true;
                }
            }
        }

        private void EnsureIsOpen() => Delegate.EnsureIsOpen();

        private void HandleTopologyRecoveryException(TopologyRecoveryException e) => ESLog.Error("Topology recovery exception", e);

        private void PropagateQueueNameChangeToBindings(string oldName, string newName)
        {
            lock (_recordedBindings)
            {
                foreach (RecordedBinding b in _recordedBindings.Keys)
                {
                    if (b.Destination.Equals(oldName))
                    {
                        b.Destination = newName;
                    }
                }
            }
        }

        private void PropagateQueueNameChangeToConsumers(string oldName, string newName)
        {
            lock (_recordedConsumers)
            {
                foreach (KeyValuePair<string, RecordedConsumer> c in _recordedConsumers)
                {
                    if (c.Value.Queue.Equals(oldName))
                    {
                        c.Value.Queue = newName;
                    }
                }
            }
        }

        private async Task RecoverBindingsAsync()
        {
            Dictionary<RecordedBinding, byte> recordedBindingsCopy;
            lock (_recordedBindings)
            {
                recordedBindingsCopy = new Dictionary<RecordedBinding, byte>(_recordedBindings);
            }

            foreach (RecordedBinding b in recordedBindingsCopy.Keys)
            {
                try
                {
                    await b.RecoverAsync().ConfigureAwait(false);
                }
                catch (Exception cause)
                {
                    string s = $"Caught an exception while recovering binding between {b.Source} and {b.Destination}: {cause.Message}";
                    HandleTopologyRecoveryException(new TopologyRecoveryException(s, cause));
                }
            }
        }

        private void RecoverConnectionBlockedHandlers()
        {
            ThrowIfDisposed();
            lock (_eventLock)
            {
                _delegate.ConnectionBlocked += _recordedBlockedEventHandlers;
            }
        }

        private bool TryRecoverConnectionDelegate()
        {
            ThrowIfDisposed();
            try
            {
                IFrameHandler fh = _endpoints.SelectOne(_factory.CreateFrameHandler);
                _delegate = new Connection(_factory, fh, ClientProvidedName);
                return true;
            }
            catch (Exception e)
            {
                ESLog.Error("Connection recovery exception.", e);
                // Trigger recovery error events
                foreach (EventHandler<ConnectionRecoveryErrorEventArgs> h in ConnectionRecoveryError?.GetInvocationList() ?? Array.Empty<Delegate>())
                {
                    try
                    {
                        h(this, new ConnectionRecoveryErrorEventArgs(e));
                    }
                    catch (Exception ex)
                    {
                        var a = new CallbackExceptionEventArgs(ex);
                        a.Detail["context"] = "OnConnectionRecoveryError";
                        _delegate.OnCallbackException(a);
                    }
                }
            }

            return false;
        }

        private void RecoverConnectionShutdownHandlers() => Delegate.ConnectionShutdown += _recordedShutdownEventHandlers;

        private void RecoverConnectionUnblockedHandlers() => Delegate.ConnectionUnblocked += _recordedUnblockedEventHandlers;

        private async Task RecoverConsumersAsync()
        {
            ThrowIfDisposed();
            Dictionary<string, RecordedConsumer> recordedConsumersCopy;
            lock (_recordedConsumers)
            {
                recordedConsumersCopy = new Dictionary<string, RecordedConsumer>(_recordedConsumers);
            }

            foreach (KeyValuePair<string, RecordedConsumer> pair in recordedConsumersCopy)
            {
                string tag = pair.Key;
                RecordedConsumer cons = pair.Value;

                try
                {
                    await cons.RecoverAsync().ConfigureAwait(false);
                    string newTag = cons.ConsumerTag;
                    lock (_recordedConsumers)
                    {
                        // make sure server-generated tags are re-added
                        _recordedConsumers.Remove(tag);
                        _recordedConsumers.Add(newTag, cons);
                    }

                    foreach (EventHandler<ConsumerTagChangedAfterRecoveryEventArgs> h in ConsumerTagChangeAfterRecovery?.GetInvocationList() ?? Array.Empty<Delegate>())
                    {
                        try
                        {
                            var eventArgs = new ConsumerTagChangedAfterRecoveryEventArgs(tag, newTag);
                            h(this, eventArgs);
                        }
                        catch (Exception e)
                        {
                            var args = new CallbackExceptionEventArgs(e);
                            args.Detail["context"] = "OnConsumerRecovery";
                            _delegate.OnCallbackException(args);
                        }
                    }
                }
                catch (Exception cause)
                {
                    string s = $"Caught an exception while recovering consumer {tag} on queue {cons.Queue}: {cause.Message}";
                    HandleTopologyRecoveryException(new TopologyRecoveryException(s, cause));
                }
            }
        }

        private async Task RecoverEntitiesAsync()
        {
            // The recovery sequence is the following:
            //
            // 1. Recover exchanges
            // 2. Recover queues
            // 3. Recover bindings
            // 4. Recover consumers
            await RecoverExchangesAsync().ConfigureAwait(false);
            await RecoverQueuesAsync().ConfigureAwait(false);
            await RecoverBindingsAsync().ConfigureAwait(false);
        }

        private async Task RecoverExchangesAsync()
        {
            Dictionary<string, RecordedExchange> recordedExchangesCopy;
            lock (_recordedEntitiesLock)
            {
                recordedExchangesCopy = new Dictionary<string, RecordedExchange>(_recordedExchanges);
            }

            foreach (RecordedExchange rx in recordedExchangesCopy.Values)
            {
                try
                {
                    await rx.RecoverAsync().ConfigureAwait(false);
                }
                catch (Exception cause)
                {
                    string s = string.Format("Caught an exception while recovering exchange {0}: {1}",
                        rx.Name, cause.Message);
                    HandleTopologyRecoveryException(new TopologyRecoveryException(s, cause));
                }
            }
        }

        private async Task RecoverChannelsAsync()
        {
            AutorecoveringChannel[] copy;
            lock (_channels)
            {
                copy = _channels.ToArray();
            }

            foreach (AutorecoveringChannel m in copy)
            {
                await m.AutomaticallyRecoverAsync(this).ConfigureAwait(false);
            }
        }

        private async Task RecoverQueuesAsync()
        {
            Dictionary<string, RecordedQueue> recordedQueuesCopy;
            lock (_recordedEntitiesLock)
            {
                recordedQueuesCopy = new Dictionary<string, RecordedQueue>(_recordedQueues);
            }

            foreach (KeyValuePair<string, RecordedQueue> pair in recordedQueuesCopy)
            {
                string oldName = pair.Key;
                RecordedQueue rq = pair.Value;

                try
                {
                    await rq.RecoverAsync().ConfigureAwait(false);
                    string newName = rq.Name;

                    if (!oldName.Equals(newName))
                    {
                        // Make sure server-named queues are re-added with
                        // their new names.
                        // We only remove old name after we've updated the bindings and consumers,
                        // plus only for server-named queues, both to make sure we don't lose
                        // anything to recover. MK.
                        PropagateQueueNameChangeToBindings(oldName, newName);
                        PropagateQueueNameChangeToConsumers(oldName, newName);
                        // see rabbitmq/rabbitmq-dotnet-client#43
                        if (rq.IsServerNamed)
                        {
                            DeleteRecordedQueue(oldName);
                        }
                        RecordQueue(newName, rq);

                        foreach (EventHandler<QueueNameChangedAfterRecoveryEventArgs> h in QueueNameChangeAfterRecovery?.GetInvocationList() ?? Array.Empty<Delegate>())
                        {
                            try
                            {
                                var eventArgs = new QueueNameChangedAfterRecoveryEventArgs(oldName, newName);
                                h(this, eventArgs);
                            }
                            catch (Exception e)
                            {
                                var args = new CallbackExceptionEventArgs(e);
                                args.Detail["context"] = "OnQueueRecovery";
                                _delegate.OnCallbackException(args);
                            }
                        }
                    }
                }
                catch (Exception cause)
                {
                    string s = string.Format("Caught an exception while recovering queue {0}: {1}",
                        oldName, cause.Message);
                    HandleTopologyRecoveryException(new TopologyRecoveryException(s, cause));
                }
            }
        }

        private void RunRecoveryEventHandlers()
        {
            ThrowIfDisposed();
            foreach (EventHandler<EventArgs> reh in RecoverySucceeded?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try
                {
                    reh(this, EventArgs.Empty);
                }
                catch (Exception e)
                {
                    var args = new CallbackExceptionEventArgs(e);
                    args.Detail["context"] = "OnConnectionRecovery";
                    _delegate.OnCallbackException(args);
                }
            }
        }

        private bool ShouldTriggerConnectionRecovery(ShutdownEventArgs args) => args.Initiator == ShutdownInitiator.Peer ||
                    // happens when EOF is reached, e.g. due to RabbitMQ node
                    // connectivity loss or abrupt shutdown
                    args.Initiator == ShutdownInitiator.Library;

        private enum RecoveryCommand
        {
            /// <summary>
            /// Transition to auto-recovery state if not already in that state.
            /// </summary>
            BeginAutomaticRecovery,
            /// <summary>
            /// Attempt to recover connection. If connection is recovered, return
            /// to connected state.
            /// </summary>
            PerformAutomaticRecovery
        }


        private enum RecoveryConnectionState
        {
            /// <summary>
            /// Underlying connection is open.
            /// </summary>
            Connected,
            /// <summary>
            /// In the process of recovering underlying connection.
            /// </summary>
            Recovering
        }

        private Task _recoveryTask;
        private RecoveryConnectionState _recoveryLoopState = RecoveryConnectionState.Connected;

        private readonly Channel<RecoveryCommand> _recoveryLoopCommandQueue = Channel.CreateUnbounded<RecoveryCommand>(new UnboundedChannelOptions { AllowSynchronousContinuations = false, SingleReader = true, SingleWriter = false });

        /// <summary>
        /// This is the main loop for the auto-recovery thread.
        /// </summary>
        private async Task MainRecoveryLoop()
        {
            try
            {
                while (await _recoveryLoopCommandQueue.Reader.WaitToReadAsync().ConfigureAwait(false))
                {
                    while (_recoveryLoopCommandQueue.Reader.TryRead(out RecoveryCommand command))
                    {
                        switch (_recoveryLoopState)
                        {
                            case RecoveryConnectionState.Connected:
                                RecoveryLoopConnectedHandler(command);
                                break;
                            case RecoveryConnectionState.Recovering:
                                RecoveryLoopRecoveringHandler(command);
                                break;
                            default:
                                ESLog.Warn("RecoveryLoop state is out of range.");
                                break;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // expected when recovery cancellation token is set.
            }
            catch (Exception e)
            {
                ESLog.Error("Main recovery loop threw unexpected exception.", e);
            }
        }

        /// <summary>
        /// Cancels the main recovery loop and will block until the loop finishes, or the timeout
        /// expires, to prevent Close operations overlapping with recovery operations.
        /// </summary>
        private void StopRecoveryLoop()
        {
            _recoveryLoopCommandQueue.Writer.Complete();
            Task timeout = Task.Delay(_factory.RequestedConnectionTimeout);

            if (Task.WhenAny(_recoveryTask, timeout).Result == timeout)
            {
                ESLog.Warn("Timeout while trying to stop background AutorecoveringConnection recovery loop.");
            }
        }

        /// <summary>
        /// Handles commands when in the Recovering state.
        /// </summary>
        /// <param name="command"></param>
        private void RecoveryLoopRecoveringHandler(RecoveryCommand command)
        {
            switch (command)
            {
                case RecoveryCommand.BeginAutomaticRecovery:
                    ESLog.Info("Received request to BeginAutomaticRecovery, but already in Recovering state.");
                    break;
                case RecoveryCommand.PerformAutomaticRecovery:
                    if (TryPerformAutomaticRecovery())
                    {
                        _recoveryLoopState = RecoveryConnectionState.Connected;
                    }
                    else
                    {
                        ScheduleRecoveryRetry();
                    }

                    break;
                default:
                    ESLog.Warn($"RecoveryLoop command {command} is out of range.");
                    break;
            }
        }

        /// <summary>
        /// Handles commands when in the Connected state.
        /// </summary>
        /// <param name="command"></param>
        private void RecoveryLoopConnectedHandler(RecoveryCommand command)
        {
            switch (command)
            {
                case RecoveryCommand.PerformAutomaticRecovery:
                    ESLog.Warn("Not expecting PerformAutomaticRecovery commands while in the connected state.");
                    break;
                case RecoveryCommand.BeginAutomaticRecovery:
                    _recoveryLoopState = RecoveryConnectionState.Recovering;
                    ScheduleRecoveryRetry();
                    break;
                default:
                    ESLog.Warn($"RecoveryLoop command {command} is out of range.");
                    break;
            }
        }

        /// <summary>
        /// Schedule a background Task to signal the command queue when the retry duration has elapsed.
        /// </summary>
        private void ScheduleRecoveryRetry() => _ = Task
                .Delay(_factory.NetworkRecoveryInterval)
                .ContinueWith(t => _recoveryLoopCommandQueue.Writer.TryWrite(RecoveryCommand.PerformAutomaticRecovery));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }
    }
}
