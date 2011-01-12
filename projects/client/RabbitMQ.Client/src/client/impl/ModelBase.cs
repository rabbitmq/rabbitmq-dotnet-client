// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2010 LShift Ltd., Cohesive Financial
//   Technologies LLC., and Rabbit Technologies Ltd.
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
//   The contents of this file are subject to the Mozilla Public License
//   Version 1.1 (the "License"); you may not use this file except in
//   compliance with the License. You may obtain a copy of the License at
//   http://www.rabbitmq.com/mpl.html
//
//   Software distributed under the License is distributed on an "AS IS"
//   basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
//   License for the specific language governing rights and limitations
//   under the License.
//
//   The Original Code is The RabbitMQ .NET Client.
//
//   The Initial Developers of the Original Code are LShift Ltd,
//   Cohesive Financial Technologies LLC, and Rabbit Technologies Ltd.
//
//   Portions created before 22-Nov-2008 00:00:00 GMT by LShift Ltd,
//   Cohesive Financial Technologies LLC, or Rabbit Technologies Ltd
//   are Copyright (C) 2007-2008 LShift Ltd, Cohesive Financial
//   Technologies LLC, and Rabbit Technologies Ltd.
//
//   Portions created by LShift Ltd are Copyright (C) 2007-2010 LShift
//   Ltd. Portions created by Cohesive Financial Technologies LLC are
//   Copyright (C) 2007-2010 Cohesive Financial Technologies
//   LLC. Portions created by Rabbit Technologies Ltd are Copyright
//   (C) 2007-2010 Rabbit Technologies Ltd.
//
//   All Rights Reserved.
//
//   Contributor(s): ______________________________________.
//
//---------------------------------------------------------------------------
using System;
using System.IO;
using System.Collections;
using System.Diagnostics;
using System.Threading;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;

// We use spec version 0-9 for common constants such as frame types
// and the frame end byte, since they don't vary *within the versions
// we support*. Obviously we may need to revisit this if that ever
// changes.
using CommonFraming = RabbitMQ.Client.Framing.v0_9;

namespace RabbitMQ.Client.Impl
{
    public abstract class ModelBase : IFullModel
    {
        private readonly object m_shutdownLock = new object();
        private ModelShutdownEventHandler m_modelShutdown;
        public ShutdownEventArgs m_closeReason = null;

        private readonly object m_eventLock = new object();
        private BasicReturnEventHandler m_basicReturn;
        private BasicAckEventHandler m_basicAck;
        private CallbackExceptionEventHandler m_callbackException;
        private FlowControlEventHandler m_flowControl;
        private BasicRecoverOkEventHandler m_basicRecoverOk;

        public ManualResetEvent m_flowControlBlock = new ManualResetEvent(true);
        private readonly object m_flowSendLock = new object();

        private ulong? m_pubMsgCount = null;

