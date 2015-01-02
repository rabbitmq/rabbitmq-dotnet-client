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
using System.Linq;
using System.Net;
using System.Threading;
using System.Timers;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Impl;
using Timer = System.Timers.Timer;

namespace RabbitMQ.Client.Framing.Impl
{
    public class AutorecoveringConnection : IConnection, IRecoverable
    {
        public readonly object m_eventLock = new object();
        public readonly object m_recordedEntitiesLock = new object();
        protected Connection m_delegate;
        protected ConnectionFactory m_factory;

        protected List<AutorecoveringModel> m_models = new List<AutorecoveringModel>();

        protected HashSet<RecordedBinding> m_recordedBindings = new HashSet<RecordedBinding>();

        protected List<EventHandler<ConnectionBlockedEventArgs>> m_recordedBlockedEventHandlers =
            new List<EventHandler<ConnectionBlockedEventArgs>>();

        protected IDictionary<string, RecordedConsumer> m_recordedConsumers =
            new Dictionary<string, RecordedConsumer>();

        protected IDictionary<string, RecordedExchange> m_recordedExchanges =
            new Dictionary<string, RecordedExchange>();

        protected IDictionary<string, RecordedQueue> m_recordedQueues =
            new Dictionary<string, RecordedQueue>();

        protected List<EventHandler<ShutdownEventArgs>> m_recordedShutdownEventHandlers =
            new List<EventHandler<ShutdownEventArgs>>();

        protected List<EventHandler<EventArgs>> m_recordedUnblockedEventHandlers =
            new List<EventHandler<EventArgs>>();

        private EventHandler<ConsumerTagChangedAfterRecoveryEventArgs> m_consumerTagChange;
        private EventHandler<QueueNameChangedAfterRecoveryEventArgs> m_queueNameChange;
        private EventHandler<EventArgs> m_recovery;

