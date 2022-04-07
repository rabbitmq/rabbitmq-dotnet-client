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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.Framing.Impl
{
    internal sealed class AutorecoveringConnection : IAutorecoveringConnection
    {
        private bool _disposed = false;
        private readonly object _eventLock = new object();

        private Connection _delegate;
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

        private readonly List<AutorecoveringModel> _models = new List<AutorecoveringModel>();

        private EventHandler<ConnectionBlockedEventArgs> _recordedBlockedEventHandlers;
        private EventHandler<ShutdownEventArgs> _recordedShutdownEventHandlers;
        private EventHandler<EventArgs> _recordedUnblockedEventHandlers;

        public AutorecoveringConnection(ConnectionFactory factory, string clientProvidedName = null)
        {
            _factory = factory;
            ClientProvidedName = clientProvidedName;
        }

        public event EventHandler<EventArgs> RecoverySucceeded;
        public event EventHandler<ConnectionRecoveryErrorEventArgs> ConnectionRecoveryError;
        public event EventHandler<CallbackExceptionEventArgs> CallbackException
        {
            add
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                lock (_eventLock)
                {
                    _delegate.CallbackException += value;
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
                    _delegate.CallbackException -= value;
                }
            }
        }

        public event EventHandler<ConnectionBlockedEventArgs> ConnectionBlocked
        {
            add
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                lock (_eventLock)
                {
                    _recordedBlockedEventHandlers += value;
                    _delegate.ConnectionBlocked += value;
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
                    _recordedBlockedEventHandlers -= value;
                    _delegate.ConnectionBlocked -= value;
                }
            }
        }

        public event EventHandler<ShutdownEventArgs> ConnectionShutdown
        {
            add
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                lock (_eventLock)
                {
                    _recordedShutdownEventHandlers += value;
                    _delegate.ConnectionShutdown += value;
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
                    _recordedShutdownEventHandlers -= value;
                    _delegate.ConnectionShutdown -= value;
                }
            }
        }

        public event EventHandler<EventArgs> ConnectionUnblocked
        {
            add
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                lock (_eventLock)
                {
                    _recordedUnblockedEventHandlers += value;
                    _delegate.ConnectionUnblocked += value;
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
                    _recordedUnblockedEventHandlers -= value;
                    _delegate.ConnectionUnblocked -= value;
                }
            }
        }

        public event EventHandler<ConsumerTagChangedAfterRecoveryEventArgs> ConsumerTagChangeAfterRecovery;
        public event EventHandler<QueueNameChangedAfterRecoveryEventArgs> QueueNameChangeAfterRecovery;

        public string ClientProvidedName { get; private set; }

        public ushort ChannelMax
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                return _delegate.ChannelMax;
            }
        }

        public ConsumerWorkService ConsumerWorkService
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                return _delegate.ConsumerWorkService;
            }
        }

        public IDictionary<string, object> ClientProperties
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                return _delegate.ClientProperties;
            }
        }

        public ShutdownEventArgs CloseReason
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                return _delegate.CloseReason;
            }
        }

        public AmqpTcpEndpoint Endpoint
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                return _delegate.Endpoint;
            }
        }

        public uint FrameMax
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                return _delegate.FrameMax;
            }
        }

        public TimeSpan Heartbeat
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                return _delegate.Heartbeat;
            }
        }

        public bool IsOpen
        {
            get
            {
                if (_delegate == null)
                {
                    return false;
                }
                else
                {
                    return _delegate.IsOpen;
                }
            }
        }

        public AmqpTcpEndpoint[] KnownHosts
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                return _delegate.KnownHosts;
            }
            set
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                _delegate.KnownHosts = value;
            }
        }

        public int LocalPort
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                return _delegate.LocalPort;
            }
        }

        public ProtocolBase Protocol
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                return _delegate.Protocol;
            }
        }

        public IDictionary<string, RecordedExchange> RecordedExchanges { get; } = new ConcurrentDictionary<string, RecordedExchange>();

        public IDictionary<string, RecordedQueue> RecordedQueues { get; } = new ConcurrentDictionary<string, RecordedQueue>();

        public int RemotePort
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                return _delegate.RemotePort;
            }
        }

        public IDictionary<string, object> ServerProperties
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                return _delegate.ServerProperties;
            }
        }

        public IList<ShutdownReportEntry> ShutdownReport
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                return _delegate.ShutdownReport;
            }
        }

        IProtocol IConnection.Protocol
        {
            get { return Endpoint.Protocol; }
        }

        private bool TryPerformAutomaticRecovery()
        {
            ESLog.Info("Performing automatic recovery");

            try
            {
                if (TryRecoverConnectionDelegate())
                {
                    lock (_recordedEntitiesLock)
                    {
                        RecoverConnectionShutdownHandlers();
                        RecoverConnectionBlockedHandlers();
                        RecoverConnectionUnblockedHandlers();

                        if (_factory.TopologyRecoveryEnabled)
                        {
                            // The recovery sequence is the following:
                            //
                            // 1. Recover exchanges
                            // 2. Recover queues
                            // 3. Recover bindings
                            // 4. Recover consumers
                            using (var recoveryModel = _delegate.CreateModel())
                            {
                                RecoverExchanges(recoveryModel);
                                RecoverQueues(recoveryModel);
                                RecoverBindings(recoveryModel);
                            }
                        }

                        RecoverModelsAndItsConsumers();
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

                try
                {
                    /*
                     * To prevent connection leaks on the next recovery loop,
                     * we abort the delegated connection if it is still open.
                     * We do not want to block the abort forever (potentially deadlocking recovery),
                     * so we specify the same configured timeout used for connection.
                     */
                    if (_delegate?.IsOpen == true)
                    {
                        _delegate.Abort(Constants.InternalError, "FailedAutoRecovery", ShutdownInitiator.Library, _factory.RequestedConnectionTimeout);
                    }
                }
                catch (Exception e2)
                {
                    ESLog.Warn("Exception when aborting previous auto recovery connection.", e2);
                }
            }

            return false;
        }

        public void Close(ShutdownEventArgs reason)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.Close(reason);
        }

        public RecoveryAwareModel CreateNonRecoveringModel()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            ISession session = _delegate.CreateSession();
            var result = new RecoveryAwareModel(session);
            result.ContinuationTimeout = _factory.ContinuationTimeout;
            result._Private_ChannelOpen("");
            return result;
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
                    if ((rx != null) && rx.IsAutoDelete)
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
                    if ((rq != null) && rq.IsAutoDelete)
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

        public override string ToString()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            return string.Format("AutorecoveringConnection({0},{1},{2})", _delegate.Id, Endpoint, GetHashCode());
        }

        public void UnregisterModel(AutorecoveringModel model)
        {
            lock (_models)
            {
                _models.Remove(model);
            }
        }

        public void Init()
        {
            Init(_factory.EndpointResolverFactory(new List<AmqpTcpEndpoint> { _factory.Endpoint }));
        }

        public void Init(IEndpointResolver endpoints)
        {
            _endpoints = endpoints;
            IFrameHandler fh = endpoints.SelectOne(_factory.CreateFrameHandler);
            Init(fh);
        }

        internal IFrameHandler FrameHandler
        {
            get
            {
                return _delegate.FrameHandler;
            }
        }

        private void Init(IFrameHandler fh)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate = new Connection(_factory, false, fh, _factory.MemoryPool, ClientProvidedName);

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
            }
        }

        ///<summary>API-side invocation of updating the secret.</summary>
        public void UpdateSecret(string newSecret, string reason)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            EnsureIsOpen();
            _delegate.UpdateSecret(newSecret, reason);
            _factory.Password = newSecret;
        }

        ///<summary>API-side invocation of connection abort.</summary>
        public void Abort()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            StopRecoveryLoop();
            if (_delegate.IsOpen)
            {
                _delegate.Abort();
            }
        }

        ///<summary>API-side invocation of connection abort.</summary>
        public void Abort(ushort reasonCode, string reasonText)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            StopRecoveryLoop();
            if (_delegate.IsOpen)
            {
                _delegate.Abort(reasonCode, reasonText);
            }
        }

        ///<summary>API-side invocation of connection abort with timeout.</summary>
        public void Abort(TimeSpan timeout)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            StopRecoveryLoop();
            if (_delegate.IsOpen)
            {
                _delegate.Abort(timeout);
            }
        }

        ///<summary>API-side invocation of connection abort with timeout.</summary>
        public void Abort(ushort reasonCode, string reasonText, TimeSpan timeout)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            StopRecoveryLoop();
            if (_delegate.IsOpen)
            {
                _delegate.Abort(reasonCode, reasonText, timeout);
            }
        }

        ///<summary>API-side invocation of connection.close.</summary>
        public void Close()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            StopRecoveryLoop();
            if (_delegate.IsOpen)
            {
                _delegate.Close();
            }
        }

        ///<summary>API-side invocation of connection.close.</summary>
        public void Close(ushort reasonCode, string reasonText)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            StopRecoveryLoop();
            if (_delegate.IsOpen)
            {
                _delegate.Close(reasonCode, reasonText);
            }
        }

        ///<summary>API-side invocation of connection.close with timeout.</summary>
        public void Close(TimeSpan timeout)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            StopRecoveryLoop();
            if (_delegate.IsOpen)
            {
                _delegate.Close(timeout);
            }
        }

        ///<summary>API-side invocation of connection.close with timeout.</summary>
        public void Close(ushort reasonCode, string reasonText, TimeSpan timeout)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            StopRecoveryLoop();
            if (_delegate.IsOpen)
            {
                _delegate.Close(reasonCode, reasonText, timeout);
            }
        }

        public IModel CreateModel()
        {
            EnsureIsOpen();
            AutorecoveringModel m = new AutorecoveringModel(this, CreateNonRecoveringModel());
            lock (_models)
            {
                _models.Add(m);
            }
            return m;
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
        }

        public void HandleConnectionBlocked(string reason)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleConnectionBlocked(reason);
        }

        public void HandleConnectionUnblocked()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.HandleConnectionUnblocked();
        }

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
                    _models.Clear();
                    _delegate = null;
                    _recordedBlockedEventHandlers = null;
                    _recordedShutdownEventHandlers = null;
                    _recordedUnblockedEventHandlers = null;

                    _disposed = true;
                }
            }
        }

        private void EnsureIsOpen()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.EnsureIsOpen();
        }

        private void HandleTopologyRecoveryException(TopologyRecoveryException e)
        {
            ESLog.Error("Topology recovery exception", e);
            if (e.InnerException is AlreadyClosedException || e.InnerException is OperationInterruptedException || e.InnerException is TimeoutException)
            {
                throw e;
            }
            ESLog.Info($"Will not retry recovery because of {e.InnerException?.GetType().FullName}: it's not a known problem with connectivty, ignoring it", e);

        }

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

        private void RecoverBindings(IModel model)
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
                    b.Recover(model);
                }
                catch (Exception cause)
                {
                    string s = string.Format("Caught an exception while recovering binding between {0} and {1}: {2}",
                        b.Source, b.Destination, cause.Message);
                    HandleTopologyRecoveryException(new TopologyRecoveryException(s, cause));
                }
            }
        }

        private void RecoverConnectionBlockedHandlers()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            lock (_eventLock)
            {
                _delegate.ConnectionBlocked += _recordedBlockedEventHandlers;
            }
        }

        private bool TryRecoverConnectionDelegate()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            try
            {
                IFrameHandler fh = _endpoints.SelectOne(_factory.CreateFrameHandler);
                _delegate = new Connection(_factory, false, fh, _factory.MemoryPool, ClientProvidedName);
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

        private void RecoverConnectionShutdownHandlers()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.ConnectionShutdown += _recordedShutdownEventHandlers;
        }

        private void RecoverConnectionUnblockedHandlers()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _delegate.ConnectionUnblocked += _recordedUnblockedEventHandlers;
        }

        internal void RecoverConsumers(AutorecoveringModel modelToRecover, IModel channelToUse)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            Dictionary<string, RecordedConsumer> recordedConsumersCopy;
            lock (_recordedConsumers)
            {
                recordedConsumersCopy = new Dictionary<string, RecordedConsumer>(_recordedConsumers);
            }

            foreach (KeyValuePair<string, RecordedConsumer> pair in recordedConsumersCopy)
            {
                RecordedConsumer cons = pair.Value;
                if (cons.Model != modelToRecover)
                {
                    continue;
                }

                string tag = pair.Key;
                try
                {
                    string newTag = cons.Recover(channelToUse);
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
                    string s = string.Format("Caught an exception while recovering consumer {0} on queue {1}: {2}",
                        tag, cons.Queue, cause.Message);
                    HandleTopologyRecoveryException(new TopologyRecoveryException(s, cause));
                }
            }
        }

        private void RecoverExchanges(IModel model)
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
                    rx.Recover(model);
                }
                catch (Exception cause)
                {
                    string s = string.Format("Caught an exception while recovering exchange {0}: {1}",
                        rx.Name, cause.Message);
                    HandleTopologyRecoveryException(new TopologyRecoveryException(s, cause));
                }
            }
        }

        private void RecoverModelsAndItsConsumers()
        {
            lock (_models)
            {
                foreach (AutorecoveringModel m in _models)
                {
                    m.AutomaticallyRecover(this, _factory.TopologyRecoveryEnabled);
                }
            }
        }

        private void RecoverQueues(IModel model)
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
                    rq.Recover(model);
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
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

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

        private bool ShouldTriggerConnectionRecovery(ShutdownEventArgs args)
        {
            return args.Initiator == ShutdownInitiator.Peer ||
                    // happens when EOF is reached, e.g. due to RabbitMQ node
                    // connectivity loss or abrupt shutdown
                    args.Initiator == ShutdownInitiator.Library;
        }

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
        private void ScheduleRecoveryRetry()
        {
            _ = Task
                .Delay(_factory.NetworkRecoveryInterval)
                .ContinueWith(t => _recoveryLoopCommandQueue.Writer.TryWrite(RecoveryCommand.PerformAutomaticRecovery));
        }
    }
}