        public event ModelShutdownEventHandler ModelShutdown
        {
            add
            {
                bool ok = false;
                lock (m_shutdownLock)
                {
                    if (m_closeReason == null)
                    {
                        m_modelShutdown += value;
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
                lock (m_shutdownLock)
                {
                    m_modelShutdown -= value;
                }
            }
        }

        public event BasicReturnEventHandler BasicReturn
        {
            add
            {
                lock (m_eventLock)
                {
                    m_basicReturn += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_basicReturn -= value;
                }
            }
        }

        public event BasicAckEventHandler BasicAcks
        {
            add
            {
                lock (m_eventLock)
                {
                    m_basicAck += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_basicAck -= value;
                }
            }
        }

        public event CallbackExceptionEventHandler CallbackException
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

        public event FlowControlEventHandler FlowControl
        {
            add
            {
                lock (m_eventLock)
                {
                    m_flowControl += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_flowControl -= value;
                }
            }
        }

        public event BasicRecoverOkEventHandler BasicRecoverOk
        {
            add
            {
                lock (m_eventLock)
                {
                    m_basicRecoverOk += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_basicRecoverOk -= value;
                }
            }
        }

        public IBasicConsumer DefaultConsumer { get; set; }

        public ISession m_session;

        public RpcContinuationQueue m_continuationQueue = new RpcContinuationQueue();

        ///<summary>Only used to kick-start a connection open
        ///sequence. See <see cref="ConnectionBase.Open"/> </summary>
        public BlockingCell m_connectionStartCell = null;

        public readonly IDictionary m_consumers = new Hashtable();

        public ModelBase(ISession session)
        {
            m_session = session;
            m_session.CommandReceived = new CommandHandler(HandleCommand);
            m_session.SessionShutdown += new SessionShutdownEventHandler(OnSessionShutdown);
        }

        public void HandleCommand(ISession session, Command cmd)
        {
            if (DispatchAsynchronous(cmd))
            {
                // Was asynchronous. Already processed. No need to process further.
            }
            else
            {
                m_continuationQueue.Next().HandleCommand(cmd);
            }
        }

        public void OnSessionShutdown(ISession session, ShutdownEventArgs reason)
        {
            SetCloseReason(reason);
            OnModelShutdown(reason);
        }

        public bool SetCloseReason(ShutdownEventArgs reason)
        {
            lock (m_shutdownLock)
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
            //Console.WriteLine("Model shutdown "+((Session)m_session).ChannelNumber+": "+reason);
            m_continuationQueue.HandleModelShutdown(reason);
            ModelShutdownEventHandler handler;
            lock (m_shutdownLock)
            {
                handler = m_modelShutdown;
                m_modelShutdown = null;
            }
            if (handler != null)
            {
                foreach (ModelShutdownEventHandler h in handler.GetInvocationList()) {
                    try {
                        h(this, reason);
                    } catch (Exception e) {
                        CallbackExceptionEventArgs args = new CallbackExceptionEventArgs(e);
                        args.Detail["context"] = "OnModelShutdown";
                        OnCallbackException(args);
                    }
                }
            }
            m_flowControlBlock.Set();
        }

        public virtual void OnBasicReturn(BasicReturnEventArgs args)
        {
            BasicReturnEventHandler handler;
            lock (m_eventLock)
            {
                handler = m_basicReturn;
            }
            if (handler != null)
            {
                foreach (BasicReturnEventHandler h in handler.GetInvocationList()) {
                    try {
                        h(this, args);
                    } catch (Exception e) {
                        CallbackExceptionEventArgs exnArgs = new CallbackExceptionEventArgs(e);
                        exnArgs.Detail["context"] = "OnBasicReturn";
                        OnCallbackException(exnArgs);
                    }
                }
            }
        }

        public virtual void OnBasicAck(BasicAckEventArgs args)
        {
            BasicAckEventHandler handler;
            lock (m_eventLock)
            {
                handler = m_basicAck;
            }
            if (handler != null)
            {
                foreach (BasicAckEventHandler h in handler.GetInvocationList()) {
                    try {
                        h(this, args);
                    } catch (Exception e) {
                        CallbackExceptionEventArgs exnArgs = new CallbackExceptionEventArgs(e);
                        exnArgs.Detail["context"] = "OnBasicAck";
                        OnCallbackException(exnArgs);
                    }
                }
            }
        }

        public virtual void OnCallbackException(CallbackExceptionEventArgs args)
        {
            CallbackExceptionEventHandler handler;
            lock (m_eventLock) {
                handler = m_callbackException;
            }
            if (handler != null) {
                foreach (CallbackExceptionEventHandler h in handler.GetInvocationList()) {
                    try {
                        h(this, args);
                    } catch {
                        // Exception in
                        // Callback-exception-handler. That was the
                        // app's last chance. Swallow the exception.
                        // FIXME: proper logging
                    }
                }
            }
        }

        public virtual void OnFlowControl(FlowControlEventArgs args)
        {
            FlowControlEventHandler handler;
            lock (m_eventLock)
            {
                handler = m_flowControl;
            }
            if (handler != null)
            {
                foreach (FlowControlEventHandler h in handler.GetInvocationList())
                {
                    try
                    {
                        h(this, args);
                    }
                    catch (Exception e)
                    {
                        CallbackExceptionEventArgs exnArgs = new CallbackExceptionEventArgs(e);
                        exnArgs.Detail["context"] = "OnFlowControl";
                        OnCallbackException(exnArgs);
                    }
                }
            }
        }

        public virtual void OnBasicRecoverOk(EventArgs args)
        {
            BasicRecoverOkEventHandler handler;
            lock (m_eventLock)
            {
                handler = m_basicRecoverOk;
            }
            if (handler != null)
            {
                foreach (BasicRecoverOkEventHandler h in handler.GetInvocationList())
                {
                    try
                    {
                        h(this, args);
                    }
                    catch (Exception e)
                    {
                        CallbackExceptionEventArgs exnArgs = new CallbackExceptionEventArgs(e);
                        exnArgs.Detail["context"] = "OnBasicRecoverOk";
                        OnCallbackException(exnArgs);
                    }
                }
            }
        }

        public void Enqueue(IRpcContinuation k)
        {
            bool ok = false;
            lock (m_shutdownLock)
            {
                if (m_closeReason == null)
                {
                    m_continuationQueue.Enqueue(k);
                    ok = true;
                }
            }
            if (!ok)
            {
                k.HandleModelShutdown(m_closeReason);
            }
        }

        public void TransmitAndEnqueue(Command cmd, IRpcContinuation k)
        {
            Enqueue(k);
            try
            {
                m_session.Transmit(cmd);
            }
            catch (AlreadyClosedException)
            {
                // Ignored, since the continuation will be told about
                // the closure via an OperationInterruptedException because
                // of the shutdown event propagation.
            }
        }

        public ShutdownEventArgs CloseReason
        {
            get
            {
                return m_closeReason;
            }
        }

        public bool IsOpen
        {
            get
            {
                return CloseReason == null;
            }
        }

        public ulong? PublishedMessageCount
        {
            get
            {
                return m_pubMsgCount;
            }
        }

        public void ModelSend(MethodBase method, ContentHeaderBase header, byte[] body)
        {
            if (method.HasContent)
            {
                lock (m_flowSendLock)
                {
                    m_flowControlBlock.WaitOne();
                    m_session.Transmit(new Command(method, header, body));
                }
            }
            else
            {
                m_session.Transmit(new Command(method, header, body));
            }
        }

        public MethodBase ModelRpc(MethodBase method, ContentHeaderBase header, byte[] body)
        {
            SimpleBlockingRpcContinuation k = new SimpleBlockingRpcContinuation();
            TransmitAndEnqueue(new Command(method, header, body), k);
            return k.GetReply().Method;
        }

        public abstract bool DispatchAsynchronous(Command cmd);

        public void HandleBasicDeliver(string consumerTag,
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
                consumer = (IBasicConsumer)m_consumers[consumerTag];
            }
            if (consumer == null)
            {
                if (DefaultConsumer == null) {
                    throw new InvalidOperationException("Unsolicited delivery -" +
                            " see IModel.DefaultConsumer to handle this" +
                            " case.");
                }
                else {
                    consumer = DefaultConsumer;
                }
            }

            try {
                consumer.HandleBasicDeliver(consumerTag,
                                            deliveryTag,
                                            redelivered,
                                            exchange,
                                            routingKey,
                                            basicProperties,
                                            body);
            } catch (Exception e) {
                CallbackExceptionEventArgs args = new CallbackExceptionEventArgs(e);
                args.Detail["consumer"] = consumer;
                args.Detail["context"] = "HandleBasicDeliver";
                OnCallbackException(args);
            }
        }

        public void HandleBasicReturn(ushort replyCode,
                                      string replyText,
                                      string exchange,
                                      string routingKey,
                                      IBasicProperties basicProperties,
                                      byte[] body)
        {
            BasicReturnEventArgs e = new BasicReturnEventArgs();
            e.ReplyCode = replyCode;
            e.ReplyText = replyText;
            e.Exchange = exchange;
            e.RoutingKey = routingKey;
            e.BasicProperties = basicProperties;
            e.Body = body;
            OnBasicReturn(e);
        }

        public void HandleBasicAck(ulong deliveryTag,
                                   bool multiple)
        {
            BasicAckEventArgs e = new BasicAckEventArgs();
            e.DeliveryTag = deliveryTag;
            e.Multiple = multiple;
            OnBasicAck(e);
        }

        public abstract void _Private_ChannelFlowOk(bool active);

        public void HandleChannelFlow(bool active)
        {
            if (active)
            {
                m_flowControlBlock.Set();
                _Private_ChannelFlowOk(active);
            }
            else
            {
                lock (m_flowSendLock)
                {
                    m_flowControlBlock.Reset();
                    _Private_ChannelFlowOk(active);
                }
            }
            OnFlowControl(new FlowControlEventArgs(active));
        }

        public void HandleConnectionStart(byte versionMajor,
                                          byte versionMinor,
                                          IDictionary serverProperties,
                                          byte[] mechanisms,
                                          byte[] locales)
        {
            if (m_connectionStartCell == null)
            {
                ShutdownEventArgs reason =
                    new ShutdownEventArgs(ShutdownInitiator.Library,
                              CommonFraming.Constants.CommandInvalid,
                              "Unexpected Connection.Start");
                ((ConnectionBase)m_session.Connection).Close(reason);
            }
            ConnectionStartDetails details = new ConnectionStartDetails();
            details.m_versionMajor = versionMajor;
            details.m_versionMinor = versionMinor;
            details.m_serverProperties = serverProperties;
            details.m_mechanisms = mechanisms;
            details.m_locales = locales;
            m_connectionStartCell.Value = details;
            m_connectionStartCell = null;
        }

        public void HandleConnectionClose(ushort replyCode,
                                          string replyText,
                                          ushort classId,
                                          ushort methodId)
        {
            ShutdownEventArgs reason = new ShutdownEventArgs(ShutdownInitiator.Peer,
                                 replyCode,
                                 replyText,
                                 classId,
                                 methodId);
            try
            {
                ((ConnectionBase)m_session.Connection).InternalClose(reason);
                _Private_ConnectionCloseOk();
                 SetCloseReason((m_session.Connection).CloseReason);
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

            m_session.Close(m_closeReason, false);
            try {
                _Private_ChannelCloseOk();
            } finally {
                m_session.Notify();
            }
        }

        public void FinishClose()
        {
            if (m_closeReason != null)
            {
                m_session.Close(m_closeReason);
            }
            if (m_connectionStartCell != null)
            {
                m_connectionStartCell.Value = null;
            }
        }

        ///////////////////////////////////////////////////////////////////////////

        public abstract IBasicProperties CreateBasicProperties();
        public abstract IFileProperties CreateFileProperties();
        public abstract IStreamProperties CreateStreamProperties();

        public abstract void ChannelFlow(bool active);

        public void ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete, IDictionary arguments)
        {
            _Private_ExchangeDeclare(exchange, type, false, durable, autoDelete, false, false, arguments);
        }

        public void ExchangeDeclare(string exchange, string type, bool durable)
        {
            ExchangeDeclare(exchange, type, durable, false, null);
        }

        public void ExchangeDeclare(string exchange, string type)
        {
            ExchangeDeclare(exchange, type, false);
        }

        public void ExchangeDeclarePassive(string exchange)
        {
            _Private_ExchangeDeclare(exchange, "", true, false, false, false, false, null);
        }

        public abstract void _Private_ExchangeDeclare(string exchange,
                                                      string type,
                                                      bool passive,
                                                      bool durable,
                                                      bool autoDelete,
                                                      bool @internal,
                                                      bool nowait,
                                                      IDictionary arguments);

        public abstract void ExchangeDelete(string exchange,
                                            bool ifUnused,
                                            bool nowait);

        public void ExchangeDelete(string exchange)
        {
            ExchangeDelete(exchange, false, false);
        }

        public abstract void ExchangeBind(string destination,
                                          string source,
                                          string routingKey,
                                          bool nowait,
                                          IDictionary arguments);

        public abstract void ExchangeUnbind(string destination,
                                            string source,
                                            string routingKey,
                                            bool nowait,
                                            IDictionary arguments);

        //TODO: Mark these as virtual, maybe the model has an optimized way
        //      of dealing with missing parameters.
        public string QueueDeclare()
        {
            return QueueDeclare("", false, true, true, null);
        }

        public string QueueDeclarePassive(string queue)
        {
            return _Private_QueueDeclare(queue, true, false, false, false, false, null);
        }

        public string QueueDeclare(string queue, bool durable, bool exclusive,
                                   bool autoDelete, IDictionary arguments)
        {
            return _Private_QueueDeclare(queue, false, durable, exclusive, autoDelete, false, arguments);
        }

        public abstract string _Private_QueueDeclare(string queue,
                                                     bool passive,
                                                     bool durable,
                                                     bool exclusive,
                                                     bool autoDelete,
                                                     bool nowait,
                                                     IDictionary arguments);

        public abstract void QueueBind(string queue,
                                       string exchange,
                                       string routingKey,
                                       bool nowait,
                                       IDictionary arguments);

        public abstract void QueueUnbind(string queue,
                                         string exchange,
                                         string routingKey,
                                         IDictionary arguments);

        public abstract uint QueuePurge(string queue,
                                        bool nowait);

        public abstract uint QueueDelete(string queue,
                                         bool ifUnused,
                                         bool ifEmpty,
                                         bool nowait);

        public uint QueueDelete(string queue)
        {
            return QueueDelete(queue, false, false, false);
        }

        public void ConfirmSelect() {
            ConfirmSelect(false);
        }

        public void ConfirmSelect(bool nowait) {
            m_pubMsgCount = 0;
            _Private_ConfirmSelect(nowait);
        }


        public abstract void _Private_ConfirmSelect(bool nowait);

        public string BasicConsume(string queue,
                                   IDictionary arguments,
                                   IBasicConsumer consumer)
        {
            return BasicConsume(queue, false, arguments, consumer);
        }

        public string BasicConsume(string queue,
                                   bool noAck,
                                   IDictionary arguments,
                                   IBasicConsumer consumer)
        {
            return BasicConsume(queue, noAck, "", arguments, consumer);
        }

        public string BasicConsume(string queue,
                                   bool noAck,
                                   string consumerTag,
                                   IDictionary arguments,
                                   IBasicConsumer consumer)
        {
            return BasicConsume(queue, noAck, consumerTag, false, false, arguments, consumer);
        }

        public class BasicConsumerRpcContinuation : SimpleBlockingRpcContinuation
        {
            public IBasicConsumer m_consumer;
            public string m_consumerTag;
            public BasicConsumerRpcContinuation() { }
        }

        public string BasicConsume(string queue,
                                   bool noAck,
                                   string consumerTag,
                                   bool noLocal,
                                   bool exclusive,
                                   IDictionary arguments,
                                   IBasicConsumer consumer)
        {
            ModelShutdown += new ModelShutdownEventHandler(consumer.HandleModelShutdown);

            BasicConsumerRpcContinuation k = new BasicConsumerRpcContinuation();
            k.m_consumer = consumer;

            Enqueue(k);
            // Non-nowait. We have an unconventional means of getting
            // the RPC response, but a response is still expected.
            try
            {
                _Private_BasicConsume(queue, consumerTag, noLocal, noAck, exclusive,
                    /*nowait:*/ false, arguments);
            }
            catch (AlreadyClosedException)
            {
                // Ignored, since the continuation will be told about
                // the closure via an OperationInterruptedException because
                // of the shutdown event propagation.
            }
            k.GetReply();
            string actualConsumerTag = k.m_consumerTag;

            return actualConsumerTag;
        }

        public void HandleBasicConsumeOk(string consumerTag)
        {
            BasicConsumerRpcContinuation k =
                (BasicConsumerRpcContinuation)m_continuationQueue.Next();
            k.m_consumerTag = consumerTag;
            lock (m_consumers)
            {
                m_consumers[consumerTag] = k.m_consumer;
            }
            try {
                k.m_consumer.HandleBasicConsumeOk(consumerTag);
            } catch (Exception e) {
                // FIXME: should we propagate the exception to the
                // caller of BasicConsume?
                CallbackExceptionEventArgs args = new CallbackExceptionEventArgs(e);
                args.Detail["consumer"] = k.m_consumer;
                args.Detail["context"] = "HandleBasicConsumeOk";
                OnCallbackException(args);
            }
            k.HandleCommand(null); // release the continuation.
        }

        public void BasicCancel(string consumerTag)
        {
            BasicConsumerRpcContinuation k = new BasicConsumerRpcContinuation();
            k.m_consumerTag = consumerTag;

            Enqueue(k);

            try
            {
                _Private_BasicCancel(consumerTag, false);
            }
            catch (AlreadyClosedException)
            {
                // Ignored, since the continuation will be told about
                // the closure via an OperationInterruptedException because
                // of the shutdown event propagation.
            }

            k.GetReply();

            ModelShutdown -= new ModelShutdownEventHandler(k.m_consumer.HandleModelShutdown);
        }

        public void HandleBasicCancelOk(string consumerTag)
        {
            BasicConsumerRpcContinuation k =
                (BasicConsumerRpcContinuation)m_continuationQueue.Next();

            Trace.Assert(k.m_consumerTag == consumerTag, string.Format(
                "Consumer tag mismatch during cancel: {0} != {1}",
                k.m_consumerTag,
                consumerTag
                ));

            lock (m_consumers)
            {
                k.m_consumer = (IBasicConsumer)m_consumers[consumerTag];
                m_consumers.Remove(consumerTag);
            }
            try {
                k.m_consumer.HandleBasicCancelOk(consumerTag);
            } catch (Exception e) {
                // FIXME: should we propagate the exception to the
                // caller of BasicCancel?
                CallbackExceptionEventArgs args = new CallbackExceptionEventArgs(e);
                args.Detail["consumer"] = k.m_consumer;
                args.Detail["context"] = "HandleBasicCancelOk";
                OnCallbackException(args);
            }
            k.HandleCommand(null); // release the continuation.
        }

        public class BasicGetRpcContinuation : SimpleBlockingRpcContinuation
        {
            public BasicGetResult m_result;
            public BasicGetRpcContinuation() { }
        }

        public BasicGetResult BasicGet(string queue,
                                       bool noAck)
        {
            BasicGetRpcContinuation k = new BasicGetRpcContinuation();
            Enqueue(k);
            try
            {
                _Private_BasicGet(queue, noAck);
            }
            catch (AlreadyClosedException)
            {
                // Ignored, since the continuation will be told about
                // the closure via an OperationInterruptedException because
                // of the shutdown event propagation.
            }
            k.GetReply();
            return k.m_result;
        }

        public abstract void _Private_BasicRecover(bool requeue);

        public void BasicRecover(bool requeue)
        {
            SimpleBlockingRpcContinuation k = new SimpleBlockingRpcContinuation();

            Enqueue(k);

            try
            {
                _Private_BasicRecover(requeue);
            }
            catch (AlreadyClosedException)
            {
                // Ignored, since the continuation will be told about
                // the closure via an OperationInterruptedException because
                // of the shutdown event propagation.
            }

            k.GetReply();
        }

        public abstract void BasicQos(uint prefetchSize,
                                      ushort prefetchCount,
                                      bool global);

        public abstract void _Private_BasicConsume(string queue,
                                                   string consumerTag,
                                                   bool noLocal,
                                                   bool noAck,
                                                   bool exclusive,
                                                   bool nowait,
                                                   IDictionary arguments);

        public abstract void _Private_BasicCancel(string consumerTag,
                                                  bool nowait);

        public void BasicPublish(PublicationAddress addr,
                                 IBasicProperties basicProperties,
                                 byte[] body)
        {
            BasicPublish(addr.ExchangeName,
                         addr.RoutingKey,
                         basicProperties,
                         body);
        }

        public void BasicPublish(string exchange,
                                 string routingKey,
                                 IBasicProperties basicProperties,
                                 byte[] body)
        {
            BasicPublish(exchange,
                         routingKey,
                         false,
                         false,
                         basicProperties,
                         body);
        }

        public void BasicPublish(string exchange,
                                 string routingKey,
                                 bool mandatory,
                                 bool immediate,
                                 IBasicProperties basicProperties,
                                 byte[] body)
        {
            if (basicProperties == null)
            {
                basicProperties = CreateBasicProperties();
            }
            if (m_pubMsgCount.HasValue)
                m_pubMsgCount++;
            _Private_BasicPublish(exchange,
                                  routingKey,
                                  mandatory,
                                  immediate,
                                  basicProperties,
                                  body);
        }

        public abstract void _Private_BasicPublish(string exchange,
                                                   string routingKey,
                                                   bool mandatory,
                                                   bool immediate,
                                                   IBasicProperties basicProperties,
                                                   byte[] body);

        public abstract void BasicAck(ulong deliveryTag,
                                      bool multiple);

        public abstract void BasicReject(ulong deliveryTag,
                                         bool requeue);

        public abstract void BasicRecoverAsync(bool requeue);

        public abstract void TxSelect();
        public abstract void TxCommit();
        public abstract void TxRollback();

        public abstract void DtxSelect();
        public abstract void DtxStart(string dtxIdentifier);

        void IDisposable.Dispose()
        {
            Close();
        }

        public void Close()
        {
        	Close(CommonFraming.Constants.ReplySuccess, "Goodbye");
        }

		public void Close(ushort replyCode, string replyText)
        {
        	Close(replyCode, replyText, false);
        }

        public void Abort()
        {
            Abort(CommonFraming.Constants.ReplySuccess, "Goodbye");
        }

        public void Abort(ushort replyCode, string replyText)
        {
            Close(replyCode, replyText, true);
        }

        public void Close(ushort replyCode, string replyText, bool abort)
        {
            ShutdownContinuation k = new ShutdownContinuation();
            ModelShutdown += new ModelShutdownEventHandler(k.OnShutdown);

            try {
                if (SetCloseReason(new ShutdownEventArgs(ShutdownInitiator.Application,
                                     replyCode,
                                     replyText)))
                {
                    _Private_ChannelClose(replyCode, replyText, 0, 0);
                }
                k.Wait();
            } catch (AlreadyClosedException ace) {
            	if (!abort)
            		throw ace;
            } catch (IOException ioe) {
            	if (!abort)
            		throw ioe;
            }
        }

        public void HandleChannelCloseOk()
        {
            FinishClose();
        }

        public abstract void _Private_ChannelOpen(string outOfBand);

        public abstract void _Private_ChannelClose(ushort replyCode,
                                                   string replyText,
                                                   ushort classId,
                                                   ushort methodId);

        public abstract void _Private_ChannelCloseOk();

        public abstract void _Private_BasicGet(string queue,
                                               bool noAck);

        public void HandleBasicGetOk(ulong deliveryTag,
                                     bool redelivered,
                                     string exchange,
                                     string routingKey,
                                     uint messageCount,
                                     IBasicProperties basicProperties,
                                     byte[] body)
        {
            BasicGetRpcContinuation k = (BasicGetRpcContinuation)m_continuationQueue.Next();
            k.m_result = new BasicGetResult(deliveryTag,
                                            redelivered,
                                            exchange,
                                            routingKey,
                                            messageCount,
                                            basicProperties,
                                            body);
            k.HandleCommand(null); // release the continuation.
        }

        public void HandleBasicGetEmpty()
        {
            BasicGetRpcContinuation k = (BasicGetRpcContinuation)m_continuationQueue.Next();
            k.m_result = null;
            k.HandleCommand(null); // release the continuation.
        }

        public void HandleBasicRecoverOk()
        {
            SimpleBlockingRpcContinuation k = (SimpleBlockingRpcContinuation)m_continuationQueue.Next();
            OnBasicRecoverOk(new EventArgs());
            k.HandleCommand(null);
        }

        public class ConnectionStartRpcContinuation : SimpleBlockingRpcContinuation
        {
            public ConnectionSecureOrTune m_result;
            public ConnectionStartRpcContinuation() { }
        }

        public ConnectionSecureOrTune ConnectionStartOk(IDictionary clientProperties,
                                                        string mechanism,
                                                        byte[] response,
                                                        string locale)
        {
            ConnectionStartRpcContinuation k = new ConnectionStartRpcContinuation();
            Enqueue(k);
            try
            {
                _Private_ConnectionStartOk(clientProperties, mechanism,
                                           response, locale);
            }
            catch (AlreadyClosedException)
            {
                // Ignored, see BasicGet
            }
            k.GetReply();
            return k.m_result;
        }

        public abstract void _Private_ConnectionStartOk(IDictionary clientProperties,
                                                        string mechanism,
                                                        byte[] response,
                                                        string locale);

        public void HandleConnectionSecure(byte[] challenge)
        {
            ConnectionStartRpcContinuation k = (ConnectionStartRpcContinuation)m_continuationQueue.Next();
            k.m_result = new ConnectionSecureOrTune();
            k.m_result.m_challenge = challenge;
            k.HandleCommand(null); // release the continuation.
        }

        public ConnectionSecureOrTune ConnectionSecureOk(byte[] response)
        {
            ConnectionStartRpcContinuation k = new ConnectionStartRpcContinuation();
            Enqueue(k);
            try
            {
                _Private_ConnectionSecureOk(response);
            }
            catch (AlreadyClosedException)
            {
                // Ignored, see BasicGet
            }
            k.GetReply();
            return k.m_result;
        }

        public abstract void _Private_ConnectionSecureOk(byte[] response);

        ///<summary>Handle incoming Connection.Tune
        ///methods.</summary>
        public void HandleConnectionTune(ushort channelMax,
                                         uint frameMax,
                                         ushort heartbeat)
        {
            ConnectionStartRpcContinuation k = (ConnectionStartRpcContinuation)m_continuationQueue.Next();
            k.m_result = new ConnectionSecureOrTune();
            k.m_result.m_tuneDetails.m_channelMax = channelMax;
            k.m_result.m_tuneDetails.m_frameMax = frameMax;
            k.m_result.m_tuneDetails.m_heartbeat = heartbeat;
            k.HandleCommand(null); // release the continuation.
        }

        public abstract void ConnectionTuneOk(ushort channelMax,
                                              uint frameMax,
                                              ushort heartbeat);

        public class ConnectionOpenContinuation : SimpleBlockingRpcContinuation
        {
            public bool m_redirect;
            public string m_host;
            public string m_knownHosts;
            public ConnectionOpenContinuation() { }
        }

        public string ConnectionOpen(string virtualHost,
                                     string capabilities,
                                     bool insist)
        {
            ConnectionOpenContinuation k = new ConnectionOpenContinuation();
            Enqueue(k);
            try {
                _Private_ConnectionOpen(virtualHost, capabilities, insist);
            }
            catch (AlreadyClosedException)
            {
                // Ignored, see BasicGet
            }
            k.GetReply();
            if (k.m_redirect) {
                throw new RedirectException(m_session.Connection.Protocol,
                                            k.m_host,
                                            k.m_knownHosts);
            } else {
                return k.m_knownHosts;
            }
        }

        public abstract void _Private_ConnectionOpen(string virtualHost,
                                                     string capabilities,
                                                     bool insist);

        public void HandleConnectionOpenOk(string knownHosts)
        {
            ConnectionOpenContinuation k = (ConnectionOpenContinuation)m_continuationQueue.Next();
            k.m_redirect = false;
            k.m_host = null;
            k.m_knownHosts = knownHosts;
            k.HandleCommand(null); // release the continuation.
        }

        public void HandleConnectionRedirect(string host,
                                             string knownHosts)
        {
            ConnectionOpenContinuation k = (ConnectionOpenContinuation)m_continuationQueue.Next();
            k.m_redirect = true;
            k.m_host = host;
            k.m_knownHosts = knownHosts;
            k.HandleCommand(null); // release the continuation.
        }

        public abstract void _Private_ConnectionClose(ushort replyCode,
                                                      string replyText,
                                                      ushort classId,
                                                      ushort methodId);

        public abstract void _Private_ConnectionCloseOk();

        public override string ToString() {
            return m_session.ToString();
        }
    }
}
