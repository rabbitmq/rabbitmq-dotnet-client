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
using System.Text;
using System.Threading;

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    public abstract class ModelBase : IFullModel, IRecoverable
    {
        public readonly IDictionary<string, IBasicConsumer> m_consumers = new Dictionary<string, IBasicConsumer>();

        ///<summary>Only used to kick-start a connection open
        ///sequence. See <see cref="Connection.Open"/> </summary>
        public BlockingCell<ConnectionStartDetails> m_connectionStartCell = null;

        private TimeSpan _handshakeContinuationTimeout = TimeSpan.FromSeconds(10);
        private TimeSpan _continuationTimeout = TimeSpan.FromSeconds(20);

        private readonly RpcContinuationQueue _continuationQueue = new RpcContinuationQueue();
        private readonly ManualResetEvent _flowControlBlock = new ManualResetEvent(true);

        private readonly object _eventLock = new object();
        private readonly object _shutdownLock = new object();
        private readonly object _rpcLock = new object();

        private readonly SynchronizedList<ulong> _unconfirmedSet = new SynchronizedList<ulong>();

        private EventHandler<ShutdownEventArgs> _modelShutdown;

        private bool _onlyAcksReceived = true;

        public IConsumerDispatcher ConsumerDispatcher { get; private set; }

        public ModelBase(ISession session)
            : this(session, session.Connection.ConsumerWorkService)
        { }

        public ModelBase(ISession session, ConsumerWorkService workService)
        {
            if (workService is AsyncConsumerWorkService asyncConsumerWorkService)
            {
                ConsumerDispatcher = new AsyncConsumerDispatcher(this, asyncConsumerWorkService);
            }
            else
            {
                ConsumerDispatcher = new ConcurrentConsumerDispatcher(this, workService);
            }

            Initialise(session);
        }

        protected void Initialise(ISession session)
        {
            CloseReason = null;
            NextPublishSeqNo = 0;
            Session = session;
            Session.CommandReceived = HandleCommand;
            Session.SessionShutdown += OnSessionShutdown;
        }

        public TimeSpan HandshakeContinuationTimeout
        {
            get { return _handshakeContinuationTimeout; }
            set { _handshakeContinuationTimeout = value; }
        }

        public TimeSpan ContinuationTimeout
        {
            get { return _continuationTimeout; }
            set { _continuationTimeout = value; }
        }

        public event EventHandler<BasicAckEventArgs> BasicAcks;
        public event EventHandler<BasicNackEventArgs> BasicNacks;
        public event EventHandler<EventArgs> BasicRecoverOk;
        public event EventHandler<BasicReturnEventArgs> BasicReturn;
        public event EventHandler<CallbackExceptionEventArgs> CallbackException;
        public event EventHandler<FlowControlEventArgs> FlowControl;
        public event EventHandler<ShutdownEventArgs> ModelShutdown
        {
            add
            {
                bool ok = false;
                if (CloseReason == null)
                {
                    lock (_shutdownLock)
                    {
                        if (CloseReason == null)
                        {
                            _modelShutdown += value;
                            ok = true;
                        }
                    }
                }
                if (!ok)
                {
                    value(this, CloseReason);
                }
            }
            remove
            {
                lock (_shutdownLock)
                {
                    _modelShutdown -= value;
                }
            }
        }

#pragma warning disable 67
        public event EventHandler<EventArgs> Recovery;
#pragma warning restore 67

        public int ChannelNumber
        {
            get { return ((Session)Session).ChannelNumber; }
        }

        public ShutdownEventArgs CloseReason { get; private set; }

        public IBasicConsumer DefaultConsumer { get; set; }

        public bool IsClosed
        {
            get { return !IsOpen; }
        }

        public bool IsOpen
        {
            get { return CloseReason == null; }
        }

        public ulong NextPublishSeqNo { get; private set; }

        public ISession Session { get; private set; }

        public void Close(ushort replyCode, string replyText, bool abort)
        {
            Close(new ShutdownEventArgs(ShutdownInitiator.Application,
                replyCode, replyText),
                abort);
        }

        public void Close(ShutdownEventArgs reason, bool abort)
        {
            var k = new ShutdownContinuation();
            ModelShutdown += k.OnConnectionShutdown;

            try
            {
                ConsumerDispatcher.Quiesce();
                if (SetCloseReason(reason))
                {
                    _Private_ChannelClose(reason.ReplyCode, reason.ReplyText, 0, 0);
                }
                k.Wait(TimeSpan.FromMilliseconds(10000));
                ConsumerDispatcher.Shutdown(this);
            }
            catch (AlreadyClosedException)
            {
                if (!abort)
                {
                    throw;
                }
            }
            catch (IOException)
            {
                if (!abort)
                {
                    throw;
                }
            }
            catch (Exception)
            {
                if (!abort)
                {
                    throw;
                }
            }
        }

        public string ConnectionOpen(string virtualHost,
            string capabilities,
            bool insist)
        {
            var k = new ConnectionOpenContinuation();
            lock (_rpcLock)
            {
                Enqueue(k);
                try
                {
                    _Private_ConnectionOpen(virtualHost, capabilities, insist);
                }
                catch (AlreadyClosedException)
                {
                    // let continuation throw OperationInterruptedException,
                    // which is a much more suitable exception before connection
                    // negotiation finishes
                }
                k.GetReply(HandshakeContinuationTimeout);
            }

            return k.m_knownHosts;
        }

        public ConnectionSecureOrTune ConnectionSecureOk(byte[] response)
        {
            var k = new ConnectionStartRpcContinuation();
            lock (_rpcLock)
            {
                Enqueue(k);
                try
                {
                    _Private_ConnectionSecureOk(response);
                }
                catch (AlreadyClosedException)
                {
                    // let continuation throw OperationInterruptedException,
                    // which is a much more suitable exception before connection
                    // negotiation finishes
                }
                k.GetReply(HandshakeContinuationTimeout);
            }
            return k.m_result;
        }

        public ConnectionSecureOrTune ConnectionStartOk(IDictionary<string, object> clientProperties,
            string mechanism,
            byte[] response,
            string locale)
        {
            var k = new ConnectionStartRpcContinuation();
            lock (_rpcLock)
            {
                Enqueue(k);
                try
                {
                    _Private_ConnectionStartOk(clientProperties, mechanism,
                        response, locale);
                }
                catch (AlreadyClosedException)
                {
                    // let continuation throw OperationInterruptedException,
                    // which is a much more suitable exception before connection
                    // negotiation finishes
                }
                k.GetReply(HandshakeContinuationTimeout);
            }
            return k.m_result;
        }

        public abstract bool DispatchAsynchronous(Command cmd);

        public void Enqueue(IRpcContinuation k)
        {
            bool ok = false;
            if (CloseReason == null)
            {
                lock (_shutdownLock)
                {
                    if (CloseReason == null)
                    {
                        _continuationQueue.Enqueue(k);
                        ok = true;
                    }
                }
            }
            if (!ok)
            {
                k.HandleModelShutdown(CloseReason);
            }
        }

        public void FinishClose()
        {
            if (CloseReason != null)
            {
                Session.Close(CloseReason);
            }
            if (m_connectionStartCell != null)
            {
                m_connectionStartCell.ContinueWithValue(null);
            }
        }

        public void HandleCommand(ISession session, Command cmd)
        {
            if (!DispatchAsynchronous(cmd))// Was asynchronous. Already processed. No need to process further.
                _continuationQueue.Next().HandleCommand(cmd);
        }

        public MethodBase ModelRpc(MethodBase method, ContentHeaderBase header, byte[] body)
        {
            var k = new SimpleBlockingRpcContinuation();
            lock (_rpcLock)
            {
                TransmitAndEnqueue(new Command(method, header, body), k);
                return k.GetReply(ContinuationTimeout).Method;
            }
        }

        public void ModelSend(MethodBase method, ContentHeaderBase header, byte[] body)
        {
            if (method.HasContent)
            {
                _flowControlBlock.WaitOne();
                Session.Transmit(new Command(method, header, body));
            }
            else
            {
                Session.Transmit(new Command(method, header, body));
            }
        }

        public virtual void OnBasicAck(BasicAckEventArgs args)
        {
            foreach (EventHandler<BasicAckEventArgs> h in BasicAcks?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try
                {
                    h(this, args);
                }
                catch (Exception e)
                {
                    OnCallbackException(CallbackExceptionEventArgs.Build(e, "OnBasicAck"));
                }
            }

            handleAckNack(args.DeliveryTag, args.Multiple, false);
        }

        public virtual void OnBasicNack(BasicNackEventArgs args)
        {
            foreach (EventHandler<BasicNackEventArgs> h in BasicNacks?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try
                {
                    h(this, args);
                }
                catch (Exception e)
                {
                    OnCallbackException(CallbackExceptionEventArgs.Build(e, "OnBasicNack"));
                }
            }

            handleAckNack(args.DeliveryTag, args.Multiple, true);
        }

        public virtual void OnBasicRecoverOk(EventArgs args)
        {
            foreach (EventHandler<EventArgs> h in BasicRecoverOk?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try
                {
                    h(this, args);
                }
                catch (Exception e)
                {
                    OnCallbackException(CallbackExceptionEventArgs.Build(e, "OnBasicRecover"));
                }
            }
        }

        public virtual void OnBasicReturn(BasicReturnEventArgs args)
        {
            foreach (EventHandler<BasicReturnEventArgs> h in BasicReturn?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try
                {
                    h(this, args);
                }
                catch (Exception e)
                {
                    OnCallbackException(CallbackExceptionEventArgs.Build(e, "OnBasicReturn"));
                }
            }
        }

        public virtual void OnCallbackException(CallbackExceptionEventArgs args)
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

        public virtual void OnFlowControl(FlowControlEventArgs args)
        {
            foreach (EventHandler<FlowControlEventArgs> h in FlowControl?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try
                {
                    h(this, args);
                }
                catch (Exception e)
                {
                    OnCallbackException(CallbackExceptionEventArgs.Build(e, "OnFlowControl"));
                }
            }
        }

        ///<summary>Broadcasts notification of the final shutdown of the model.</summary>
        ///<remarks>
        ///<para>
        ///Do not call anywhere other than at the end of OnSessionShutdown.
        ///</para>
        ///<para>
        ///Must not be called when m_closeReason == null, because
        ///otherwise there's a window when a new continuation could be
        ///being enqueued at the same time as we're broadcasting the
        ///shutdown event. See the definition of Enqueue() above.
        ///</para>
        ///</remarks>
        public virtual void OnModelShutdown(ShutdownEventArgs reason)
        {
            _continuationQueue.HandleModelShutdown(reason);
            EventHandler<ShutdownEventArgs> handler;
            lock (_shutdownLock)
            {
                handler = _modelShutdown;
                _modelShutdown = null;
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
                        OnCallbackException(CallbackExceptionEventArgs.Build(e, "OnModelShutdown"));
                    }
                }
            }
            lock (_unconfirmedSet.SyncRoot)
                Monitor.Pulse(_unconfirmedSet.SyncRoot);

            _flowControlBlock.Set();
        }

        public void OnSessionShutdown(object sender, ShutdownEventArgs reason)
        {
            ConsumerDispatcher.Quiesce();
            SetCloseReason(reason);
            OnModelShutdown(reason);
            BroadcastShutdownToConsumers(m_consumers, reason);
            ConsumerDispatcher.Shutdown(this);
        }

        protected void BroadcastShutdownToConsumers(IDictionary<string, IBasicConsumer> cs, ShutdownEventArgs reason)
        {
            foreach (KeyValuePair<string, IBasicConsumer> c in cs)
            {
                ConsumerDispatcher.HandleModelShutdown(c.Value, reason);
            }
        }

        public bool SetCloseReason(ShutdownEventArgs reason)
        {
            if (CloseReason == null)
            {
                lock (_shutdownLock)
                {
                    if (CloseReason == null)
                    {
                        CloseReason = reason;
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else
                return false;
        }

        public override string ToString()
        {
            return Session.ToString();
        }

        public void TransmitAndEnqueue(Command cmd, IRpcContinuation k)
        {
            Enqueue(k);
            Session.Transmit(cmd);
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // dispose managed resources
                Abort();
            }

            // dispose unmanaged resources
        }

        public abstract void ConnectionTuneOk(ushort channelMax,
            uint frameMax,
            ushort heartbeat);

        public void HandleBasicAck(ulong deliveryTag,
            bool multiple)
        {
            var e = new BasicAckEventArgs
            {
                DeliveryTag = deliveryTag,
                Multiple = multiple
            };
            OnBasicAck(e);
        }

        public void HandleBasicCancel(string consumerTag, bool nowait)
        {
            IBasicConsumer consumer;
            lock (m_consumers)
            {
                consumer = m_consumers[consumerTag];
                m_consumers.Remove(consumerTag);
            }
            if (consumer == null)
            {
                consumer = DefaultConsumer;
            }
            ConsumerDispatcher.HandleBasicCancel(consumer, consumerTag);
        }

        public void HandleBasicCancelOk(string consumerTag)
        {
            var k =
                (BasicConsumerRpcContinuation)_continuationQueue.Next();
            /*
                        Trace.Assert(k.m_consumerTag == consumerTag, string.Format(
                            "Consumer tag mismatch during cancel: {0} != {1}",
                            k.m_consumerTag,
                            consumerTag
                            ));
            */
            lock (m_consumers)
            {
                k.m_consumer = m_consumers[consumerTag];
                m_consumers.Remove(consumerTag);
            }
            ConsumerDispatcher.HandleBasicCancelOk(k.m_consumer, consumerTag);
            k.HandleCommand(null); // release the continuation.
        }

        public void HandleBasicConsumeOk(string consumerTag)
        {
            var k =
                (BasicConsumerRpcContinuation)_continuationQueue.Next();
            k.m_consumerTag = consumerTag;
            lock (m_consumers)
            {
                m_consumers[consumerTag] = k.m_consumer;
            }
            ConsumerDispatcher.HandleBasicConsumeOk(k.m_consumer, consumerTag);
            k.HandleCommand(null); // release the continuation.
        }

        public virtual void HandleBasicDeliver(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties basicProperties,
            byte[] body)
        {
            IBasicConsumer consumer;
            lock (m_consumers)
            {
                consumer = m_consumers[consumerTag];
            }
            if (consumer == null)
            {
                if (DefaultConsumer == null)
                {
                    throw new InvalidOperationException("Unsolicited delivery -" +
                                                        " see IModel.DefaultConsumer to handle this" +
                                                        " case.");
                }
                else
                {
                    consumer = DefaultConsumer;
                }
            }

            ConsumerDispatcher.HandleBasicDeliver(consumer,
                    consumerTag,
                    deliveryTag,
                    redelivered,
                    exchange,
                    routingKey,
                    basicProperties,
                    body);
        }

        public void HandleBasicGetEmpty()
        {
            var k = (BasicGetRpcContinuation)_continuationQueue.Next();
            k.m_result = null;
            k.HandleCommand(null); // release the continuation.
        }

        public virtual void HandleBasicGetOk(ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            uint messageCount,
            IBasicProperties basicProperties,
            byte[] body)
        {
            var k = (BasicGetRpcContinuation)_continuationQueue.Next();
            k.m_result = new BasicGetResult(deliveryTag,
                redelivered,
                exchange,
                routingKey,
                messageCount,
                basicProperties,
                body);
            k.HandleCommand(null); // release the continuation.
        }

        public void HandleBasicNack(ulong deliveryTag,
            bool multiple,
            bool requeue)
        {
            var e = new BasicNackEventArgs
            {
                DeliveryTag = deliveryTag,
                Multiple = multiple,
                Requeue = requeue
            };
            OnBasicNack(e);
        }

        public void HandleBasicRecoverOk()
        {
            var k = (SimpleBlockingRpcContinuation)_continuationQueue.Next();
            OnBasicRecoverOk(new EventArgs());
            k.HandleCommand(null);
        }

        public void HandleBasicReturn(ushort replyCode,
            string replyText,
            string exchange,
            string routingKey,
            IBasicProperties basicProperties,
            byte[] body)
        {
            var e = new BasicReturnEventArgs
            {
                ReplyCode = replyCode,
                ReplyText = replyText,
                Exchange = exchange,
                RoutingKey = routingKey,
                BasicProperties = basicProperties,
                Body = body
            };
            OnBasicReturn(e);
        }

        public void HandleChannelClose(ushort replyCode,
            string replyText,
            ushort classId,
            ushort methodId)
        {
            SetCloseReason(new ShutdownEventArgs(ShutdownInitiator.Peer,
                replyCode,
                replyText,
                classId,
                methodId));

            Session.Close(CloseReason, false);
            try
            {
                _Private_ChannelCloseOk();
            }
            finally
            {
                Session.Notify();
            }
        }

        public void HandleChannelCloseOk()
        {
            FinishClose();
        }

        public void HandleChannelFlow(bool active)
        {
            if (active)
            {
                _flowControlBlock.Set();
                _Private_ChannelFlowOk(active);
            }
            else
            {
                _flowControlBlock.Reset();
                _Private_ChannelFlowOk(active);
            }
            OnFlowControl(new FlowControlEventArgs(active));
        }

        public void HandleConnectionBlocked(string reason)
        {
            var cb = (Connection)Session.Connection;

            cb.HandleConnectionBlocked(reason);
        }

        public void HandleConnectionClose(ushort replyCode,
            string replyText,
            ushort classId,
            ushort methodId)
        {
            var reason = new ShutdownEventArgs(ShutdownInitiator.Peer,
                replyCode,
                replyText,
                classId,
                methodId);
            try
            {
                ((Connection)Session.Connection).InternalClose(reason);
                _Private_ConnectionCloseOk();
                SetCloseReason(Session.Connection.CloseReason);
            }
            catch (IOException)
            {
                // Ignored. We're only trying to be polite by sending
                // the close-ok, after all.
            }
            catch (AlreadyClosedException)
            {
                // Ignored. We're only trying to be polite by sending
                // the close-ok, after all.
            }
        }

        public void HandleConnectionOpenOk(string knownHosts)
        {
            var k = (ConnectionOpenContinuation)_continuationQueue.Next();
            k.m_redirect = false;
            k.m_host = null;
            k.m_knownHosts = knownHosts;
            k.HandleCommand(null); // release the continuation.
        }

        public void HandleConnectionSecure(byte[] challenge)
        {
            var k = (ConnectionStartRpcContinuation)_continuationQueue.Next();
            k.m_result = new ConnectionSecureOrTune
            {
                m_challenge = challenge
            };
            k.HandleCommand(null); // release the continuation.
        }

        public void HandleConnectionStart(byte versionMajor,
            byte versionMinor,
            IDictionary<string, object> serverProperties,
            byte[] mechanisms,
            byte[] locales)
        {
            if (m_connectionStartCell == null)
            {
                var reason =
                    new ShutdownEventArgs(ShutdownInitiator.Library,
                        Constants.CommandInvalid,
                        "Unexpected Connection.Start");
                ((Connection)Session.Connection).Close(reason);
            }
            var details = new ConnectionStartDetails
            {
                m_versionMajor = versionMajor,
                m_versionMinor = versionMinor,
                m_serverProperties = serverProperties,
                m_mechanisms = mechanisms,
                m_locales = locales
            };
            m_connectionStartCell.ContinueWithValue(details);
            m_connectionStartCell = null;
        }

        ///<summary>Handle incoming Connection.Tune
        ///methods.</summary>
        public void HandleConnectionTune(ushort channelMax, uint frameMax, ushort heartbeatInSeconds)
        {
            var k = (ConnectionStartRpcContinuation)_continuationQueue.Next();
            k.m_result = new ConnectionSecureOrTune
            {
                m_tuneDetails =
                {
                    m_channelMax = channelMax,
                    m_frameMax = frameMax,
                    m_heartbeatInSeconds = heartbeatInSeconds
                }
            };
            k.HandleCommand(null); // release the continuation.
        }

        public void HandleConnectionUnblocked()
        {
            var cb = (Connection)Session.Connection;

            cb.HandleConnectionUnblocked();
        }

        public void HandleQueueDeclareOk(string queue,
            uint messageCount,
            uint consumerCount)
        {
            var k = (QueueDeclareRpcContinuation)_continuationQueue.Next();
            k.m_result = new QueueDeclareOk(queue, messageCount, consumerCount);
            k.HandleCommand(null); // release the continuation.
        }

        public abstract void _Private_BasicCancel(string consumerTag,
            bool nowait);

        public abstract void _Private_BasicConsume(string queue,
            string consumerTag,
            bool noLocal,
            bool autoAck,
            bool exclusive,
            bool nowait,
            IDictionary<string, object> arguments);

        public abstract void _Private_BasicGet(string queue,
            bool autoAck);

        public abstract void _Private_BasicPublish(string exchange,
            string routingKey,
            bool mandatory,
            IBasicProperties basicProperties,
            byte[] body);

        public abstract void _Private_BasicRecover(bool requeue);

        public abstract void _Private_ChannelClose(ushort replyCode,
            string replyText,
            ushort classId,
            ushort methodId);

        public abstract void _Private_ChannelCloseOk();

        public abstract void _Private_ChannelFlowOk(bool active);

        public abstract void _Private_ChannelOpen(string outOfBand);

        public abstract void _Private_ConfirmSelect(bool nowait);

        public abstract void _Private_ConnectionClose(ushort replyCode,
            string replyText,
            ushort classId,
            ushort methodId);

        public abstract void _Private_ConnectionCloseOk();

        public abstract void _Private_ConnectionOpen(string virtualHost,
            string capabilities,
            bool insist);

        public abstract void _Private_ConnectionSecureOk(byte[] response);

        public abstract void _Private_ConnectionStartOk(IDictionary<string, object> clientProperties,
            string mechanism,
            byte[] response,
            string locale);

        public abstract void _Private_UpdateSecret(
            byte[] @newSecret,
            string @reason);

        public abstract void _Private_ExchangeBind(string destination,
            string source,
            string routingKey,
            bool nowait,
            IDictionary<string, object> arguments);

        public abstract void _Private_ExchangeDeclare(string exchange,
            string type,
            bool passive,
            bool durable,
            bool autoDelete,
            bool @internal,
            bool nowait,
            IDictionary<string, object> arguments);

        public abstract void _Private_ExchangeDelete(string exchange,
            bool ifUnused,
            bool nowait);

        public abstract void _Private_ExchangeUnbind(string destination,
            string source,
            string routingKey,
            bool nowait,
            IDictionary<string, object> arguments);

        public abstract void _Private_QueueBind(string queue,
            string exchange,
            string routingKey,
            bool nowait,
            IDictionary<string, object> arguments);

        public abstract void _Private_QueueDeclare(string queue,
            bool passive,
            bool durable,
            bool exclusive,
            bool autoDelete,
            bool nowait,
            IDictionary<string, object> arguments);

        public abstract uint _Private_QueueDelete(string queue,
            bool ifUnused,
            bool ifEmpty,
            bool nowait);

        public abstract uint _Private_QueuePurge(string queue,
            bool nowait);

        public void Abort()
        {
            Abort(Constants.ReplySuccess, "Goodbye");
        }

        public void Abort(ushort replyCode, string replyText)
        {
            Close(replyCode, replyText, true);
        }

        public abstract void BasicAck(ulong deliveryTag, bool multiple);

        public void BasicCancel(string consumerTag)
        {
            var k = new BasicConsumerRpcContinuation { m_consumerTag = consumerTag };

            lock (_rpcLock)
            {
                Enqueue(k);
                _Private_BasicCancel(consumerTag, false);
                k.GetReply(ContinuationTimeout);
            }
            lock (m_consumers)
            {
                m_consumers.Remove(consumerTag);
            }

            ModelShutdown -= k.m_consumer.HandleModelShutdown;
        }

        public string BasicConsume(string queue,
            bool autoAck,
            string consumerTag,
            bool noLocal,
            bool exclusive,
            IDictionary<string, object> arguments,
            IBasicConsumer consumer)
        {
            // TODO: Replace with flag
            if (ConsumerDispatcher is AsyncConsumerDispatcher)
            {
                if (!(consumer is IAsyncBasicConsumer))
                {
                    // TODO: Friendly message
                    throw new InvalidOperationException("In the async mode you have to use an async consumer");
                }
            }

            var k = new BasicConsumerRpcContinuation { m_consumer = consumer };

            lock (_rpcLock)
            {
                Enqueue(k);
                // Non-nowait. We have an unconventional means of getting
                // the RPC response, but a response is still expected.
                _Private_BasicConsume(queue, consumerTag, noLocal, autoAck, exclusive,
                    /*nowait:*/ false, arguments);
                k.GetReply(ContinuationTimeout);
            }
            string actualConsumerTag = k.m_consumerTag;

            return actualConsumerTag;
        }

        public BasicGetResult BasicGet(string queue,
            bool autoAck)
        {
            var k = new BasicGetRpcContinuation();
            lock (_rpcLock)
            {
                Enqueue(k);
                _Private_BasicGet(queue, autoAck);
                k.GetReply(ContinuationTimeout);
            }

            return k.m_result;
        }

        public abstract void BasicNack(ulong deliveryTag,
            bool multiple,
            bool requeue);

        internal void AllocatatePublishSeqNos(int count)
        {
            int c = 0;
            lock (_unconfirmedSet.SyncRoot)
            {
                while (c < count)
                {
                    if (NextPublishSeqNo > 0)
                    {
                        if (!_unconfirmedSet.Contains(NextPublishSeqNo))
                        {
                            _unconfirmedSet.Add(NextPublishSeqNo);
                        }
                        NextPublishSeqNo++;
                    }
                    c++;
                }
            }
        }

        public void BasicPublish(string exchange,
            string routingKey,
            bool mandatory,
            IBasicProperties basicProperties,
            byte[] body)
        {
            if (routingKey == null)
            {
                throw new ArgumentNullException(nameof(routingKey));
            }

            if (basicProperties == null)
            {
                basicProperties = CreateBasicProperties();
            }
            if (NextPublishSeqNo > 0)
            {
                lock (_unconfirmedSet.SyncRoot)
                {
                    if (!_unconfirmedSet.Contains(NextPublishSeqNo))
                    {
                        _unconfirmedSet.Add(NextPublishSeqNo);
                    }
                    NextPublishSeqNo++;
                }
            }
            _Private_BasicPublish(exchange,
                routingKey,
                mandatory,
                basicProperties,
                body);
        }

        public void UpdateSecret(string newSecret, string reason)
        {
            if (newSecret == null)
            {
                throw new ArgumentNullException(nameof(newSecret));
            }

            if (reason == null)
            {
                throw new ArgumentNullException(nameof(reason));
            }

            _Private_UpdateSecret(Encoding.UTF8.GetBytes(newSecret), reason);
        }

        public abstract void BasicQos(uint prefetchSize,
            ushort prefetchCount,
            bool global);

        public void BasicRecover(bool requeue)
        {
            var k = new SimpleBlockingRpcContinuation();

            lock (_rpcLock)
            {
                Enqueue(k);
                _Private_BasicRecover(requeue);
                k.GetReply(ContinuationTimeout);
            }
        }

        public abstract void BasicRecoverAsync(bool requeue);

        public abstract void BasicReject(ulong deliveryTag,
            bool requeue);

        public void Close()
        {
            Close(Constants.ReplySuccess, "Goodbye");
        }

        public void Close(ushort replyCode, string replyText)
        {
            Close(replyCode, replyText, false);
        }

        public void ConfirmSelect()
        {
            if (NextPublishSeqNo == 0UL)
            {
                NextPublishSeqNo = 1;
            }
            _Private_ConfirmSelect(false);
        }

        ///////////////////////////////////////////////////////////////////////////

        public abstract IBasicProperties CreateBasicProperties();
        public IBasicPublishBatch CreateBasicPublishBatch()
        {
            return new BasicPublishBatch(this);
        }


        public void ExchangeBind(string destination,
            string source,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            _Private_ExchangeBind(destination, source, routingKey, false, arguments);
        }

        public void ExchangeBindNoWait(string destination,
            string source,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            _Private_ExchangeBind(destination, source, routingKey, true, arguments);
        }

        public void ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments)
        {
            _Private_ExchangeDeclare(exchange, type, false, durable, autoDelete, false, false, arguments);
        }

        public void ExchangeDeclareNoWait(string exchange,
            string type,
            bool durable,
            bool autoDelete,
            IDictionary<string, object> arguments)
        {
            _Private_ExchangeDeclare(exchange, type, false, durable, autoDelete, false, true, arguments);
        }

        public void ExchangeDeclarePassive(string exchange)
        {
            _Private_ExchangeDeclare(exchange, "", true, false, false, false, false, null);
        }

        public void ExchangeDelete(string exchange,
            bool ifUnused)
        {
            _Private_ExchangeDelete(exchange, ifUnused, false);
        }

        public void ExchangeDeleteNoWait(string exchange,
            bool ifUnused)
        {
            _Private_ExchangeDelete(exchange, ifUnused, false);
        }

        public void ExchangeUnbind(string destination,
            string source,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            _Private_ExchangeUnbind(destination, source, routingKey, false, arguments);
        }

        public void ExchangeUnbindNoWait(string destination,
            string source,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            _Private_ExchangeUnbind(destination, source, routingKey, true, arguments);
        }

        public void QueueBind(string queue,
            string exchange,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            _Private_QueueBind(queue, exchange, routingKey, false, arguments);
        }

        public void QueueBindNoWait(string queue,
            string exchange,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            _Private_QueueBind(queue, exchange, routingKey, true, arguments);
        }

        public QueueDeclareOk QueueDeclare(string queue, bool durable,
                                           bool exclusive, bool autoDelete,
                                           IDictionary<string, object> arguments)
        {
            return QueueDeclare(queue, false, durable, exclusive, autoDelete, arguments);
        }

        public void QueueDeclareNoWait(string queue, bool durable, bool exclusive,
            bool autoDelete, IDictionary<string, object> arguments)
        {
            _Private_QueueDeclare(queue, false, durable, exclusive, autoDelete, true, arguments);
        }

        public QueueDeclareOk QueueDeclarePassive(string queue)
        {
            return QueueDeclare(queue, true, false, false, false, null);
        }

        public uint MessageCount(string queue)
        {
            QueueDeclareOk ok = QueueDeclarePassive(queue);
            return ok.MessageCount;
        }

        public uint ConsumerCount(string queue)
        {
            QueueDeclareOk ok = QueueDeclarePassive(queue);
            return ok.ConsumerCount;
        }

        public uint QueueDelete(string queue,
            bool ifUnused,
            bool ifEmpty)
        {
            return _Private_QueueDelete(queue, ifUnused, ifEmpty, false);
        }

        public void QueueDeleteNoWait(string queue,
            bool ifUnused,
            bool ifEmpty)
        {
            _Private_QueueDelete(queue, ifUnused, ifEmpty, true);
        }

        public uint QueuePurge(string queue)
        {
            return _Private_QueuePurge(queue, false);
        }

        public abstract void QueueUnbind(string queue,
            string exchange,
            string routingKey,
            IDictionary<string, object> arguments);

        public abstract void TxCommit();

        public abstract void TxRollback();

        public abstract void TxSelect();

        public bool WaitForConfirms(TimeSpan timeout, out bool timedOut)
        {
            if (NextPublishSeqNo == 0UL)
            {
                throw new InvalidOperationException("Confirms not selected");
            }
            bool isWaitInfinite = timeout.TotalMilliseconds == Timeout.Infinite;
            Stopwatch stopwatch = Stopwatch.StartNew();
            lock (_unconfirmedSet.SyncRoot)
            {
                while (true)
                {
                    if (!IsOpen)
                    {
                        throw new AlreadyClosedException(CloseReason);
                    }

                    if (_unconfirmedSet.Count == 0)
                    {
                        bool aux = _onlyAcksReceived;
                        _onlyAcksReceived = true;
                        timedOut = false;
                        return aux;
                    }
                    if (isWaitInfinite)
                    {
                        Monitor.Wait(_unconfirmedSet.SyncRoot);
                    }
                    else
                    {
                        TimeSpan elapsed = stopwatch.Elapsed;
                        if (elapsed > timeout || !Monitor.Wait(
                            _unconfirmedSet.SyncRoot, timeout - elapsed))
                        {
                            timedOut = true;
                            return _onlyAcksReceived;
                        }
                    }
                }
            }
        }

        public bool WaitForConfirms()
        {
            return WaitForConfirms(TimeSpan.FromMilliseconds(Timeout.Infinite), out _);
        }

        public bool WaitForConfirms(TimeSpan timeout)
        {
            return WaitForConfirms(timeout, out _);
        }

        public void WaitForConfirmsOrDie()
        {
            WaitForConfirmsOrDie(TimeSpan.FromMilliseconds(Timeout.Infinite));
        }

        public void WaitForConfirmsOrDie(TimeSpan timeout)
        {
            bool onlyAcksReceived = WaitForConfirms(timeout, out bool timedOut);
            if (!onlyAcksReceived)
            {
                Close(new ShutdownEventArgs(ShutdownInitiator.Application,
                    Constants.ReplySuccess,
                    "Nacks Received", new IOException("nack received")),
                    false);
                throw new IOException("Nacks Received");
            }
            if (timedOut)
            {
                Close(new ShutdownEventArgs(ShutdownInitiator.Application,
                    Constants.ReplySuccess,
                    "Timed out waiting for acks",
                    new IOException("timed out waiting for acks")),
                    false);
                throw new IOException("Timed out waiting for acks");
            }
        }

        internal void SendCommands(IList<Command> commands)
        {
            _flowControlBlock.WaitOne();
            AllocatatePublishSeqNos(commands.Count);
            Session.Transmit(commands);
        }

        protected virtual void handleAckNack(ulong deliveryTag, bool multiple, bool isNack)
        {
            lock (_unconfirmedSet.SyncRoot)
            {
                if (multiple)
                {
                    for (ulong i = _unconfirmedSet[0]; i <= deliveryTag; i++)
                    {
                        // removes potential duplicates
                        while (_unconfirmedSet.Remove(i))
                        {
                        }
                    }
                }
                else
                {
                    while (_unconfirmedSet.Remove(deliveryTag))
                    {
                    }
                }
                _onlyAcksReceived = _onlyAcksReceived && !isNack;
                if (_unconfirmedSet.Count == 0)
                {
                    Monitor.Pulse(_unconfirmedSet.SyncRoot);
                }
            }
        }

        private QueueDeclareOk QueueDeclare(string queue, bool passive, bool durable, bool exclusive,
            bool autoDelete, IDictionary<string, object> arguments)
        {
            var k = new QueueDeclareRpcContinuation();
            lock (_rpcLock)
            {
                Enqueue(k);
                _Private_QueueDeclare(queue, passive, durable, exclusive, autoDelete, false, arguments);
                k.GetReply(ContinuationTimeout);
            }
            return k.m_result;
        }


        public class BasicConsumerRpcContinuation : SimpleBlockingRpcContinuation
        {
            public IBasicConsumer m_consumer;
            public string m_consumerTag;
        }

        public class BasicGetRpcContinuation : SimpleBlockingRpcContinuation
        {
            public BasicGetResult m_result;
        }

        public class ConnectionOpenContinuation : SimpleBlockingRpcContinuation
        {
            public string m_host;
            public string m_knownHosts;
            public bool m_redirect;
        }

        public class ConnectionStartRpcContinuation : SimpleBlockingRpcContinuation
        {
            public ConnectionSecureOrTune m_result;
        }

        public class QueueDeclareRpcContinuation : SimpleBlockingRpcContinuation
        {
            public QueueDeclareOk m_result;
        }
    }
}
