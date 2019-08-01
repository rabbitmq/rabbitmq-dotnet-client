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
//  at http://www.mozilla.org/MPL/
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
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Impl;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Framing.Impl
{
    public class AutorecoveringConnection : IConnection, IRecoverable
    {
        protected Connection m_delegate;
        protected ConnectionFactory m_factory;

        // list of endpoints provided on initial connection.
        // on re-connection, the next host in the line is chosen using
        // IHostnameSelector
        private IEndpointResolver endpoints;

        protected List<AutorecoveringModel> m_models = new List<AutorecoveringModel>();

        // Notes on ConcurrentDictionary:
        //   From MSDN: "All public and protected members of ConcurrentDictionary<TKey,TValue> are thread-safe
        //   and may be used concurrently from multiple threads. However, members accessed through one of the
        //   interfaces the ConcurrentDictionary<TKey,TValue> implements, including extension methods, are not
        //   guaranteed to be thread safe and may need to be synchronized by the caller."
        // Take-away: When interacting with ConcurrentDictionary make sure to use its members and be wary of
        //   extension methods or casting it to an interface like IDictionary or ICollection.

        protected ConcurrentDictionary<RecordedBinding, byte> m_recordedBindings =
            new ConcurrentDictionary<RecordedBinding, byte>();

        protected ConcurrentDictionary<string, RecordedConsumer> m_recordedConsumers =
            new ConcurrentDictionary<string, RecordedConsumer>();

        protected ConcurrentDictionary<string, RecordedExchange> m_recordedExchanges =
            new ConcurrentDictionary<string, RecordedExchange>();

        protected ConcurrentDictionary<string, RecordedQueue> m_recordedQueues =
            new ConcurrentDictionary<string, RecordedQueue>();

        //private EventHandler<ConsumerTagChangedAfterRecoveryEventArgs> m_consumerTagChange;
        //private EventHandler<QueueNameChangedAfterRecoveryEventArgs> m_queueNameChange;
        //private EventHandler<EventArgs> m_recovery;
        //private EventHandler<ConnectionRecoveryErrorEventArgs> m_connectionRecoveryError;

        private Thread m_recoveryThread;

        public AutorecoveringConnection(ConnectionFactory factory, string clientProvidedName = null)
        {
            m_factory = factory;
            this.ClientProvidedName = clientProvidedName;
        }

        public event EventHandler<EventArgs> RecoverySucceeded;

        public event EventHandler<ConnectionRecoveryErrorEventArgs> ConnectionRecoveryError;

        public event EventHandler<CallbackExceptionEventArgs> CallbackException;

        public event EventHandler<ConnectionBlockedEventArgs> ConnectionBlocked;

        public event EventHandler<ShutdownEventArgs> ConnectionShutdown;

        public event EventHandler<EventArgs> ConnectionUnblocked;

        public event EventHandler<ConsumerTagChangedAfterRecoveryEventArgs> ConsumerTagChangeAfterRecovery;

        public event EventHandler<QueueNameChangedAfterRecoveryEventArgs> QueueNameChangeAfterRecovery;

        [Obsolete("Use RecoverySucceeded instead")]
        public event EventHandler<EventArgs> Recovery
        {
            add => RecoverySucceeded += value;
            remove => RecoverySucceeded -= value;
        }

        public string ClientProvidedName { get; private set; }

        [Obsolete("Please explicitly close connections instead.")]
        public bool AutoClose
        {
            get => m_delegate.AutoClose;
            set => m_delegate.AutoClose = value;
        }

        public ushort ChannelMax => m_delegate.ChannelMax;

        public ConsumerWorkService ConsumerWorkService => m_delegate.ConsumerWorkService;

        public IDictionary<string, object> ClientProperties => m_delegate.ClientProperties;

        public ShutdownEventArgs CloseReason => m_delegate.CloseReason;

        public AmqpTcpEndpoint Endpoint => m_delegate.Endpoint;

        public uint FrameMax => m_delegate.FrameMax;

        public ushort Heartbeat => m_delegate.Heartbeat;

        public bool IsOpen => m_delegate.IsOpen;

        public AmqpTcpEndpoint[] KnownHosts
        {
            get => m_delegate.KnownHosts;
            set => m_delegate.KnownHosts = value;
        }

        public int LocalPort => m_delegate.LocalPort;

        public ProtocolBase Protocol => m_delegate.Protocol;

        public IDictionary<string, RecordedExchange> RecordedExchanges => m_recordedExchanges;

        public IDictionary<string, RecordedQueue> RecordedQueues => m_recordedQueues;

        public int RemotePort => m_delegate.RemotePort;

        public IDictionary<string, object> ServerProperties => m_delegate.ServerProperties;

        public IList<ShutdownReportEntry> ShutdownReport => m_delegate.ShutdownReport;

        IProtocol IConnection.Protocol => Endpoint.Protocol;


        private enum RecoveryCommand
        {
            RecoverConnection
        }


        private enum RecoveryConnectionState
        {
            Connected,
            Recovering
        }


        private BlockingCollection<RecoveryCommand> m_recoveryLoopCommandQueue = new BlockingCollection<RecoveryCommand>();
        private RecoveryConnectionState m_recoveryLoopState = RecoveryConnectionState.Connected;
        private CancellationTokenSource m_recoveryCancellationToken = new CancellationTokenSource();
        private TaskCompletionSource<int> m_recoveryLoopComplete = new TaskCompletionSource<int>();

        private void MainRecoveryLoop()
        {
            while (m_recoveryLoopCommandQueue.TryTake(out var command, 0, m_recoveryCancellationToken.Token))
            {
                switch (m_recoveryLoopState)
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

            m_recoveryLoopComplete.SetResult(0);
        }

        private void StopRecoveryLoop()
        {
            m_recoveryCancellationToken.Cancel();
            if (!m_recoveryLoopComplete.Task.Wait(m_factory.RequestedConnectionTimeout))
            {
                ESLog.Warn("Timeout while trying to stop background AutorecoveringConnection recovery loop.");
            }
        }

        private void RecoveryLoopRecoveringHandler(RecoveryCommand command)
        {
            switch (command)
            {
                case RecoveryCommand.RecoverConnection:
                    if (TryRecoverConnection())
                    {
                        m_recoveryLoopState = RecoveryConnectionState.Connected;
                    }
                    else
                    {
                        Task.Delay(m_factory.NetworkRecoveryInterval).ContinueWith(t => { m_recoveryLoopCommandQueue.TryAdd(RecoveryCommand.RecoverConnection); });
                    }

                    break;
                default:
                    ESLog.Warn($"RecoveryLoop command {command} is out of range.");
                    break;
            }
        }

        private void RecoveryLoopConnectedHandler(RecoveryCommand command)
        {
            switch (command)
            {
                case RecoveryCommand.RecoverConnection:
                    m_recoveryLoopState = RecoveryConnectionState.Recovering;
                    Task.Delay(m_factory.NetworkRecoveryInterval).ContinueWith(t => { m_recoveryLoopCommandQueue.TryAdd(RecoveryCommand.RecoverConnection); });
                    break;
                default:
                    ESLog.Warn($"RecoveryLoop command {command} is out of range.");
                    break;
            }
        }

        private bool TryRecoverConnection()
        {
            ESLog.Info("Performing automatic recovery");

            try
            {
                if (TryRecoverConnectionDelegate(out var connection))
                {
                    RegisterForConnectionEvents(connection);

                    RecoverModels();
                    if (m_factory.TopologyRecoveryEnabled)
                    {
                        RecoverEntities();
                        RecoverConsumers();
                    }

                    ESLog.Info("Connection recovery completed");
                    RunRecoveryEventHandlers();

                    m_delegate = connection;
                    return true;
                }
            }
            catch (Exception e)
            {
                ESLog.Error("Exception when recovering connection.", e);
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
            if (!m_recordedBindings.TryRemove(rb, out var value))
            {
                ESLog.Warn($"Failed to remove RecordedBinding: {rb}");
            }
        }

        public RecordedConsumer DeleteRecordedConsumer(string consumerTag)
        {
            if (m_recordedConsumers.TryRemove(consumerTag, out var value))
            {
                return value;
            }

            return null;
        }

        public void DeleteRecordedExchange(string name)
        {
            if (m_recordedExchanges.TryRemove(name, out var exchange))
            {
                DeleteBindings(name);
            }
            else
            {
                ESLog.Warn($"Failed to remove Exchange: {name}");
            }
        }

        public void DeleteRecordedQueue(string name)
        {
            if (m_recordedQueues.TryRemove(name, out var exchange))
            {
                DeleteBindings(name);
            }
            else
            {
                ESLog.Warn($"Failed to remove Queue: {name}");
            }
        }

        public void DeleteBindings(string name)
        {
            var bindings = m_recordedBindings.Select(p => p.Key);

            // find bindings that need removal, check if some auto-delete exchanges
            // might need the same
            foreach (var b in bindings.Where(b => name.Equals(b.Destination)))
            {
                DeleteRecordedBinding(b);
                MaybeDeleteRecordedAutoDeleteExchange(b.Source);
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
            if (!HasMoreDestinationsBoundToExchange(m_recordedBindings.Keys, exchange))
            {
                if (m_recordedExchanges.TryGetValue(exchange, out var rx))
                {
                    // last binding where this exchange is the source is gone,
                    // remove recorded exchange
                    // if it is auto-deleted. See bug 26364.
                    if ((rx != null) && rx.IsAutoDelete)
                    {
                        m_recordedExchanges.TryRemove(exchange, out var ex);
                    }
                }
            }
        }

        public void MaybeDeleteRecordedAutoDeleteQueue(string queue)
        {
            if (!HasMoreConsumersOnQueue(m_recordedConsumers.Values, queue))
            {
                if (m_recordedQueues.TryGetValue(queue, out var rq))
                {
                    // last consumer on this connection is gone, remove recorded queue
                    // if it is auto-deleted. See bug 26364.
                    if ((rq != null) && rq.IsAutoDelete)
                    {
                        m_recordedQueues.TryRemove(queue, out var q);
                    }
                }
            }
        }

        public void RecordBinding(RecordedBinding rb)
        {
            m_recordedBindings.TryAdd(rb, 0);
        }

        public void RecordConsumer(string name, RecordedConsumer c)
        {
            m_recordedConsumers.TryAdd(name, c);
        }

        public void RecordExchange(string name, RecordedExchange x)
        {
            m_recordedExchanges.TryAdd(name, x);
        }

        public void RecordQueue(string name, RecordedQueue q)
        {
            m_recordedQueues.TryAdd(name, q);
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
            m_delegate = new Connection(m_factory,
                false,
                fh,
                this.ClientProvidedName);

            m_recoveryThread = new Thread(MainRecoveryLoop);
            m_recoveryThread.Start();

            EventHandler<ShutdownEventArgs> recoveryListener = (_, args) =>
            {
                var condition = m_factory.ConnectionRecoveryTriggeringCondition ?? ShouldTriggerConnectionRecovery;

                if (condition(args))
                {
                    if (!m_recoveryLoopCommandQueue.TryAdd(RecoveryCommand.RecoverConnection))
                    {
                        ESLog.Warn("Failed to notify RecoveryLoop to RecoverConnection.");
                    }
                }
            };
            ConnectionShutdown += recoveryListener;

            RegisterForConnectionEvents(m_delegate);
        }

        private void RegisterForConnectionEvents(Connection connection)
        {
            connection.ConnectionShutdown += OnConnectionShutdown;
            connection.CallbackException += OnCallbackException;
            connection.ConnectionBlocked += OnConnectionBlocked;
            connection.ConnectionUnblocked += OnConnectionUnblocked;
        }

        private void OnConnectionUnblocked(object sender, EventArgs e)
        {
            ConnectionUnblocked?.Invoke(sender, e);
        }

        private void OnConnectionBlocked(object sender, ConnectionBlockedEventArgs e)
        {
            ConnectionBlocked?.Invoke(sender, e);
        }

        private void OnCallbackException(object sender, CallbackExceptionEventArgs e)
        {
            CallbackException?.Invoke(sender, e);
        }

        private void OnConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            ConnectionShutdown?.Invoke(sender, e);
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
        public void Abort(ushort reasonCode, string reasonText, int timeout)
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
            try
            {
                Abort((int)m_factory.HandshakeContinuationTimeout.TotalMilliseconds);
            }
            catch (Exception e)
            {
                ESLog.Error("Unable to abort Connection on Dispose.", e);
            }
            finally
            {
                m_models.Clear();
            }
        }

        protected void EnsureIsOpen()
        {
            m_delegate.EnsureIsOpen();
        }

        protected void HandleTopologyRecoveryException(TopologyRecoveryException e)
        {
            ESLog.Error("Topology recovery exception", e);
        }

        protected void PropagateQueueNameChangeToBindings(string oldName, string newName)
        {
            var bs = m_recordedBindings.Keys.Where(b => b.Destination.Equals(oldName));

            foreach (RecordedBinding b in bs)
            {
                b.Destination = newName;
            }
        }

        protected void PropagateQueueNameChangeToConsumers(string oldName, string newName)
        {
            var cs = m_recordedConsumers.Values.Where(c => c.Queue.Equals(oldName));

            foreach (var c in cs)
            {
                c.Queue = newName;
            }
        }

        protected void RecoverBindings()
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
                        b.Source,
                        b.Destination,
                        cause.Message);
                    HandleTopologyRecoveryException(new TopologyRecoveryException(s, cause));
                }
            }
        }

        protected bool TryRecoverConnectionDelegate(out Connection connection)
        {
            try
            {
                var fh = endpoints.SelectOne(m_factory.CreateFrameHandler);
                connection = new Connection(m_factory, false, fh, this.ClientProvidedName);

                return true;
            }
            catch (Exception e)
            {
                ESLog.Error("Connection recovery exception.", e);
                // Trigger recovery error events
                var args = new ConnectionRecoveryErrorEventArgs(e);

                foreach (EventHandler<ConnectionRecoveryErrorEventArgs> h in ConnectionRecoveryError?.GetInvocationList() ?? new EventHandler<ConnectionRecoveryErrorEventArgs>[] { })
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

            connection = null;
            return false;
        }

        protected void RecoverConsumers()
        {
            foreach (KeyValuePair<string, RecordedConsumer> pair in m_recordedConsumers.ToArray())
            {
                string tag = pair.Key;
                RecordedConsumer cons = pair.Value;

                try
                {
                    string newTag = cons.Recover();

                    // make sure server-generated tags are re-added
                    m_recordedConsumers.TryRemove(tag, out var old);
                    m_recordedConsumers.TryAdd(newTag, cons);

                    foreach (EventHandler<ConsumerTagChangedAfterRecoveryEventArgs> h in ConsumerTagChangeAfterRecovery?.GetInvocationList() ?? new EventHandler<ConsumerTagChangedAfterRecoveryEventArgs>[] { })
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
                catch (Exception cause)
                {
                    string s = String.Format("Caught an exception while recovering consumer {0} on queue {1}: {2}",
                        tag,
                        cons.Queue,
                        cause.Message);
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
                        rx.Name,
                        cause.Message);
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
                foreach (KeyValuePair<string, RecordedQueue> pair in m_recordedQueues.ToArray())
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

                        foreach (EventHandler<QueueNameChangedAfterRecoveryEventArgs> h in QueueNameChangeAfterRecovery?.GetInvocationList() ?? new EventHandler<QueueNameChangedAfterRecoveryEventArgs>[] { })
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
                    catch (Exception cause)
                    {
                        string s = String.Format("Caught an exception while recovering queue {0}: {1}",
                            oldName,
                            cause.Message);
                        HandleTopologyRecoveryException(new TopologyRecoveryException(s, cause));
                    }
                }
            }
        }

        protected void RunRecoveryEventHandlers()
        {
            foreach (EventHandler<EventArgs> reh in RecoverySucceeded?.GetInvocationList() ?? new EventHandler<EventArgs>[] { })
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

        protected bool ShouldTriggerConnectionRecovery(ShutdownEventArgs args)
        {
            return (args.Initiator == ShutdownInitiator.Peer ||
                    // happens when EOF is reached, e.g. due to RabbitMQ node
                    // connectivity loss or abrupt shutdown
                    args.Initiator == ShutdownInitiator.Library);
        }
    }
}