        public AutorecoveringConnection(ConnectionFactory factory)
        {
            m_factory = factory;
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
                    m_recordedBlockedEventHandlers.Add(value);
                    m_delegate.ConnectionBlocked += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_recordedBlockedEventHandlers.Remove(value);
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
                    m_recordedShutdownEventHandlers.Add(value);
                    m_delegate.ConnectionShutdown += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_recordedShutdownEventHandlers.Remove(value);
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
                    m_recordedUnblockedEventHandlers.Add(value);
                    m_delegate.ConnectionUnblocked += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_recordedUnblockedEventHandlers.Remove(value);
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

        public event EventHandler<EventArgs> Recovery
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

        public bool AutoClose
        {
            get { return m_delegate.AutoClose; }
            set { m_delegate.AutoClose = value; }
        }

        public ushort ChannelMax
        {
            get { return m_delegate.ChannelMax; }
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

        public EndPoint LocalEndPoint
        {
            get { return m_delegate.LocalEndPoint; }
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

        public EndPoint RemoteEndPoint
        {
            get { return m_delegate.RemoteEndPoint; }
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

        public void BeginAutomaticRecovery()
        {
            var t = new Timer(m_factory.NetworkRecoveryInterval.TotalSeconds * 1000);
            t.Elapsed += PerformAutomaticRecovery;

            t.AutoReset = false;
            t.Enabled = true;
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
                m_recordedBindings.RemoveWhere(b => b.Equals(rb));
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
                List<RecordedBinding> bs = m_recordedBindings.Where(b => name.Equals(b.Destination)).
                                                              ToList();
                m_recordedBindings.RemoveWhere(b => name.Equals(b.Destination));
                foreach (RecordedBinding b in bs)
                {
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
                List<RecordedBinding> bs = m_recordedBindings.Where(b => name.Equals(b.Destination)).
                                                              ToList();
                m_recordedBindings.RemoveWhere(b => name.Equals(b.Destination));
                foreach (RecordedBinding b in bs)
                {
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
                if (!HasMoreDestinationsBoundToExchange(m_recordedBindings, exchange))
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
                m_recordedBindings.Add(rb);
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
            return string.Format("AutorecoveringConnection({0},{1},{2})", m_delegate.m_id, Endpoint, GetHashCode());
        }

        public void UnregisterModel(AutorecoveringModel model)
        {
            lock (this)
            {
                m_models.Remove(model);
            }
        }

        public void init()
        {
            m_delegate = new Connection(m_factory, false, m_factory.CreateFrameHandler());

            AutorecoveringConnection self = this;
            EventHandler<ShutdownEventArgs> recoveryListener = (_, args) =>
            {
                lock (self)
                {
                    if (ShouldTriggerConnectionRecovery(args))
                    {
                        try
                        {
                            self.BeginAutomaticRecovery();
                        }
                        catch (Exception e)
                        {
                            // TODO: logging
                            Console.WriteLine("BeginAutomaticRecovery() failed: {0}", e);
                        }
                    }
                }
            };
            lock (m_eventLock)
            {
                ConnectionShutdown += recoveryListener;
                if (!m_recordedShutdownEventHandlers.Contains(recoveryListener))
                {
                    m_recordedShutdownEventHandlers.Add(recoveryListener);
                }
            }
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

        public IModel CreateModel()
        {
            EnsureIsOpen();
            AutorecoveringModel m;
            m = new AutorecoveringModel(this,
                CreateNonRecoveringModel());
            lock (this)
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

        protected void EnsureIsOpen()
        {
            m_delegate.EnsureIsOpen();
        }

        protected void HandleTopologyRecoveryException(TopologyRecoveryException e)
        {
            // TODO
            Console.WriteLine("Topology recovery exception: {0}", e);
        }

        protected void PerformAutomaticRecovery(object self, ElapsedEventArgs _e)
        {
            lock (self)
            {
                RecoverConnectionDelegate();
                RecoverConnectionShutdownHandlers();
                RecoverConnectionBlockedHandlers();
                RecoverConnectionUnblockedHandlers();

                RecoverModels();
                if (m_factory.TopologyRecoveryEnabled)
                {
                    RecoverEntities();
                    RecoverConsumers();
                }

                RunRecoveryEventHandlers();
            }
        }

        protected void PropagateQueueNameChangeToBindings(string oldName, string newName)
        {
            lock (m_recordedBindings)
            {
                IEnumerable<RecordedBinding> bs = m_recordedBindings.
                    Where(b => b.Destination.Equals(oldName));
                foreach (RecordedBinding b in bs)
                {
                    b.Destination = newName;
                }
            }
        }

        protected void PropagateQueueNameChangeToConsumers(string oldName, string newName)
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

        protected void RecoverBindings()
        {
            foreach (RecordedBinding b in m_recordedBindings)
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

        protected void RecoverConnectionBlockedHandlers()
        {
            List<EventHandler<ConnectionBlockedEventArgs>> handler = m_recordedBlockedEventHandlers;
            if (handler != null)
            {
                foreach (EventHandler<ConnectionBlockedEventArgs> eh in handler)
                {
                    m_delegate.ConnectionBlocked += eh;
                }
            }
        }

        protected void RecoverConnectionDelegate()
        {
            bool recovering = true;
            while (recovering)
            {
                try
                {
                    m_delegate = new Connection(m_factory, false, m_factory.CreateFrameHandler());
                    recovering = false;
                }
                catch (Exception e)
                {
                    // TODO: exponential back-off
                    Thread.Sleep(m_factory.NetworkRecoveryInterval);
                    // TODO: provide a way to handle these exceptions
                }
            }
        }

        protected void RecoverConnectionShutdownHandlers()
        {
            foreach (EventHandler<ShutdownEventArgs> eh in m_recordedShutdownEventHandlers)
            {
                m_delegate.ConnectionShutdown += eh;
            }
        }

        protected void RecoverConnectionUnblockedHandlers()
        {
            List<EventHandler<EventArgs>> handler = m_recordedUnblockedEventHandlers;
            if (handler != null)
            {
                foreach (EventHandler<EventArgs> eh in handler)
                {
                    m_delegate.ConnectionUnblocked += eh;
                }
            }
        }

        protected void RecoverConsumers()
        {
            var dict = new Dictionary<string, RecordedConsumer>(m_recordedConsumers);
            foreach (KeyValuePair<string, RecordedConsumer> pair in dict)
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

        protected void RecoverModels()
        {
            lock (m_models)
            {
                foreach (AutorecoveringModel m in m_models)
                {
                    m.AutomaticallyRecover(this, m_delegate);
                }
            }
        }

        protected void RecoverQueues()
        {
            lock (m_recordedQueues)
            {
                var rqs = new Dictionary<string, RecordedQueue>(m_recordedQueues);
                foreach (KeyValuePair<string, RecordedQueue> pair in rqs)
                {
                    string oldName = pair.Key;
                    RecordedQueue rq = pair.Value;

                    try
                    {
                        rq.Recover();
                        string newName = rq.Name;

                        // make sure server-named queues are re-added with
                        // their new names. MK.

                        DeleteRecordedQueue(oldName);
                        RecordQueue(newName, rq);
                        PropagateQueueNameChangeToBindings(oldName, newName);
                        PropagateQueueNameChangeToConsumers(oldName, newName);

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

        protected void RunRecoveryEventHandlers()
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

        protected bool ShouldTriggerConnectionRecovery(ShutdownEventArgs args)
        {
            return (args.Initiator == ShutdownInitiator.Peer ||
                    // happens when EOF is reached, e.g. due to RabbitMQ node
                    // connectivity loss or abrupt shutdown
                    args.Initiator == ShutdownInitiator.Library);
        }
    }
}
