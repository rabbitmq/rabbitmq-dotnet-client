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

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    public class AutorecoveringModel : IFullModel, IRecoverable
    {
        protected AutorecoveringConnection m_connection;
        protected Model m_delegate;

        public readonly object m_eventLock = new object();

        private RecoveryEventHandler m_recovery;

        protected List<ModelShutdownEventHandler> m_recordedShutdownEventHandlers =
            new List<ModelShutdownEventHandler>();
        protected List<BasicReturnEventHandler> m_recordedBasicReturnEventHandlers =
            new List<BasicReturnEventHandler>();
        protected List<BasicAckEventHandler> m_recordedBasicAckEventHandlers =
            new List<BasicAckEventHandler>();
        protected List<BasicNackEventHandler> m_recordedBasicNackEventHandlers =
            new List<BasicNackEventHandler>();
        protected List<CallbackExceptionEventHandler> m_recordedCallbackExceptionEventHandlers =
            new List<CallbackExceptionEventHandler>();

        protected ushort prefetchCountConsumer = 0;
        protected ushort prefetchCountGlobal   = 0;
        protected bool usesPublisherConfirms   = false;
        protected bool usesTransactions        = false;

        public AutorecoveringModel(AutorecoveringConnection conn, Model _delegate)
        {
            this.m_connection = conn;
            this.m_delegate   = _delegate;
        }

        public void AutomaticallyRecover(AutorecoveringConnection conn, IConnection connDelegate)
        {
            this.m_connection = conn;
            this.m_delegate   = (Model)connDelegate.CreateModel();
            // TODO: inherit ack offset

            this.RecoverModelShutdownHandlers();
            this.RecoverState();

            this.RecoverBasicReturnHandlers();
            this.RecoverBasicAckHandlers();
            this.RecoverBasicNackHandlers();
            this.RecoverCallbackExceptionHandlers();

            this.RunRecoveryEventHandlers();
        }

        public IModel Delegate
        {
            get { return this.m_delegate; }
        }


        public event ModelShutdownEventHandler ModelShutdown
        {
            add
            {
                lock(this.m_eventLock)
                {
                    m_recordedShutdownEventHandlers.Add(value);
                    m_delegate.ModelShutdown += value;
                }
            }
            remove
            {
                lock(this.m_eventLock)
                {
                    m_recordedShutdownEventHandlers.Remove(value);
                    m_delegate.ModelShutdown -= value;
                }
            }
        }

        public event BasicReturnEventHandler BasicReturn
        {
            add
            {
                lock(this.m_eventLock)
                {
                    m_recordedBasicReturnEventHandlers.Add(value);
                    m_delegate.BasicReturn += value;
                }
            }
            remove
            {
                lock(this.m_eventLock)
                {
                    m_recordedBasicReturnEventHandlers.Remove(value);
                    m_delegate.BasicReturn -= value;
                }
            }
        }

        public event BasicAckEventHandler BasicAcks
        {
            add
            {
                lock(this.m_eventLock)
                {
                    m_recordedBasicAckEventHandlers.Add(value);
                    m_delegate.BasicAcks += value;
                }
            }
            remove
            {
                lock(this.m_eventLock)
                {
                    m_recordedBasicAckEventHandlers.Remove(value);
                    m_delegate.BasicAcks -= value;
                }
            }
        }

        public event BasicNackEventHandler BasicNacks
        {
            add
            {
                lock(this.m_eventLock)
                {
                    m_recordedBasicNackEventHandlers.Add(value);
                    m_delegate.BasicNacks += value;
                }
            }
            remove
            {
                lock(this.m_eventLock)
                {
                    m_recordedBasicNackEventHandlers.Remove(value);
                    m_delegate.BasicNacks -= value;
                }
            }
        }

        public event CallbackExceptionEventHandler CallbackException
        {
            add
            {
                lock(this.m_eventLock)
                {
                    m_recordedCallbackExceptionEventHandlers.Add(value);
                    m_delegate.CallbackException += value;
                }
            }
            remove
            {
                lock(this.m_eventLock)
                {
                    m_recordedCallbackExceptionEventHandlers.Remove(value);
                    m_delegate.CallbackException -= value;
                }
            }
        }

        public event FlowControlEventHandler FlowControl
        {
            add
            {
                // TODO: record and re-add handlers
                m_delegate.FlowControl += value;
            }
            remove
            {
                m_delegate.FlowControl -= value;
            }
        }

        public event BasicRecoverOkEventHandler BasicRecoverOk
        {
            add
            {
                // TODO: record and re-add handlers
                m_delegate.BasicRecoverOk += value;
            }
            remove
            {
                m_delegate.BasicRecoverOk -= value;
            }
        }

        public event RecoveryEventHandler Recovery
        {
            add
            {
                this.m_recovery += value;
            }
            remove
            {
                this.m_recovery -= value;
            }
        }

        public int ChannelNumber
        {
            get
            {
                return m_delegate.ChannelNumber;
            }
        }

        public IBasicConsumer DefaultConsumer
        {
            get
            {
                return m_delegate.DefaultConsumer;
            }
            set
            {
                m_delegate.DefaultConsumer = value;
            }
        }




        void IDisposable.Dispose()
        {
            m_delegate.Close();
        }

        public void Close()
        {
            try
            {
                m_delegate.Close();
            } finally
            {
                m_connection.UnregisterModel(this);
            }

        }

        public void Close(ushort replyCode, string replyText)
        {
            try
            {
                m_delegate.Close(replyCode, replyText);
            } finally
            {
                m_connection.UnregisterModel(this);
            }
        }

        public void Abort()
        {
            try {
                m_delegate.Abort();
            } finally
            {
                m_connection.UnregisterModel(this);
            }
        }

        public void Abort(ushort replyCode, string replyText)
        {
            try
            {
                m_delegate.Abort(replyCode, replyText);
            }
            finally
            {
                m_connection.UnregisterModel(this);
            }
        }

        public void Close(ushort replyCode, string replyText, bool abort)
        {
            try
            {
                m_delegate.Close(replyCode, replyText, abort);
            }
            finally
            {
                m_connection.UnregisterModel(this);
            }
        }

        public void Close(ShutdownEventArgs reason, bool abort)
        {
            try
            {
                m_delegate.Close(reason, abort);
            }
            finally
            {
                m_connection.UnregisterModel(this);
            }
        }

        public void HandleChannelCloseOk()
        {
            m_delegate.HandleChannelCloseOk();
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

        public bool IsClosed
        {
            get
            {
                return m_delegate.IsClosed;
            }
        }

        public ulong NextPublishSeqNo
        {
            get
            {
                return m_delegate.NextPublishSeqNo;
            }
        }

        public bool DispatchAsynchronous(Command cmd)
        {
            return m_delegate.DispatchAsynchronous(cmd);
        }

        public void HandleBasicDeliver(string consumerTag,
                                       ulong deliveryTag,
                                       bool redelivered,
                                       string exchange,
                                       string routingKey,
                                       IBasicProperties basicProperties,
                                       byte[] body)
        {
            m_delegate.HandleBasicDeliver(consumerTag, deliveryTag, redelivered, exchange,
                                          routingKey, basicProperties, body);
        }

        public void HandleBasicCancel(string consumerTag, bool nowait)
        {
            m_delegate.HandleBasicCancel(consumerTag, nowait);
        }

        public void HandleBasicReturn(ushort replyCode,
                                      string replyText,
                                      string exchange,
                                      string routingKey,
                                      IBasicProperties basicProperties,
                                      byte[] body)
        {
            m_delegate.HandleBasicReturn(replyCode, replyText, exchange,
                                         routingKey, basicProperties, body);
        }

        public void HandleBasicAck(ulong deliveryTag,
                                   bool multiple)
        {
            m_delegate.HandleBasicAck(deliveryTag, multiple);
        }

        public void HandleBasicNack(ulong deliveryTag,
                                    bool multiple,
                                    bool requeue)
        {
            m_delegate.HandleBasicNack(deliveryTag, multiple, requeue);
        }

        public void HandleChannelFlow(bool active)
        {
            m_delegate.HandleChannelFlow(active);
        }

        public void HandleConnectionStart(byte versionMajor,
                                          byte versionMinor,
                                          IDictionary<string, object> serverProperties,
                                          byte[] mechanisms,
                                          byte[] locales)
        {
            m_delegate.HandleConnectionStart(versionMajor, versionMinor, serverProperties,
                                             mechanisms, locales);
        }

        public void HandleConnectionClose(ushort replyCode,
                                          string replyText,
                                          ushort classId,
                                          ushort methodId)
        {
            m_delegate.HandleConnectionClose(replyCode, replyText, classId, methodId);
        }

        public void HandleConnectionBlocked(string reason)
        {
            m_delegate.HandleConnectionBlocked(reason);
        }

        public void HandleConnectionUnblocked()
        {
            m_delegate.HandleConnectionUnblocked();
        }

        public void HandleChannelClose(ushort replyCode,
                                       string replyText,
                                       ushort classId,
                                       ushort methodId)
        {
            m_delegate.HandleChannelClose(replyCode, replyText, classId, methodId);
        }

        public void FinishClose()
        {
            m_delegate.FinishClose();
        }

        public IBasicProperties CreateBasicProperties()
        {
            return m_delegate.CreateBasicProperties();
        }

        public void ExchangeDeclare(string exchange, string type, bool durable)
        {
            ExchangeDeclare(exchange, type, durable, false, null);
        }

        public void ExchangeDeclare(string exchange, string type)
        {
            ExchangeDeclare(exchange, type, false, false, null);
        }

        public void ExchangeDeclare(string exchange, string type, bool durable,
                                    bool autoDelete, IDictionary<string, object> arguments)
        {
            var rx = new RecordedExchange(this, exchange).
                WithType(type).
                WithDurable(durable).
                WithAutoDelete(autoDelete).
                WithArguments(arguments);
            m_delegate.ExchangeDeclare(exchange, type, durable,
                                       autoDelete, arguments);
            m_connection.RecordExchange(exchange, rx);
        }

        public void ExchangeDeclarePassive(string exchange)
        {
            m_delegate.ExchangeDeclarePassive(exchange);
        }

        public void ExchangeDeclareNoWait(string exchange,
                                          string type,
                                          bool durable,
                                          bool autoDelete,
                                          IDictionary<string, object> arguments)
        {
            var rx = new RecordedExchange(this, exchange).
                WithType(type).
                WithDurable(durable).
                WithAutoDelete(autoDelete).
                WithArguments(arguments);
            m_delegate.ExchangeDeclareNoWait(exchange, type, durable,
                                             autoDelete, arguments);
            m_connection.RecordExchange(exchange, rx);
        }

        public void ExchangeDelete(string exchange,
                                   bool ifUnused)
        {
            m_delegate.ExchangeDelete(exchange, ifUnused);
            m_connection.DeleteRecordedExchange(exchange);
        }

        public void ExchangeDelete(string exchange)
        {
            ExchangeDelete(exchange, false);
        }

        public void ExchangeDeleteNoWait(string exchange,
                                         bool ifUnused)
        {
            m_delegate.ExchangeDeleteNoWait(exchange, ifUnused);
            m_connection.DeleteRecordedExchange(exchange);
        }

        public void ExchangeBind(string destination,
                                 string source,
                                 string routingKey)
        {
            this.ExchangeBind(destination, source, routingKey, null);
        }

        public void ExchangeBind(string destination,
                                 string source,
                                 string routingKey,
                                 IDictionary<string, object> arguments)
        {
            var eb = new RecordedExchangeBinding(this).
                WithSource(source).
                WithDestination(destination).
                WithRoutingKey(routingKey).
                WithArguments(arguments);
            m_connection.RecordBinding(eb);
            m_delegate.ExchangeBind(destination, source, routingKey, arguments);
        }

        public void ExchangeBindNoWait(string destination,
                                       string source,
                                       string routingKey,
                                       IDictionary<string, object> arguments)
        {
            m_delegate.ExchangeBindNoWait(destination, source, routingKey, arguments);
        }

        public void ExchangeUnbind(string destination,
                                   string source,
                                   string routingKey,
                                   IDictionary<string, object> arguments)
        {
            var eb = new RecordedExchangeBinding(this).
                WithSource(source).
                WithDestination(destination).
                WithRoutingKey(routingKey).
                WithArguments(arguments);
            m_connection.DeleteRecordedBinding(eb);
            m_delegate.ExchangeUnbind(destination, source, routingKey, arguments);
        }

        public void ExchangeUnbind(string destination,
                                   string source,
                                   string routingKey)
        {
            this.ExchangeUnbind(destination, source, routingKey, null);
        }

        public void ExchangeUnbindNoWait(string destination,
                                         string source,
                                         string routingKey,
                                         IDictionary<string, object> arguments)
        {
            m_delegate.ExchangeUnbind(destination, source, routingKey, arguments);
        }

        public QueueDeclareOk QueueDeclare()
        {
            return QueueDeclare("", false, true, true, null);
        }

        public QueueDeclareOk QueueDeclarePassive(string queue)
        {
            return m_delegate.QueueDeclarePassive(queue);
        }

        public void QueueDeclareNoWait(string queue, bool durable, bool exclusive,
                                       bool autoDelete, IDictionary<string, object> arguments)
        {
            m_delegate.QueueDeclareNoWait(queue, durable, exclusive,
                                          autoDelete, arguments);
            var rq = new RecordedQueue(this, queue).
                Durable(durable).
                Exclusive(exclusive).
                AutoDelete(autoDelete).
                Arguments(arguments);
            m_connection.RecordQueue(queue, rq);
        }

        public QueueDeclareOk QueueDeclare(string queue, bool durable, bool exclusive,
                                           bool autoDelete, IDictionary<string, object> arguments)
        {
            var result = m_delegate.QueueDeclare(queue, durable, exclusive,
                                                 autoDelete, arguments);
            var rq     = new RecordedQueue(this, result.QueueName).
                Durable(durable).
                Exclusive(exclusive).
                AutoDelete(autoDelete).
                Arguments(arguments);
            m_connection.RecordQueue(result.QueueName, rq);
            return result;
        }


        public void QueueBind(string queue,
                              string exchange,
                              string routingKey,
                              IDictionary<string, object> arguments)
        {
            var qb = new RecordedQueueBinding(this).
                WithSource(exchange).
                WithDestination(queue).
                WithRoutingKey(routingKey).
                WithArguments(arguments);
            m_connection.RecordBinding(qb);
            m_delegate.QueueBind(queue, exchange, routingKey, arguments);
        }

        public void QueueBind(string queue,
                              string exchange,
                              string routingKey)
        {
            this.QueueBind(queue, exchange, routingKey, null);
        }

        public void QueueBindNoWait(string queue,
                                    string exchange,
                                    string routingKey,
                                    IDictionary<string, object> arguments)
        {
            m_delegate.QueueBind(queue, exchange, routingKey, arguments);
        }

        public void QueueUnbind(string queue,
                                string exchange,
                                string routingKey,
                                IDictionary<string, object> arguments)
        {
            var qb = new RecordedQueueBinding(this).
                WithSource(exchange).
                WithDestination(queue).
                WithRoutingKey(routingKey).
                WithArguments(arguments);
            m_connection.DeleteRecordedBinding(qb);
            m_delegate.QueueUnbind(queue, exchange, routingKey, arguments);
            m_connection.MaybeDeleteRecordedAutoDeleteExchange(exchange);
        }

        public uint QueuePurge(string queue)
        {
            return m_delegate.QueuePurge(queue);
        }

        public uint QueueDelete(string queue,
                                bool ifUnused,
                                bool ifEmpty)
        {
            var result = m_delegate.QueueDelete(queue, ifUnused, ifEmpty);
            m_connection.DeleteRecordedQueue(queue);
            return result;
        }

        public uint QueueDelete(string queue)
        {
            return QueueDelete(queue, false, false);
        }

        public void QueueDeleteNoWait(string queue,
                                      bool ifUnused,
                                      bool ifEmpty)
        {
            m_delegate.QueueDeleteNoWait(queue, ifUnused, ifEmpty);
            m_connection.DeleteRecordedQueue(queue);
        }

        public void ConfirmSelect()
        {
            this.usesPublisherConfirms = true;
            m_delegate.ConfirmSelect();
        }

        public bool WaitForConfirms(TimeSpan timeout, out bool timedOut)
        {
            return m_delegate.WaitForConfirms(timeout, out timedOut);
        }

        public bool WaitForConfirms(TimeSpan timeout)
        {
            return m_delegate.WaitForConfirms(timeout);
        }

        public bool WaitForConfirms()
        {
            return m_delegate.WaitForConfirms();
        }

        public void WaitForConfirmsOrDie()
        {
            m_delegate.WaitForConfirmsOrDie();
        }

        public void WaitForConfirmsOrDie(TimeSpan timeout)
        {
            m_delegate.WaitForConfirmsOrDie(timeout);
        }

        public string BasicConsume(string queue,
                                   bool noAck,
                                   IBasicConsumer consumer)
        {
            return this.BasicConsume(queue, noAck, "", consumer);
        }

        public string BasicConsume(string queue,
                                   bool noAck,
                                   string consumerTag,
                                   IBasicConsumer consumer)
        {
            return this.BasicConsume(queue, noAck, consumerTag, null, consumer);
        }

        public string BasicConsume(string queue,
                                   bool noAck,
                                   string consumerTag,
                                   IDictionary<string, object> arguments,
                                   IBasicConsumer consumer)
        {
            return this.BasicConsume(queue, noAck, consumerTag, false, false,
                                     arguments, consumer);
        }

        public string BasicConsume(string queue,
                                   bool noAck,
                                   string consumerTag,
                                   bool noLocal,
                                   bool exclusive,
                                   IDictionary<string, object> arguments,
                                   IBasicConsumer consumer)
        {
            var result = m_delegate.BasicConsume(queue, noAck, consumerTag, noLocal,
                                                 exclusive, arguments, consumer);
            var rc = new RecordedConsumer(this, queue).
                WithConsumerTag(result).
                WithConsumer(consumer).
                WithExclusive(exclusive).
                WithAutoAck(noAck).
                WithArguments(arguments);
            m_connection.RecordConsumer(result, rc);
            return result;
        }

        public void HandleBasicConsumeOk(string consumerTag)
        {
            m_delegate.HandleBasicConsumeOk(consumerTag);
        }

        public void BasicCancel(string consumerTag)
        {
            var cons = m_connection.DeleteRecordedConsumer(consumerTag);
            if(cons != null)
            {
                m_connection.MaybeDeleteRecordedAutoDeleteQueue(cons.Queue);
            }
            m_delegate.BasicCancel(consumerTag);
        }

        public void HandleBasicCancelOk(string consumerTag)
        {
            m_delegate.HandleBasicCancelOk(consumerTag);
        }

        public BasicGetResult BasicGet(string queue,
                                       bool noAck)
        {
            return m_delegate.BasicGet(queue, noAck);
        }

        public void BasicRecover(bool requeue)
        {
            m_delegate.BasicRecover(requeue);
        }

        public void BasicQos(ushort prefetchCount,
                             bool global)
        {
            m_delegate.BasicQos(0, prefetchCount, global);
        }

        public void BasicQos(uint prefetchSize,
                             ushort prefetchCount,
                             bool global)
        {
            if(global)
            {
                this.prefetchCountGlobal = prefetchCount;
            } else
            {
                this.prefetchCountConsumer = prefetchCount;
            }
            m_delegate.BasicQos(prefetchSize, prefetchCount, global);
        }

        public void BasicPublish(PublicationAddress addr,
                                 IBasicProperties basicProperties,
                                 byte[] body)
        {
            m_delegate.BasicPublish(addr.ExchangeName,
                                    addr.RoutingKey,
                                    basicProperties,
                                    body);
        }

        public void BasicPublish(string exchange,
                                 string routingKey,
                                 IBasicProperties basicProperties,
                                 byte[] body)
        {
            m_delegate.BasicPublish(exchange,
                                    routingKey,
                                    basicProperties,
                                    body);
        }

        public void BasicPublish(string exchange,
                                 string routingKey,
                                 bool mandatory,
                                 IBasicProperties basicProperties,
                                 byte[] body)
        {
            m_delegate.BasicPublish(exchange,
                                    routingKey,
                                    mandatory,
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
            m_delegate.BasicPublish(exchange, routingKey, mandatory, immediate,
                                    basicProperties, body);
        }


        public void BasicAck(ulong deliveryTag,
                             bool multiple)
        {
            m_delegate.BasicAck(deliveryTag, multiple);
        }

        public void BasicReject(ulong deliveryTag,
                                bool requeue)
        {
            m_delegate.BasicReject(deliveryTag, requeue);
        }

        public void BasicNack(ulong deliveryTag,
                              bool multiple,
                              bool requeue)
        {
            m_delegate.BasicNack(deliveryTag, multiple, requeue);
        }

        public void BasicRecoverAsync(bool requeue)
        {
            m_delegate.BasicRecoverAsync(requeue);
        }

        public void TxSelect()
        {
            this.usesTransactions = true;
            m_delegate.TxSelect();
        }

        public void TxCommit()
        {
            m_delegate.TxCommit();
        }

        public void TxRollback()
        {
            m_delegate.TxRollback();
        }




        public void HandleBasicGetOk(ulong deliveryTag,
                                     bool redelivered,
                                     string exchange,
                                     string routingKey,
                                     uint messageCount,
                                     IBasicProperties basicProperties,
                                     byte[] body){
            m_delegate.HandleBasicGetOk(deliveryTag, redelivered, exchange, routingKey,
                                        messageCount, basicProperties, body);
        }

        public void HandleBasicGetEmpty()
        {
            m_delegate.HandleBasicGetEmpty();
        }

        public void HandleBasicRecoverOk()
        {
            m_delegate.HandleBasicRecoverOk();
        }

        public void HandleCommand(ISession session, Command cmd)
        {
            m_delegate.HandleCommand(session, cmd);
        }

        public void OnSessionShutdown(ISession session, ShutdownEventArgs reason)
        {
            m_delegate.OnSessionShutdown(session, reason);
        }

        public bool SetCloseReason(ShutdownEventArgs reason)
        {
            return m_delegate.SetCloseReason(reason);
        }

        public virtual void OnModelShutdown(ShutdownEventArgs reason)
        {
            m_delegate.OnModelShutdown(reason);
        }

        public virtual void OnBasicReturn(BasicReturnEventArgs args)
        {
            m_delegate.OnBasicReturn(args);
        }

        public virtual void OnBasicAck(BasicAckEventArgs args)
        {
            m_delegate.OnBasicAck(args);
        }

        public virtual void OnBasicNack(BasicNackEventArgs args)
        {
            m_delegate.OnBasicNack(args);
        }

        public virtual void OnCallbackException(CallbackExceptionEventArgs args)
        {
            m_delegate.OnCallbackException(args);
        }

        public virtual void OnFlowControl(FlowControlEventArgs args)
        {
            m_delegate.OnFlowControl(args);
        }

        public virtual void OnBasicRecoverOk(EventArgs args)
        {
            m_delegate.OnBasicRecoverOk(args);
        }

        public void HandleQueueDeclareOk(string queue,
                                         uint messageCount,
                                         uint consumerCount)
        {
            m_delegate.HandleQueueDeclareOk(queue, messageCount, consumerCount);
        }

        public override string ToString() {
            return m_delegate.ToString();
        }


        protected void RecoverModelShutdownHandlers()
        {
            var handler = this.m_recordedShutdownEventHandlers;
            if(handler != null)
            {
                foreach(var eh in handler)
                {
                    this.m_delegate.ModelShutdown += eh;
                }
            }
        }

        protected void RecoverBasicReturnHandlers()
        {
            var handler = this.m_recordedBasicReturnEventHandlers;
            if(handler != null)
            {
                foreach(var eh in handler)
                {
                    this.m_delegate.BasicReturn += eh;
                }
            }
        }

        protected void RecoverBasicAckHandlers()
        {
            var handler = this.m_recordedBasicAckEventHandlers;
            if(handler != null)
            {
                foreach(var eh in handler)
                {
                    this.m_delegate.BasicAcks += eh;
                }
            }
        }

        protected void RecoverBasicNackHandlers()
        {
            var handler = this.m_recordedBasicNackEventHandlers;
            if(handler != null)
            {
                foreach(var eh in handler)
                {
                    this.m_delegate.BasicNacks += eh;
                }
            }
        }

        protected void RecoverCallbackExceptionHandlers()
        {
            var handler = this.m_recordedCallbackExceptionEventHandlers;
            if(handler != null)
            {
                foreach(var eh in handler)
                {
                    this.m_delegate.CallbackException += eh;
                }
            }
        }

        protected void RecoverState()
        {
            if(prefetchCountConsumer != 0)
            {
                BasicQos(prefetchCountConsumer, false);
            }

            if(prefetchCountGlobal != 0)
            {
                BasicQos(prefetchCountGlobal, true);
            }

            if(usesPublisherConfirms)
            {
                ConfirmSelect();
            }

            if(usesTransactions)
            {
                TxSelect();
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
                        args.Detail["context"] = "OnModelRecovery";
                        this.m_delegate.OnCallbackException(args);
                    }
                }
            }
        }


        public void _Private_ExchangeDeclare(string exchange,
                                             string type,
                                             bool passive,
                                             bool durable,
                                             bool autoDelete,
                                             bool @internal,
                                             bool nowait,
                                             IDictionary<string, object> arguments)
        {
            _Private_ExchangeDeclare(exchange, type, passive,
                                     durable, autoDelete, @internal,
                                     nowait, arguments);
        }

        public void _Private_ExchangeDelete(string exchange,
                                            bool ifUnused,
                                            bool nowait)
        {
            _Private_ExchangeDelete(exchange, ifUnused, nowait);
        }

        public void _Private_ExchangeBind(string destination,
                                          string source,
                                          string routingKey,
                                          bool nowait,
                                          IDictionary<string, object> arguments)
        {
            _Private_ExchangeBind(destination, source, routingKey,
                                  nowait, arguments);
        }

        public void _Private_ExchangeUnbind(string destination,
                                            string source,
                                            string routingKey,
                                            bool nowait,
                                            IDictionary<string, object> arguments)
        {
            m_delegate._Private_ExchangeUnbind(destination, source, routingKey,
                                               nowait, arguments);
        }

        public void _Private_QueueDeclare(string queue,
                                          bool passive,
                                          bool durable,
                                          bool exclusive,
                                          bool autoDelete,
                                          bool nowait,
                                          IDictionary<string, object> arguments)
        {
            m_delegate._Private_QueueDeclare(queue, passive,
                                             durable, exclusive, autoDelete,
                                             nowait, arguments);
        }

        public void _Private_QueueBind(string queue,
                                       string exchange,
                                       string routingKey,
                                       bool nowait,
                                       IDictionary<string, object> arguments)
        {
            _Private_QueueBind(queue, exchange, routingKey,
                               nowait, arguments);
        }

        public uint _Private_QueuePurge(string queue,
                                        bool nowait)
        {
            return m_delegate._Private_QueuePurge(queue, nowait);
        }


        public uint _Private_QueueDelete(string queue,
                                         bool ifUnused,
                                         bool ifEmpty,
                                         bool nowait)
        {
            return m_delegate._Private_QueueDelete(queue, ifUnused,
                                                   ifEmpty, nowait);
        }

        public void _Private_BasicPublish(string exchange,
                                          string routingKey,
                                          bool mandatory,
                                          bool immediate,
                                          IBasicProperties basicProperties,
                                          byte[] body)
        {
            m_delegate._Private_BasicPublish(exchange, routingKey, mandatory,
                                             immediate, basicProperties, body);
        }

        public void _Private_BasicConsume(string queue,
                                          string consumerTag,
                                          bool noLocal,
                                          bool noAck,
                                          bool exclusive,
                                          bool nowait,
                                          IDictionary<string, object> arguments)
        {
            m_delegate._Private_BasicConsume(queue,
                                             consumerTag,
                                             noLocal,
                                             noAck,
                                             exclusive,
                                             nowait,
                                             arguments);
        }

        public void _Private_ConfirmSelect(bool nowait)
        {
            m_delegate._Private_ConfirmSelect(nowait);
        }

        public void _Private_BasicCancel(string consumerTag,
                                         bool nowait)
        {
            m_delegate._Private_BasicCancel(consumerTag,
                                            nowait);
        }

        public void _Private_ChannelOpen(string outOfBand)
        {
            m_delegate._Private_ChannelOpen(outOfBand);
        }

        public void _Private_ChannelCloseOk()
        {
            m_delegate._Private_ChannelCloseOk();
        }

        public void _Private_ChannelClose(ushort replyCode,
                                          string replyText,
                                          ushort classId,
                                          ushort methodId)
        {
            m_delegate._Private_ChannelClose(replyCode, replyText,
                                             classId, methodId);
        }

        public void _Private_BasicGet(string queue, bool noAck)
        {
            m_delegate._Private_BasicGet(queue, noAck);
        }

        public void _Private_BasicRecover(bool requeue)
        {
            m_delegate._Private_BasicRecover(requeue);
        }

        public void _Private_ChannelFlowOk(bool active)
        {
            m_delegate._Private_ChannelFlowOk(active);
        }

        public void _Private_ConnectionStartOk(IDictionary<string, object> clientProperties,
                                               string mechanism, byte[] response, string locale)
        {
            m_delegate._Private_ConnectionStartOk(clientProperties, mechanism,
                                                  response, locale);
        }

        public void HandleConnectionSecure(byte[] challenge)
        {
            m_delegate.HandleConnectionSecure(challenge);
        }

        public void _Private_ConnectionSecureOk(byte[] response)
        {
            m_delegate._Private_ConnectionSecureOk(response);
        }

        public void HandleConnectionTune(ushort channelMax,
                                         uint frameMax,
                                         ushort heartbeat)
        {
            m_delegate.HandleConnectionTune(channelMax, frameMax, heartbeat);
        }

        public void ConnectionTuneOk(ushort channelMax,
                                     uint frameMax,
                                     ushort heartbeat)
        {
            m_delegate.ConnectionTuneOk(channelMax, frameMax, heartbeat);
        }

        public void _Private_ConnectionOpen(string virtualHost,
                                            string capabilities,
                                            bool insist)
        {
            m_delegate._Private_ConnectionOpen(virtualHost, capabilities, insist);
        }

        public void HandleConnectionOpenOk(string knownHosts)
        {
            m_delegate.HandleConnectionOpenOk(knownHosts);
        }

        public void _Private_ConnectionClose(ushort replyCode,
                                             string replyText,
                                             ushort classId,
                                             ushort methodId)
        {
            m_delegate._Private_ConnectionClose(replyCode, replyText,
                                                classId, methodId);
        }

        public void _Private_ConnectionCloseOk()
        {
            m_delegate._Private_ConnectionCloseOk();
        }
    }
}