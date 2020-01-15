// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2016 Pivotal Software, Inc.
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
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Framing.Impl
{
    internal sealed class AutorecoveringConnection : IConnection
    {
        private readonly object m_eventLock = new object();

        private readonly object manuallyClosedLock = new object();
        private Connection m_delegate;
        private ConnectionFactory m_factory;

        // list of endpoints provided on initial connection.
        // on re-connection, the next host in the line is chosen using
        // IHostnameSelector
        private IEndpointResolver endpoints;

        private readonly object m_recordedEntitiesLock = new object();

        private List<AutorecoveringModel> m_models = new List<AutorecoveringModel>();

        private ConcurrentDictionary<RecordedBinding, byte> m_recordedBindings =
            new ConcurrentDictionary<RecordedBinding, byte>();

        private EventHandler<ConnectionBlockedEventArgs> m_recordedBlockedEventHandlers;

        private IDictionary<string, RecordedConsumer> m_recordedConsumers =
            new ConcurrentDictionary<string, RecordedConsumer>();

        private IDictionary<string, RecordedExchange> m_recordedExchanges =
            new ConcurrentDictionary<string, RecordedExchange>();

        private IDictionary<string, RecordedQueue> m_recordedQueues =
            new ConcurrentDictionary<string, RecordedQueue>();

        private EventHandler<ShutdownEventArgs> m_recordedShutdownEventHandlers;
        private EventHandler<EventArgs> m_recordedUnblockedEventHandlers;
        private EventHandler<ConsumerTagChangedAfterRecoveryEventArgs> m_consumerTagChange;
        private EventHandler<QueueNameChangedAfterRecoveryEventArgs> m_queueNameChange;
        private EventHandler<EventArgs> m_recovery;
        private EventHandler<ConnectionRecoveryErrorEventArgs> m_connectionRecoveryError;

        public AutorecoveringConnection(ConnectionFactory factory, string clientProvidedName = null)
        {
            m_factory = factory;
            this.ClientProvidedName = clientProvidedName;
        }

        public event EventHandler<EventArgs> RecoverySucceeded
        {
            add
            {
                lock (m_eventLock)
                {
                    m_recovery += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_recovery -= value;
                }
            }
        }

        public event EventHandler<ConnectionRecoveryErrorEventArgs> ConnectionRecoveryError
        {
            add
            {
                lock (m_eventLock)
                {
                    m_connectionRecoveryError += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_connectionRecoveryError -= value;
                }
            }
        }

        public event EventHandler<CallbackExceptionEventArgs> CallbackException
        {
            add
            {
                lock (m_eventLock)
                {
                    m_delegate.CallbackException += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_delegate.CallbackException -= value;
                }
            }
        }

        public event EventHandler<ConnectionBlockedEventArgs> ConnectionBlocked
        {
            add
            {
                lock (m_eventLock)
                {
                    m_recordedBlockedEventHandlers += value;
                    m_delegate.ConnectionBlocked += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_recordedBlockedEventHandlers -= value;
                    m_delegate.ConnectionBlocked -= value;
                }
            }
        }

        public event EventHandler<ShutdownEventArgs> ConnectionShutdown
        {
            add
            {
                lock (m_eventLock)
                {
                    m_recordedShutdownEventHandlers += value;
                    m_delegate.ConnectionShutdown += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_recordedShutdownEventHandlers -= value;
                    m_delegate.ConnectionShutdown -= value;
                }
            }
        }

        public event EventHandler<EventArgs> ConnectionUnblocked
        {
            add
            {
                lock (m_eventLock)
                {
                    m_recordedUnblockedEventHandlers += value;
                    m_delegate.ConnectionUnblocked += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_recordedUnblockedEventHandlers -= value;
                    m_delegate.ConnectionUnblocked -= value;
                }
            }
        }

        public event EventHandler<ConsumerTagChangedAfterRecoveryEventArgs> ConsumerTagChangeAfterRecovery
        {
            add
            {
                lock (m_eventLock)
                {
                    m_consumerTagChange += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_consumerTagChange -= value;
                }
            }
        }

        public event EventHandler<QueueNameChangedAfterRecoveryEventArgs> QueueNameChangeAfterRecovery
        {
            add
            {
                lock (m_eventLock)
                {
                    m_queueNameChange += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_queueNameChange -= value;
                }
            }
        }

        public string ClientProvidedName { get; private set; }

        public ushort ChannelMax
        {
            get { return m_delegate.ChannelMax; }
        }

        public ConsumerWorkService ConsumerWorkService
        {
            get { return m_delegate.ConsumerWorkService; }
        }

        public IDictionary<string, object> ClientProperties
        {
            get { return m_delegate.ClientProperties; }
        }

        public ShutdownEventArgs CloseReason
        {
            get { return m_delegate.CloseReason; }
        }

        public AmqpTcpEndpoint Endpoint
        {
            get { return m_delegate.Endpoint; }
        }

        public uint FrameMax
        {
            get { return m_delegate.FrameMax; }
        }

        public ushort Heartbeat
        {
            get { return m_delegate.Heartbeat; }
        }

        public bool IsOpen
        {
            get { return m_delegate.IsOpen; }
        }

        public AmqpTcpEndpoint[] KnownHosts
        {
            get { return m_delegate.KnownHosts; }
            set { m_delegate.KnownHosts = value; }
        }

        public int LocalPort
        {
            get { return m_delegate.LocalPort; }
        }

        public ProtocolBase Protocol
        {
            get { return m_delegate.Protocol; }
        }

        public IDictionary<string, RecordedExchange> RecordedExchanges
        {
            get { return m_recordedExchanges; }
        }

        public IDictionary<string, RecordedQueue> RecordedQueues
        {
            get { return m_recordedQueues; }
        }

        public int RemotePort
        {
            get { return m_delegate.RemotePort; }
        }

        public IDictionary<string, object> ServerProperties
        {
            get { return m_delegate.ServerProperties; }
        }

        public IList<ShutdownReportEntry> ShutdownReport
        {
            get { return m_delegate.ShutdownReport; }
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
                    RecoverConnectionShutdownHandlers();
                    RecoverConnectionBlockedHandlers();
                    RecoverConnectionUnblockedHandlers();

                    RecoverModels();
                    if (m_factory.TopologyRecoveryEnabled)
                    {
                        RecoverEntities();
                        RecoverConsumers();
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

        public void Close(ShutdownEventArgs reason)
        {
            m_delegate.Close(reason);
        }

        public RecoveryAwareModel CreateNonRecoveringModel()
        {
            ISession session = m_delegate.CreateSession();
            var result = new RecoveryAwareModel(session);
            result._Private_ChannelOpen("");
            return result;
        }

        public void DeleteRecordedBinding(RecordedBinding rb)
        {
            lock (m_recordedEntitiesLock)
            {
                ((IDictionary<RecordedBinding, byte>)m_recordedBindings).Remove(rb);
            }
        }

        public RecordedConsumer DeleteRecordedConsumer(string consumerTag)
        {
            RecordedConsumer rc = null;
            lock (m_recordedEntitiesLock)
            {
                if (m_recordedConsumers.ContainsKey(consumerTag))
                {
                    rc = m_recordedConsumers[consumerTag];
                    m_recordedConsumers.Remove(consumerTag);
                }
            }

            return rc;
        }

        public void DeleteRecordedExchange(string name)
        {
            lock (m_recordedEntitiesLock)
            {
                m_recordedExchanges.Remove(name);

                // find bindings that need removal, check if some auto-delete exchanges
                // might need the same
                var bs = m_recordedBindings.Keys.Where(b => name.Equals(b.Destination));
                foreach (var b in bs)
                {
                    DeleteRecordedBinding(b);
                    MaybeDeleteRecordedAutoDeleteExchange(b.Source);
                }
            }
        }

        public void DeleteRecordedQueue(string name)
        {
            lock (m_recordedEntitiesLock)
            {
                m_recordedQueues.Remove(name);
                // find bindings that need removal, check if some auto-delete exchanges
                // might need the same
                var bs = m_recordedBindings.Keys.Where(b => name.Equals(b.Destination));
                foreach (var b in bs)
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
            lock (m_recordedEntitiesLock)
            {
                if (!HasMoreDestinationsBoundToExchange(m_recordedBindings.Keys, exchange))
                {
                    RecordedExchange rx;
                    m_recordedExchanges.TryGetValue(exchange, out rx);
                    // last binding where this exchange is the source is gone,
                    // remove recorded exchange
                    // if it is auto-deleted. See bug 26364.
                    if ((rx != null) && rx.IsAutoDelete)
                    {
                        m_recordedExchanges.Remove(exchange);
                    }
                }
            }
        }

        public void MaybeDeleteRecordedAutoDeleteQueue(string queue)
        {
            lock (m_recordedEntitiesLock)
            {
                if (!HasMoreConsumersOnQueue(m_recordedConsumers.Values, queue))
                {
                    RecordedQueue rq;
                    m_recordedQueues.TryGetValue(queue, out rq);
                    // last consumer on this connection is gone, remove recorded queue
                    // if it is auto-deleted. See bug 26364.
                    if ((rq != null) && rq.IsAutoDelete)
                    {
                        m_recordedQueues.Remove(queue);
                    }
                }
            }
        }

        public void RecordBinding(RecordedBinding rb)
        {
            lock (m_recordedEntitiesLock)
            {
                m_recordedBindings.TryAdd(rb, 0);
            }
        }

        public void RecordConsumer(string name, RecordedConsumer c)
        {
            lock (m_recordedEntitiesLock)
            {
                if (!m_recordedConsumers.ContainsKey(name))
                {
                    m_recordedConsumers.Add(name, c);
                }
            }
        }

        public void RecordExchange(string name, RecordedExchange x)
        {
            lock (m_recordedEntitiesLock)
            {
                m_recordedExchanges[name] = x;
            }
        }

        public void RecordQueue(string name, RecordedQueue q)
        {
            lock (m_recordedEntitiesLock)
            {
                m_recordedQueues[name] = q;
            }
        }

        public override string ToString()
        {
            return string.Format("AutorecoveringConnection({0},{1},{2})", m_delegate.Id, Endpoint, GetHashCode());
        }

        public void UnregisterModel(AutorecoveringModel model)
        {
            lock (m_models)
            {
                m_models.Remove(model);
            }
        }

        public void Init()
        {
            this.Init(m_factory.EndpointResolverFactory(new List<AmqpTcpEndpoint> { m_factory.Endpoint }));
        }

        public void Init(IEndpointResolver endpoints)
        {
            this.endpoints = endpoints;
            var fh = endpoints.SelectOne(m_factory.CreateFrameHandler);
            this.Init(fh);
        }

        private void Init(IFrameHandler fh)
        {
            m_delegate = new Connection(m_factory, false,
                fh, this.ClientProvidedName);

            m_recoveryTask = Task.Run(MainRecoveryLoop);

            EventHandler<ShutdownEventArgs> recoveryListener = (_, args) =>
            {
                if (ShouldTriggerConnectionRecovery(args))
                {
                    if (!m_recoveryLoopCommandQueue.TryAdd(RecoveryCommand.BeginAutomaticRecovery))
                    {
                        ESLog.Warn("Failed to notify RecoveryLoop to BeginAutomaticRecovery.");
                    }
                }
            };
            lock (m_eventLock)
            {
                ConnectionShutdown += recoveryListener;
                m_recordedShutdownEventHandlers += recoveryListener;
            }
        }

        ///<summary>API-side invocation of updating the secret.</summary>
        public void UpdateSecret(string newSecret, string reason)
        {
            EnsureIsOpen();
            m_delegate.UpdateSecret(newSecret, reason);
            m_factory.Password = newSecret;
        }

        ///<summary>API-side invocation of connection abort.</summary>
        public void Abort()
        {
            StopRecoveryLoop();
            if (m_delegate.IsOpen)
                m_delegate.Abort();
        }

        ///<summary>API-side invocation of connection abort.</summary>
        public void Abort(ushort reasonCode, string reasonText)
        {
            StopRecoveryLoop();
            if (m_delegate.IsOpen)
                m_delegate.Abort(reasonCode, reasonText);
        }

        ///<summary>API-side invocation of connection abort with timeout.</summary>
        public void Abort(int timeout)
        {
            StopRecoveryLoop();
            if (m_delegate.IsOpen)
                m_delegate.Abort(timeout);
        }

        ///<summary>API-side invocation of connection abort with timeout.</summary>
        public void Abort(TimeSpan timeout)
        {
            StopRecoveryLoop();
            if (m_delegate.IsOpen)
                m_delegate.Abort(timeout);
        }

        ///<summary>API-side invocation of connection abort with timeout.</summary>
        public void Abort(ushort reasonCode, string reasonText, int timeout)
        {
            StopRecoveryLoop();
            if (m_delegate.IsOpen)
                m_delegate.Abort(reasonCode, reasonText, timeout);
        }

        ///<summary>API-side invocation of connection abort with timeout.</summary>
        public void Abort(ushort reasonCode, string reasonText, TimeSpan timeout)
        {
            StopRecoveryLoop();
            if (m_delegate.IsOpen)
                m_delegate.Abort(reasonCode, reasonText, timeout);
        }

        ///<summary>API-side invocation of connection.close.</summary>
        public void Close()
        {
            StopRecoveryLoop();
            if (m_delegate.IsOpen)
                m_delegate.Close();
        }

        ///<summary>API-side invocation of connection.close.</summary>
        public void Close(ushort reasonCode, string reasonText)
        {
            StopRecoveryLoop();
            if (m_delegate.IsOpen)
                m_delegate.Close(reasonCode, reasonText);
        }

        ///<summary>API-side invocation of connection.close with timeout.</summary>
        public void Close(int timeout)
        {
            StopRecoveryLoop();
            if (m_delegate.IsOpen)
                m_delegate.Close(timeout);
        }

        ///<summary>API-side invocation of connection.close with timeout.</summary>
        public void Close(ushort reasonCode, string reasonText, int timeout)
        {
            StopRecoveryLoop();
            if (m_delegate.IsOpen)
                m_delegate.Close(reasonCode, reasonText, timeout);
        }

        public IModel CreateModel()
        {
            EnsureIsOpen();
            AutorecoveringModel m;
            m = new AutorecoveringModel(this,
                CreateNonRecoveringModel());
            lock (m_models)
            {
                m_models.Add(m);
            }
            return m;
        }

        public void HandleConnectionBlocked(string reason)
        {
            m_delegate.HandleConnectionBlocked(reason);
        }

        public void HandleConnectionUnblocked()
        {
            m_delegate.HandleConnectionUnblocked();
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
                }
                catch (Exception)
                {
                    // TODO: log
                }
                finally
                {
                    m_models.Clear();
                    m_recordedBlockedEventHandlers = null;
                    m_recordedShutdownEventHandlers = null;
                    m_recordedUnblockedEventHandlers = null;
                }
            }

            // dispose unmanaged resources
        }

        private void EnsureIsOpen()
        {
            m_delegate.EnsureIsOpen();
        }

        private void HandleTopologyRecoveryException(TopologyRecoveryException e)
        {
            ESLog.Error("Topology recovery exception", e);
        }

        private void PropagateQueueNameChangeToBindings(string oldName, string newName)
        {
            lock (m_recordedBindings)
            {
                var bs = m_recordedBindings.Keys.Where(b => b.Destination.Equals(oldName));
                foreach (RecordedBinding b in bs)
                {
                    b.Destination = newName;
                }
            }
        }

        private void PropagateQueueNameChangeToConsumers(string oldName, string newName)
        {
            lock (m_recordedBindings)
            {
                IEnumerable<KeyValuePair<string, RecordedConsumer>> cs = m_recordedConsumers.
                    Where(pair => pair.Value.Queue.Equals(oldName));
                foreach (KeyValuePair<string, RecordedConsumer> c in cs)
                {
                    c.Value.Queue = newName;
                }
            }
        }

        private void RecoverBindings()
        {
            foreach (var b in m_recordedBindings.Keys)
            {
                try
                {
                    b.Recover();
                }
                catch (Exception cause)
                {
                    string s = String.Format("Caught an exception while recovering binding between {0} and {1}: {2}",
                        b.Source, b.Destination, cause.Message);
                    HandleTopologyRecoveryException(new TopologyRecoveryException(s, cause));
                }
            }
        }

        private void RecoverConnectionBlockedHandlers()
        {
            lock (m_eventLock)
            {
                m_delegate.ConnectionBlocked += m_recordedBlockedEventHandlers;
            }
        }

        private bool TryRecoverConnectionDelegate()
        {
            try
            {
                var fh = endpoints.SelectOne(m_factory.CreateFrameHandler);
                m_delegate = new Connection(m_factory, false, fh, this.ClientProvidedName);
                return true;
            }
            catch (Exception e)
            {
                ESLog.Error("Connection recovery exception.", e);
                // Trigger recovery error events
                var handler = m_connectionRecoveryError;
                if (handler != null)
                {
                    var args = new ConnectionRecoveryErrorEventArgs(e);
                    foreach (EventHandler<ConnectionRecoveryErrorEventArgs> h in handler.GetInvocationList())
                    {
                        try
                        {
                            h(this, args);
                        }
                        catch (Exception ex)
                        {
                            var a = new CallbackExceptionEventArgs(ex);
                            a.Detail["context"] = "OnConnectionRecoveryError";
                            m_delegate.OnCallbackException(a);
                        }
                    }
                }
            }

            return false;
        }

        private void RecoverConnectionShutdownHandlers()
        {
            m_delegate.ConnectionShutdown += m_recordedShutdownEventHandlers;
        }

        private void RecoverConnectionUnblockedHandlers()
        {
            m_delegate.ConnectionUnblocked += m_recordedUnblockedEventHandlers;
        }

        private void RecoverConsumers()
        {
            foreach (KeyValuePair<string, RecordedConsumer> pair in m_recordedConsumers)
            {
                string tag = pair.Key;
                RecordedConsumer cons = pair.Value;

                try
                {
                    string newTag = cons.Recover();
                    lock (m_recordedConsumers)
                    {
                        // make sure server-generated tags are re-added
                        m_recordedConsumers.Remove(tag);
                        m_recordedConsumers.Add(newTag, cons);
                    }

                    if (m_consumerTagChange != null)
                    {
                        foreach (EventHandler<ConsumerTagChangedAfterRecoveryEventArgs> h in m_consumerTagChange.GetInvocationList())
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
                                m_delegate.OnCallbackException(args);
                            }
                        }
                    }
                }
                catch (Exception cause)
                {
                    string s = String.Format("Caught an exception while recovering consumer {0} on queue {1}: {2}",
                        tag, cons.Queue, cause.Message);
                    HandleTopologyRecoveryException(new TopologyRecoveryException(s, cause));
                }
            }
        }

        private void RecoverEntities()
        {
            // The recovery sequence is the following:
            //
            // 1. Recover exchanges
            // 2. Recover queues
            // 3. Recover bindings
            // 4. Recover consumers
            RecoverExchanges();
            RecoverQueues();
            RecoverBindings();
        }

        private void RecoverExchanges()
        {
            foreach (RecordedExchange rx in m_recordedExchanges.Values)
            {
                try
                {
                    rx.Recover();
                }
                catch (Exception cause)
                {
                    string s = String.Format("Caught an exception while recovering exchange {0}: {1}",
                        rx.Name, cause.Message);
                    HandleTopologyRecoveryException(new TopologyRecoveryException(s, cause));
                }
            }
        }

        private void RecoverModels()
        {
            lock (m_models)
            {
                foreach (AutorecoveringModel m in m_models)
                {
                    m.AutomaticallyRecover(this, m_delegate);
                }
            }
        }

        private void RecoverQueues()
        {
            lock (m_recordedQueues)
            {
                foreach (KeyValuePair<string, RecordedQueue> pair in m_recordedQueues)
                {
                    string oldName = pair.Key;
                    RecordedQueue rq = pair.Value;

                    try
                    {
                        rq.Recover();
                        string newName = rq.Name;

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

                        if (m_queueNameChange != null)
                        {
                            foreach (EventHandler<QueueNameChangedAfterRecoveryEventArgs> h in m_queueNameChange.GetInvocationList())
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
                                    m_delegate.OnCallbackException(args);
                                }
                            }
                        }
                    }
                    catch (Exception cause)
                    {
                        string s = String.Format("Caught an exception while recovering queue {0}: {1}",
                            oldName, cause.Message);
                        HandleTopologyRecoveryException(new TopologyRecoveryException(s, cause));
                    }
                }
            }
        }

        private void RunRecoveryEventHandlers()
        {
            EventHandler<EventArgs> handler = m_recovery;
            if (handler != null)
            {
                foreach (EventHandler<EventArgs> reh in handler.GetInvocationList())
                {
                    try
                    {
                        reh(this, EventArgs.Empty);
                    }
                    catch (Exception e)
                    {
                        var args = new CallbackExceptionEventArgs(e);
                        args.Detail["context"] = "OnConnectionRecovery";
                        m_delegate.OnCallbackException(args);
                    }
                }
            }
        }

        private bool ShouldTriggerConnectionRecovery(ShutdownEventArgs args)
        {
            return (args.Initiator == ShutdownInitiator.Peer ||
                    // happens when EOF is reached, e.g. due to RabbitMQ node
                    // connectivity loss or abrupt shutdown
                    args.Initiator == ShutdownInitiator.Library);
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

        private Task m_recoveryTask;
        private RecoveryConnectionState m_recoveryLoopState = RecoveryConnectionState.Connected;

        private readonly BlockingCollection<RecoveryCommand> m_recoveryLoopCommandQueue = new BlockingCollection<RecoveryCommand>();
        private readonly CancellationTokenSource m_recoveryCancellationToken = new CancellationTokenSource();
        private readonly TaskCompletionSource<int> m_recoveryLoopComplete = new TaskCompletionSource<int>();

        /// <summary>
        /// This is the main loop for the auto-recovery thread.
        /// </summary>
        private async Task MainRecoveryLoop()
        {
            try
            {
                while (m_recoveryLoopCommandQueue.TryTake(out var command, -1, m_recoveryCancellationToken.Token))
                {
                    switch (m_recoveryLoopState)
                    {
                        case RecoveryConnectionState.Connected:
                            await RecoveryLoopConnectedHandler(command).ConfigureAwait(false);
                            break;
                        case RecoveryConnectionState.Recovering:
                            await RecoveryLoopRecoveringHandler(command).ConfigureAwait(false);
                            break;
                        default:
                            ESLog.Warn("RecoveryLoop state is out of range.");
                            break;
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

            m_recoveryLoopComplete.SetResult(0);
        }

        /// <summary>
        /// Cancels the main recovery loop and will block until the loop finishes, or the timeout
        /// expires, to prevent Close operations overlapping with recovery operations.
        /// </summary>
        private void StopRecoveryLoop()
        {
            m_recoveryCancellationToken.Cancel();
            if (!m_recoveryLoopComplete.Task.Wait(m_factory.RequestedConnectionTimeout))
            {
                ESLog.Warn("Timeout while trying to stop background AutorecoveringConnection recovery loop.");
            }
        }

        /// <summary>
        /// Handles commands when in the Recovering state.
        /// </summary>
        /// <param name="command"></param>
        private async Task RecoveryLoopRecoveringHandler(RecoveryCommand command)
        {
            switch (command)
            {
                case RecoveryCommand.BeginAutomaticRecovery:
                    ESLog.Info("Received request to BeginAutomaticRecovery, but already in Recovering state.");
                    break;
                case RecoveryCommand.PerformAutomaticRecovery:
                    if (TryPerformAutomaticRecovery())
                    {
                        m_recoveryLoopState = RecoveryConnectionState.Connected;
                    }
                    else
                    {
                        await Task.Delay(m_factory.NetworkRecoveryInterval);
                        m_recoveryLoopCommandQueue.TryAdd(RecoveryCommand.PerformAutomaticRecovery);
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
        private async Task RecoveryLoopConnectedHandler(RecoveryCommand command)
        {
            switch (command)
            {
                case RecoveryCommand.PerformAutomaticRecovery:
                    ESLog.Warn("Not expecting PerformAutomaticRecovery commands while in the connected state.");
                    break;
                case RecoveryCommand.BeginAutomaticRecovery:
                    m_recoveryLoopState = RecoveryConnectionState.Recovering;
                    await Task.Delay(m_factory.NetworkRecoveryInterval).ConfigureAwait(false);
                    m_recoveryLoopCommandQueue.TryAdd(RecoveryCommand.PerformAutomaticRecovery);
                    break;
                default:
                    ESLog.Warn($"RecoveryLoop command {command} is out of range.");
                    break;
            }
        }
    }
}
