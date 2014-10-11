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

using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Threading;

using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace RabbitMQ.Client.Framing.Impl
{
    public class AutorecoveringConnection : IConnection, NetworkConnection, IRecoverable
    {
        protected ConnectionFactory m_factory;
        protected Connection m_delegate;

        public readonly object m_eventLock = new object();
        public readonly object m_recordedEntitiesLock = new object();

        private RecoveryEventHandler m_recovery;
        private QueueNameChangeAfterRecoveryEventHandler m_queueNameChange;
        private ConsumerTagChangeAfterRecoveryEventHandler m_consumerTagChange;

        protected List<ConnectionShutdownEventHandler> m_recordedShutdownEventHandlers =
            new List<ConnectionShutdownEventHandler>();
        protected List<ConnectionBlockedEventHandler> m_recordedBlockedEventHandlers =
            new List<ConnectionBlockedEventHandler>();
        protected List<ConnectionUnblockedEventHandler> m_recordedUnblockedEventHandlers =
            new List<ConnectionUnblockedEventHandler>();
        protected List<AutorecoveringModel> m_models =
            new List<AutorecoveringModel>();

        protected IDictionary<string, RecordedExchange> m_recordedExchanges =
            new Dictionary<string, RecordedExchange>();
        protected IDictionary<string, RecordedQueue> m_recordedQueues =
            new Dictionary<string, RecordedQueue>();
        protected IDictionary<string, RecordedConsumer> m_recordedConsumers =
            new Dictionary<string, RecordedConsumer>();
        protected HashSet<RecordedBinding> m_recordedBindings =
            new HashSet<RecordedBinding>();

        public AutorecoveringConnection(ConnectionFactory factory)
        {
            this.m_factory = factory;
        }

        public void init()
        {
            this.m_delegate = new Connection(m_factory, false, m_factory.CreateFrameHandler());

            var self = this;
            ConnectionShutdownEventHandler recoveryListener = (_, args) =>
                {
                    if(args.Initiator == ShutdownInitiator.Peer)
                    {
                        try
                        {
                            self.BeginAutomaticRecovery();
                        } catch (Exception e)
                        {
                            // TODO: logging
                            Console.WriteLine("BeginAutomaticRecovery() failed: {0}", e);
                        }
                    }
                };
            lock(this.m_eventLock)
            {
                this.ConnectionShutdown += recoveryListener;
                this.m_recordedShutdownEventHandlers.Add(recoveryListener);
            }
        }


        public event ConnectionShutdownEventHandler ConnectionShutdown
        {
            add
            {
                lock(this.m_eventLock)
                {
                    m_recordedShutdownEventHandlers.Add(value);
                    m_delegate.ConnectionShutdown += value;
                }
            }
            remove
            {
                lock(this.m_eventLock)
                {
                    m_recordedShutdownEventHandlers.Remove(value);
                    m_delegate.ConnectionShutdown -= value;
                }
            }
        }

        public event ConnectionBlockedEventHandler ConnectionBlocked
        {
            add
            {
                lock(this.m_eventLock)
                {
                    m_recordedBlockedEventHandlers.Add(value);
                    m_delegate.ConnectionBlocked += value;
                }
            }
            remove
            {
                lock(this.m_eventLock)
                {
                    m_recordedBlockedEventHandlers.Remove(value);
                    m_delegate.ConnectionBlocked -= value;
                }
            }
        }

        public event ConnectionUnblockedEventHandler ConnectionUnblocked
        {
            add
            {
                lock(this.m_eventLock)
                {
                    m_recordedUnblockedEventHandlers.Add(value);
                    m_delegate.ConnectionUnblocked += value;
                }
            }
            remove
            {
                lock(this.m_eventLock)
                {
                    m_recordedUnblockedEventHandlers.Remove(value);
                    m_delegate.ConnectionUnblocked -= value;
                }
            }
        }

        public void HandleConnectionBlocked(string reason)
        {
            m_delegate.HandleConnectionBlocked(reason);
        }

        public void HandleConnectionUnblocked()
        {
            m_delegate.HandleConnectionUnblocked();
        }

        public event CallbackExceptionEventHandler CallbackException
        {
            add
            {
                lock(this.m_eventLock)
                {
                    m_delegate.CallbackException += value;
                }
            }
            remove
            {
                lock(this.m_eventLock)
                {
                    m_delegate.CallbackException -= value;
                }
            }
        }

        public event RecoveryEventHandler Recovery
        {
            add
            {
                lock(this.m_eventLock)
                {
                    this.m_recovery += value;
                }
            }
            remove
            {
                lock(this.m_eventLock)
                {
                    this.m_recovery -= value;
                }
            }
        }

        public event QueueNameChangeAfterRecoveryEventHandler QueueNameChangeAfterRecovery
        {
            add
            {
                lock(this.m_eventLock)
                {
                    this.m_queueNameChange += value;
                }
            }
            remove
            {
                lock(this.m_eventLock)
                {
                    this.m_queueNameChange -= value;
                }
            }
        }

        public event ConsumerTagChangeAfterRecoveryEventHandler ConsumerTagChangeAfterRecovery
        {
            add
            {
                lock(this.m_eventLock)
                {
                    this.m_consumerTagChange += value;
                }
            }
            remove
            {
                lock(this.m_eventLock)
                {
                    this.m_consumerTagChange -= value;
                }
            }
        }

        public AmqpTcpEndpoint Endpoint
        {
            get
            {
                return m_delegate.Endpoint;
            }
        }

        public EndPoint LocalEndPoint
        {
            get { return m_delegate.LocalEndPoint; }
        }

        public EndPoint RemoteEndPoint
        {
            get { return m_delegate.RemoteEndPoint; }
        }

        public int LocalPort
        {
            get
            {
                return m_delegate.LocalPort;
            }
        }
        public int RemotePort
        {
            get
            {
                return m_delegate.RemotePort;
            }
        }

        IProtocol IConnection.Protocol
        {
            get
            {
                return Endpoint.Protocol;
            }
        }

        public ProtocolBase Protocol
        {
            get
            {
                return (ProtocolBase)m_delegate.Protocol;
            }
        }

        public ushort ChannelMax
        {
            get
            {
                return m_delegate.ChannelMax;
            }
        }

        public uint FrameMax
        {
            get
            {
                return m_delegate.FrameMax;
            }
        }

        public ushort Heartbeat
        {
            get
            {
                return m_delegate.Heartbeat;
            }
        }

        public IDictionary<string, object> ClientProperties
        {
            get
            {
                return m_delegate.ClientProperties;
            }
        }

        public IDictionary<string, object> ServerProperties
        {
            get
            {
                return m_delegate.ServerProperties;
            }
        }

        public AmqpTcpEndpoint[] KnownHosts
        {
            get
            {
                return m_delegate.KnownHosts;
            }
            set
            {
                m_delegate.KnownHosts = value;
            }
        }

        public ShutdownEventArgs CloseReason
        {
            get
            {
                return m_delegate.CloseReason;
            }
        }

        public bool IsOpen
        {
            get
            {
                return m_delegate.IsOpen;
            }
        }

        public bool AutoClose
        {
            get
            {
                return m_delegate.AutoClose;
            }
            set
            {
                m_delegate.AutoClose = value;
            }
        }

        public IModel CreateModel()
        {
            AutorecoveringModel m;
            lock(this)
            {
                m = new AutorecoveringModel(this,
                                            (RecoveryAwareModel)this.CreateNonRecoveringModel());
                m_models.Add(m);
            }
            return m;
        }

        public void UnregisterModel(AutorecoveringModel model)
        {
            lock(this)
            {
                m_models.Remove(model);
            }
        }

        public RecoveryAwareModel CreateNonRecoveringModel()
        {
            var session = m_delegate.CreateSession();
            var result  = new RecoveryAwareModel(session);
            result._Private_ChannelOpen("");
            return result;
        }

        public IList<ShutdownReportEntry> ShutdownReport
        {
            get
            {
                return m_delegate.ShutdownReport;
            }
        }

        void IDisposable.Dispose()
        {
            Abort();
            if (ShutdownReport.Count > 0)
            {
                foreach (ShutdownReportEntry entry in ShutdownReport)
                {
                    if (entry.Exception != null)
                        throw entry.Exception;
                }
                throw new OperationInterruptedException(null);
            }
        }

        ///<summary>API-side invocation of connection.close.</summary>
        public void Close()
        {
            m_delegate.Close();
        }

        ///<summary>API-side invocation of connection.close.</summary>
        public void Close(ushort reasonCode, string reasonText)
        {
            m_delegate.Close(reasonCode, reasonText);
        }

        ///<summary>API-side invocation of connection.close with timeout.</summary>
        public void Close(int timeout)
        {
            m_delegate.Close(timeout);
        }

        ///<summary>API-side invocation of connection.close with timeout.</summary>
        public void Close(ushort reasonCode, string reasonText, int timeout)
        {
            m_delegate.Close(reasonCode, reasonText, timeout);
        }

        public void Close(ShutdownEventArgs reason)
        {
            m_delegate.Close(reason);
        }

        ///<summary>API-side invocation of connection abort.</summary>
        public void Abort()
        {
            m_delegate.Abort();
        }

        ///<summary>API-side invocation of connection abort.</summary>
        public void Abort(ushort reasonCode, string reasonText)
        {
            m_delegate.Abort(reasonCode, reasonText);
        }

        ///<summary>API-side invocation of connection abort with timeout.</summary>
        public void Abort(int timeout)
        {
            m_delegate.Abort(timeout);
        }

        ///<summary>API-side invocation of connection abort with timeout.</summary>
        public void Abort(ushort reasonCode, string reasonText, int timeout)
        {
            m_delegate.Abort(reasonCode, reasonText, timeout);
        }

        public override string ToString()
        {
            return string.Format("AutorecoveringConnection({0},{1},{2})", m_delegate.m_id, Endpoint, GetHashCode());
        }

        public void BeginAutomaticRecovery()
        {
            Thread.Sleep(m_factory.NetworkRecoveryInterval);
            lock(this)
            {
                this.RecoverConnectionDelegate();
                this.RecoverConnectionShutdownHandlers();
                this.RecoverConnectionBlockedHandlers();
                this.RecoverConnectionUnblockedHandlers();

                this.RecoverModels();
                if(m_factory.TopologyRecoveryEnabled)
                {
                    this.RecoverEntities();
                    this.RecoverConsumers();
                }

                this.RunRecoveryEventHandlers();
            }
        }

        protected void RecoverConnectionDelegate()
        {
            this.m_delegate = new Connection(m_factory, false, m_factory.CreateFrameHandler());
        }

        protected void RecoverConnectionShutdownHandlers()
        {
            foreach(var eh in this.m_recordedShutdownEventHandlers)
            {
                this.m_delegate.ConnectionShutdown += eh;
            }
        }

        protected void RecoverConnectionBlockedHandlers()
        {
            var handler = this.m_recordedBlockedEventHandlers;
            if(handler != null)
            {
                foreach(var eh in handler)
                {
                    this.m_delegate.ConnectionBlocked += eh;
                }
            }
        }

        protected void RecoverConnectionUnblockedHandlers()
        {
            var handler = this.m_recordedUnblockedEventHandlers;
            if(handler != null)
            {
                foreach(var eh in handler)
                {
                    this.m_delegate.ConnectionUnblocked += eh;
                }
            }
        }

        protected void RunRecoveryEventHandlers()
        {
            var handler = m_recovery;
            if(handler != null)
            {
                foreach(RecoveryEventHandler reh in handler.GetInvocationList())
                {
                    try
                    {
                        reh(this);
                    } catch (Exception e)
                    {
                        var args = new CallbackExceptionEventArgs(e);
                        args.Detail["context"] = "OnConnectionRecovery";
                        this.m_delegate.OnCallbackException(args);
                    }
                }
            }
        }

        protected void RecoverModels()
        {
            lock(this.m_models)
            {
                foreach(var m in this.m_models)
                {
                    m.AutomaticallyRecover(this, this.m_delegate);
                }
            }
        }

        protected void RecoverEntities()
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

        protected void RecoverExchanges()
        {
            foreach(var rx in this.m_recordedExchanges.Values)
            {
                try
                {
                    rx.Recover();
                } catch (Exception cause)
                {
                    var s = String.Format("Caught an exception while recovering exchange {0}: {1}",
                                          rx.Name, cause.Message);
                    this.HandleTopologyRecoveryException(new TopologyRecoveryException(s, cause));
                }
            }
        }

        protected void RecoverQueues()
        {
            lock(this.m_recordedQueues) {
                var rqs = new Dictionary<string, RecordedQueue>(m_recordedQueues);
                foreach(var pair in rqs)
                {
                    var oldName = pair.Key;
                    var rq      = pair.Value;

                    try
                    {
                        rq.Recover();
                        var newName = rq.Name;

                        // make sure server-named queues are re-added with
                        // their new names. MK.

                        this.DeleteRecordedQueue(oldName);
                        this.RecordQueue(newName, rq);
                        this.PropagateQueueNameChangeToBindings(oldName, newName);
                        this.PropagateQueueNameChangeToConsumers(oldName, newName);

                        if(this.m_queueNameChange != null)
                        {
                            foreach(QueueNameChangeAfterRecoveryEventHandler h in this.m_queueNameChange.GetInvocationList())
                            {
                                try
                                {
                                    h(oldName, newName);
                                } catch (Exception e)
                                {
                                    CallbackExceptionEventArgs args = new CallbackExceptionEventArgs(e);
                                    args.Detail["context"] = "OnQueueRecovery";
                                    m_delegate.OnCallbackException(args);
                                }
                            }
                        }
                    } catch (Exception cause)
                    {
                        var s = String.Format("Caught an exception while recovering queue {0}: {1}",
                                              oldName, cause.Message);
                        this.HandleTopologyRecoveryException(new TopologyRecoveryException(s, cause));
                    }
                }
            }
        }

        protected void PropagateQueueNameChangeToBindings(string oldName, string newName)
        {
            lock(this.m_recordedBindings)
            {
                var bs = this.m_recordedBindings.
                    Where(b => b.Destination.Equals(oldName));
                foreach(var b in bs)
                {
                    b.Destination = newName;
                }
            }
        }

        protected void PropagateQueueNameChangeToConsumers(string oldName, string newName)
        {
            lock(this.m_recordedBindings)
            {
                var cs = this.m_recordedConsumers.
                    Where(pair => pair.Value.Queue.Equals(oldName));
                foreach(var c in cs)
                {
                    c.Value.Queue = newName;
                }
            }
        }

        protected void RecoverBindings()
        {
            foreach(var b in this.m_recordedBindings)
            {
                try
                {
                    b.Recover();
                } catch (Exception cause)
                {
                    var s = String.Format("Caught an exception while recovering binding between {0} and {1}: {2}",
                                          b.Source, b.Destination, cause.Message);
                    this.HandleTopologyRecoveryException(new TopologyRecoveryException(s, cause));
                }
            }
        }

        protected void RecoverConsumers()
        {
            var dict = new Dictionary<string, RecordedConsumer>(m_recordedConsumers);
            foreach(var pair in dict)
            {
                var tag  = pair.Key;
                var cons = pair.Value;

                try
                {
                    var newTag = cons.Recover();
                    lock(this.m_recordedConsumers)
                    {
                        // make sure server-generated tags are re-added
                        this.m_recordedConsumers.Remove(tag);
                        this.m_recordedConsumers.Add(newTag, cons);
                    }

                    if(this.m_consumerTagChange != null)
                    {
                        foreach(ConsumerTagChangeAfterRecoveryEventHandler h in this.m_consumerTagChange.GetInvocationList())
                        {
                            try
                            {
                                h(tag, newTag);
                            } catch (Exception e)
                            {
                                CallbackExceptionEventArgs args = new CallbackExceptionEventArgs(e);
                                args.Detail["context"] = "OnConsumerRecovery";
                                m_delegate.OnCallbackException(args);
                            }
                        }
                    }
                } catch (Exception cause)
                {
                    var s = String.Format("Caught an exception while recovering consumer {0} on queue {1}: {2}",
                                          tag, cons.Queue, cause.Message);
                    this.HandleTopologyRecoveryException(new TopologyRecoveryException(s, cause));
                }
            }
        }


        protected void HandleTopologyRecoveryException(TopologyRecoveryException e)
        {
            // TODO
            Console.WriteLine("Topology recovery exception: {0}", e);
        }

        public void RecordExchange(string name, RecordedExchange x)
        {
            lock(this.m_recordedEntitiesLock)
            {
                m_recordedExchanges[name] = x;
            }
        }

        public void DeleteRecordedExchange(string name)
        {
            lock(this.m_recordedEntitiesLock)
            {
                m_recordedExchanges.Remove(name);

                // find bindings that need removal, check if some auto-delete exchanges
                // might need the same
                var bs = m_recordedBindings.Where(b => name.Equals(b.Destination)).
                    ToList();
                m_recordedBindings.RemoveWhere(b => name.Equals(b.Destination));
                foreach(var b in bs)
                {
                    MaybeDeleteRecordedAutoDeleteExchange(b.Source);
                }
            }
        }

        public void RecordQueue(string name, RecordedQueue q)
        {
            lock(this.m_recordedEntitiesLock)
            {
                m_recordedQueues[name] = q;
            }
        }

        public void DeleteRecordedQueue(string name)
        {
            lock(this.m_recordedEntitiesLock)
            {
                m_recordedQueues.Remove(name);
                // find bindings that need removal, check if some auto-delete exchanges
                // might need the same
                var bs = m_recordedBindings.Where(b => name.Equals(b.Destination)).
                    ToList();
                m_recordedBindings.RemoveWhere(b => name.Equals(b.Destination));
                foreach(var b in bs)
                {
                    MaybeDeleteRecordedAutoDeleteExchange(b.Source);
                }
            }
        }

        public void RecordBinding(RecordedBinding rb)
        {
            lock(this.m_recordedEntitiesLock)
            {
                m_recordedBindings.Add(rb);
            }
        }

        public void DeleteRecordedBinding(RecordedBinding rb)
        {
            lock(this.m_recordedEntitiesLock)
            {
                m_recordedBindings.RemoveWhere(b => b.Equals(rb));
            }
        }

       public void RecordConsumer(string name, RecordedConsumer c)
        {
            lock(this.m_recordedEntitiesLock)
            {
                if(!m_recordedConsumers.ContainsKey(name))
                {
                    m_recordedConsumers.Add(name, c);
                }
            }
        }

        public RecordedConsumer DeleteRecordedConsumer(string consumerTag)
        {
            RecordedConsumer rc = null;
            lock(this.m_recordedEntitiesLock)
            {
                if(m_recordedConsumers.ContainsKey(consumerTag))
                {
                    rc = m_recordedConsumers[consumerTag];
                    m_recordedConsumers.Remove(consumerTag);

                }
            }

            return rc;
        }

        public void MaybeDeleteRecordedAutoDeleteQueue(string queue)
        {
            lock(this.m_recordedEntitiesLock)
            {
                if(!HasMoreConsumersOnQueue(this.m_recordedConsumers.Values, queue))
                {
                    RecordedQueue rq;
                    this.m_recordedQueues.TryGetValue(queue, out rq);
                    // last consumer on this connection is gone, remove recorded queue
                    // if it is auto-deleted. See bug 26364.
                    if((rq != null) && rq.IsAutoDelete)
                    {
                        this.m_recordedQueues.Remove(queue);
                    }
                }
            }
        }

        public void MaybeDeleteRecordedAutoDeleteExchange(string exchange)
        {
            lock(this.m_recordedEntitiesLock)
            {
                if(!HasMoreDestinationsBoundToExchange(this.m_recordedBindings, exchange))
                {
                    RecordedExchange rx;
                    this.m_recordedExchanges.TryGetValue(exchange, out rx);
                    // last binding where this exchange is the source is gone,
                    // remove recorded exchange
                    // if it is auto-deleted. See bug 26364.
                    if((rx != null) && rx.IsAutoDelete)
                    {
                        this.m_recordedExchanges.Remove(exchange);
                    }
                }
            }
        }

        public bool HasMoreConsumersOnQueue(ICollection<RecordedConsumer> consumers,
                                            string queue)
        {
            var cs = new List<RecordedConsumer>(consumers);
            return cs.Exists(c => c.Queue.Equals(queue));
        }

        public bool HasMoreDestinationsBoundToExchange(ICollection<RecordedBinding> bindings,
                                                       string exchange)
        {
            var bs = new List<RecordedBinding>(bindings);
            return bs.Exists(b => b.Source.Equals(exchange));
        }

        public IDictionary<string, RecordedQueue> RecordedQueues
        {
            get { return m_recordedQueues; }
        }

        public IDictionary<string, RecordedExchange> RecordedExchanges
        {
            get { return m_recordedExchanges; }
        }
    }
}