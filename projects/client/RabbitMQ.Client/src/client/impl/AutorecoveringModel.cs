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

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    internal sealed class AutorecoveringModel : IFullModel, IRecoverable
    {
        private readonly object _eventLock = new object();
        private AutorecoveringConnection _connection;
        private RecoveryAwareModel _delegate;

        private EventHandler<BasicAckEventArgs> _recordedBasicAckEventHandlers;
        private EventHandler<BasicNackEventArgs> _recordedBasicNackEventHandlers;
        private EventHandler<BasicReturnEventArgs> _recordedBasicReturnEventHandlers;
        private EventHandler<CallbackExceptionEventArgs> _recordedCallbackExceptionEventHandlers;
        private EventHandler<ShutdownEventArgs> _recordedShutdownEventHandlers;

        private ushort _prefetchCountConsumer = 0;
        private ushort _prefetchCountGlobal = 0;
        private bool _usesPublisherConfirms = false;
        private bool _usesTransactions = false;

        public IConsumerDispatcher ConsumerDispatcher
        {
            get { return _delegate.ConsumerDispatcher; }
        }

        public TimeSpan ContinuationTimeout
        {
            get { return _delegate.ContinuationTimeout; }
            set { _delegate.ContinuationTimeout = value; }
        }

        public AutorecoveringModel(AutorecoveringConnection conn, RecoveryAwareModel _delegate)
        {
            _connection = conn;
            this._delegate = _delegate;
        }

        public event EventHandler<BasicAckEventArgs> BasicAcks
        {
            add
            {
                lock (_eventLock)
                {
                    _recordedBasicAckEventHandlers += value;
                    _delegate.BasicAcks += value;
                }
            }
            remove
            {
                lock (_eventLock)
                {
                    _recordedBasicAckEventHandlers -= value;
                    _delegate.BasicAcks -= value;
                }
            }
        }

        public event EventHandler<BasicNackEventArgs> BasicNacks
        {
            add
            {
                lock (_eventLock)
                {
                    _recordedBasicNackEventHandlers += value;
                    _delegate.BasicNacks += value;
                }
            }
            remove
            {
                lock (_eventLock)
                {
                    _recordedBasicNackEventHandlers -= value;
                    _delegate.BasicNacks -= value;
                }
            }
        }

        public event EventHandler<EventArgs> BasicRecoverOk
        {
            add
            {
                // TODO: record and re-add handlers
                _delegate.BasicRecoverOk += value;
            }
            remove { _delegate.BasicRecoverOk -= value; }
        }

        public event EventHandler<BasicReturnEventArgs> BasicReturn
        {
            add
            {
                lock (_eventLock)
                {
                    _recordedBasicReturnEventHandlers += value;
                    _delegate.BasicReturn += value;
                }
            }
            remove
            {
                lock (_eventLock)
                {
                    _recordedBasicReturnEventHandlers -= value;
                    _delegate.BasicReturn -= value;
                }
            }
        }

        public event EventHandler<CallbackExceptionEventArgs> CallbackException
        {
            add
            {
                lock (_eventLock)
                {
                    _recordedCallbackExceptionEventHandlers += value;
                    _delegate.CallbackException += value;
                }
            }
            remove
            {
                lock (_eventLock)
                {
                    _recordedCallbackExceptionEventHandlers -= value;
                    _delegate.CallbackException -= value;
                }
            }
        }

        public event EventHandler<FlowControlEventArgs> FlowControl
        {
            add
            {
                // TODO: record and re-add handlers
                _delegate.FlowControl += value;
            }
            remove { _delegate.FlowControl -= value; }
        }

        public event EventHandler<ShutdownEventArgs> ModelShutdown
        {
            add
            {
                lock (_eventLock)
                {
                    _recordedShutdownEventHandlers += value;
                    _delegate.ModelShutdown += value;
                }
            }
            remove
            {
                lock (_eventLock)
                {
                    _recordedShutdownEventHandlers -= value;
                    _delegate.ModelShutdown -= value;
                }
            }
        }

        public event EventHandler<EventArgs> Recovery;

        public int ChannelNumber
        {
            get { return _delegate.ChannelNumber; }
        }

        public ShutdownEventArgs CloseReason
        {
            get { return _delegate.CloseReason; }
        }

        public IBasicConsumer DefaultConsumer
        {
            get { return _delegate.DefaultConsumer; }
            set { _delegate.DefaultConsumer = value; }
        }

        public IModel Delegate
        {
            get { return _delegate; }
        }

        public bool IsClosed
        {
            get { return _delegate.IsClosed; }
        }

        public bool IsOpen
        {
            get { return _delegate.IsOpen; }
        }

        public ulong NextPublishSeqNo
        {
            get { return _delegate.NextPublishSeqNo; }
        }

        public void AutomaticallyRecover(AutorecoveringConnection conn)
        {
            _connection = conn;
            RecoveryAwareModel defunctModel = _delegate;

            _delegate = conn.CreateNonRecoveringModel();
            _delegate.InheritOffsetFrom(defunctModel);

            RecoverModelShutdownHandlers();
            RecoverState();

            RecoverBasicReturnHandlers();
            RecoverBasicAckHandlers();
            RecoverBasicNackHandlers();
            RecoverCallbackExceptionHandlers();

            RunRecoveryEventHandlers();
        }

        public void BasicQos(ushort prefetchCount,
            bool global)
        {
            _delegate.BasicQos(0, prefetchCount, global);
        }

        public void Close(ushort replyCode, string replyText, bool abort)
        {
            try
            {
                _delegate.Close(replyCode, replyText, abort);
            }
            finally
            {
                _connection.UnregisterModel(this);
            }
        }

        public void Close(ShutdownEventArgs reason, bool abort)
        {
            try
            {
                _delegate.Close(reason, abort).GetAwaiter().GetResult();;
            }
            finally
            {
                _connection.UnregisterModel(this);
            }
        }

        public bool DispatchAsynchronous(Command cmd)
        {
            return _delegate.DispatchAsynchronous(cmd);
        }

        public void FinishClose()
        {
            _delegate.FinishClose();
        }

        public void HandleCommand(ISession session, Command cmd)
        {
            _delegate.HandleCommand(session, cmd);
        }

        public void OnBasicAck(BasicAckEventArgs args)
        {
            _delegate.OnBasicAck(args);
        }

        public void OnBasicNack(BasicNackEventArgs args)
        {
            _delegate.OnBasicNack(args);
        }

        public void OnBasicRecoverOk(EventArgs args)
        {
            _delegate.OnBasicRecoverOk(args);
        }

        public void OnBasicReturn(BasicReturnEventArgs args)
        {
            _delegate.OnBasicReturn(args);
        }

        public void OnCallbackException(CallbackExceptionEventArgs args)
        {
            _delegate.OnCallbackException(args);
        }

        public void OnFlowControl(FlowControlEventArgs args)
        {
            _delegate.OnFlowControl(args);
        }

        public void OnModelShutdown(ShutdownEventArgs reason)
        {
            _delegate.OnModelShutdown(reason);
        }

        public void OnSessionShutdown(ISession session, ShutdownEventArgs reason)
        {
            _delegate.OnSessionShutdown(session, reason);
        }

        public bool SetCloseReason(ShutdownEventArgs reason)
        {
            return _delegate.SetCloseReason(reason);
        }

        public override string ToString()
        {
            return _delegate.ToString();
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
                Abort();

                _connection = null;
                _delegate = null;
                _recordedBasicAckEventHandlers = null;
                _recordedBasicNackEventHandlers = null;
                _recordedBasicReturnEventHandlers = null;
                _recordedCallbackExceptionEventHandlers = null;
                _recordedShutdownEventHandlers = null;
            }

            // dispose unmanaged resources
        }

        public void ConnectionTuneOk(ushort channelMax,
            uint frameMax,
            ushort heartbeat)
        {
            _delegate.ConnectionTuneOk(channelMax, frameMax, heartbeat);
        }

        public void HandleBasicAck(ulong deliveryTag,
            bool multiple)
        {
            _delegate.HandleBasicAck(deliveryTag, multiple);
        }

        public void HandleBasicCancel(string consumerTag, bool nowait)
        {
            _delegate.HandleBasicCancel(consumerTag, nowait);
        }

        public void HandleBasicCancelOk(string consumerTag)
        {
            _delegate.HandleBasicCancelOk(consumerTag);
        }

        public void HandleBasicConsumeOk(string consumerTag)
        {
            _delegate.HandleBasicConsumeOk(consumerTag);
        }

        public void HandleBasicDeliver(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body)
        {
            _delegate.HandleBasicDeliver(consumerTag, deliveryTag, redelivered, exchange,
                routingKey, basicProperties, body);
        }

        public void HandleBasicGetEmpty() => _delegate.HandleBasicGetEmpty();

        public void HandleBasicGetOk(ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            uint messageCount,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body)
        {
            _delegate.HandleBasicGetOk(deliveryTag, redelivered, exchange, routingKey,
                messageCount, basicProperties, body);
        }

        public void HandleBasicNack(ulong deliveryTag,
            bool multiple,
            bool requeue)
        {
            _delegate.HandleBasicNack(deliveryTag, multiple, requeue);
        }

        public void HandleBasicRecoverOk()
        {
            _delegate.HandleBasicRecoverOk();
        }

        public void HandleBasicReturn(ushort replyCode,
            string replyText,
            string exchange,
            string routingKey,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body)
        {
            _delegate.HandleBasicReturn(replyCode, replyText, exchange,
                routingKey, basicProperties, body);
        }

        public void HandleChannelClose(ushort replyCode,
            string replyText,
            ushort classId,
            ushort methodId)
        {
            _delegate.HandleChannelClose(replyCode, replyText, classId, methodId);
        }

        public void HandleChannelCloseOk()
        {
            _delegate.HandleChannelCloseOk();
        }

        public void HandleChannelFlow(bool active)
        {
            _delegate.HandleChannelFlow(active);
        }

        public void HandleConnectionBlocked(string reason)
        {
            _delegate.HandleConnectionBlocked(reason);
        }

        public void HandleConnectionClose(ushort replyCode,
            string replyText,
            ushort classId,
            ushort methodId)
        {
            _delegate.HandleConnectionClose(replyCode, replyText, classId, methodId);
        }

        public void HandleConnectionOpenOk(string knownHosts)
        {
            _delegate.HandleConnectionOpenOk(knownHosts);
        }

        public void HandleConnectionSecure(byte[] challenge)
        {
            _delegate.HandleConnectionSecure(challenge);
        }

        public void HandleConnectionStart(byte versionMajor,
            byte versionMinor,
            IDictionary<string, object> serverProperties,
            byte[] mechanisms,
            byte[] locales)
        {
            _delegate.HandleConnectionStart(versionMajor, versionMinor, serverProperties,
                mechanisms, locales);
        }

        public void HandleConnectionTune(ushort channelMax,
            uint frameMax,
            ushort heartbeat)
        {
            _delegate.HandleConnectionTune(channelMax, frameMax, heartbeat);
        }

        public void HandleConnectionUnblocked()
        {
            _delegate.HandleConnectionUnblocked();
        }

        public void HandleQueueDeclareOk(string queue,
            uint messageCount,
            uint consumerCount)
        {
            _delegate.HandleQueueDeclareOk(queue, messageCount, consumerCount);
        }

        public void _Private_BasicCancel(string consumerTag,
            bool nowait)
        {
            _delegate._Private_BasicCancel(consumerTag,
                nowait);
        }

        public void _Private_BasicConsume(string queue,
            string consumerTag,
            bool noLocal,
            bool autoAck,
            bool exclusive,
            bool nowait,
            IDictionary<string, object> arguments)
        {
            _delegate._Private_BasicConsume(queue,
                consumerTag,
                noLocal,
                autoAck,
                exclusive,
                nowait,
                arguments);
        }

        public void _Private_BasicGet(string queue, bool autoAck)
        {
            _delegate._Private_BasicGet(queue, autoAck);
        }

        public void _Private_BasicPublish(string exchange,
            string routingKey,
            bool mandatory,
            IBasicProperties basicProperties,
            byte[] body)
        {
            if (routingKey == null)
            {
                throw new ArgumentNullException(nameof(routingKey));
            }

            _delegate._Private_BasicPublish(exchange, routingKey, mandatory,
                basicProperties, body);
        }

        public void _Private_BasicRecover(bool requeue)
        {
            _delegate._Private_BasicRecover(requeue);
        }

        public void _Private_ChannelClose(ushort replyCode,
            string replyText,
            ushort classId,
            ushort methodId)
        {
            _delegate._Private_ChannelClose(replyCode, replyText,
                classId, methodId);
        }

        public void _Private_ChannelCloseOk()
        {
            _delegate._Private_ChannelCloseOk();
        }

        public void _Private_ChannelFlowOk(bool active)
        {
            _delegate._Private_ChannelFlowOk(active);
        }

        public void _Private_ChannelOpen(string outOfBand)
        {
            _delegate._Private_ChannelOpen(outOfBand);
        }

        public void _Private_ConfirmSelect(bool nowait)
        {
            _delegate._Private_ConfirmSelect(nowait);
        }

        public void _Private_ConnectionClose(ushort replyCode,
            string replyText,
            ushort classId,
            ushort methodId)
        {
            _delegate._Private_ConnectionClose(replyCode, replyText,
                classId, methodId);
        }

        public void _Private_ConnectionCloseOk()
        {
            _delegate._Private_ConnectionCloseOk();
        }

        public void _Private_ConnectionOpen(string virtualHost,
            string capabilities,
            bool insist)
        {
            _delegate._Private_ConnectionOpen(virtualHost, capabilities, insist);
        }

        public void _Private_ConnectionSecureOk(byte[] response)
        {
            _delegate._Private_ConnectionSecureOk(response);
        }

        public void _Private_ConnectionStartOk(IDictionary<string, object> clientProperties,
            string mechanism, byte[] response, string locale)
        {
            _delegate._Private_ConnectionStartOk(clientProperties, mechanism,
                response, locale);
        }

        public void _Private_UpdateSecret(byte[] newSecret, string reason)
        {
            _delegate._Private_UpdateSecret(newSecret, reason);
        }

        public void _Private_ExchangeBind(string destination,
            string source,
            string routingKey,
            bool nowait,
            IDictionary<string, object> arguments)
        {
            _delegate._Private_ExchangeBind(destination, source, routingKey,
                nowait, arguments);
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
            _delegate._Private_ExchangeDeclare(exchange, type, passive,
                durable, autoDelete, @internal,
                nowait, arguments);
        }

        public void _Private_ExchangeDelete(string exchange,
            bool ifUnused,
            bool nowait)
        {
            _delegate._Private_ExchangeDelete(exchange, ifUnused, nowait);
        }

        public void _Private_ExchangeUnbind(string destination,
            string source,
            string routingKey,
            bool nowait,
            IDictionary<string, object> arguments)
        {
            _delegate._Private_ExchangeUnbind(destination, source, routingKey,
                nowait, arguments);
        }

        public void _Private_QueueBind(string queue,
            string exchange,
            string routingKey,
            bool nowait,
            IDictionary<string, object> arguments)
        {
            _delegate._Private_QueueBind(queue, exchange, routingKey,
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
            _delegate._Private_QueueDeclare(queue, passive,
                durable, exclusive, autoDelete,
                nowait, arguments);
        }

        public uint _Private_QueueDelete(string queue,
            bool ifUnused,
            bool ifEmpty,
            bool nowait)
        {
            return _delegate._Private_QueueDelete(queue, ifUnused,
                ifEmpty, nowait);
        }

        public uint _Private_QueuePurge(string queue,
            bool nowait)
        {
            return _delegate._Private_QueuePurge(queue, nowait);
        }

        public void Abort()
        {
            try
            {
                _delegate.Abort();
            }
            finally
            {
                _connection.UnregisterModel(this);
            }
        }

        public void Abort(ushort replyCode, string replyText)
        {
            try
            {
                _delegate.Abort(replyCode, replyText);
            }
            finally
            {
                _connection.UnregisterModel(this);
            }
        }

        public void BasicAck(ulong deliveryTag,
            bool multiple)
        {
            _delegate.BasicAck(deliveryTag, multiple);
        }

        public void BasicCancel(string consumerTag)
        {
            RecordedConsumer cons = _connection.DeleteRecordedConsumer(consumerTag);
            if (cons != null)
            {
                _connection.MaybeDeleteRecordedAutoDeleteQueue(cons.Queue);
            }
            _delegate.BasicCancel(consumerTag);
        }

        public void BasicCancelNoWait(string consumerTag)
        {
            RecordedConsumer cons = _connection.DeleteRecordedConsumer(consumerTag);
            if (cons != null)
            {
                _connection.MaybeDeleteRecordedAutoDeleteQueue(cons.Queue);
            }
            _delegate.BasicCancelNoWait(consumerTag);
        }

        public string BasicConsume(
            string queue,
            bool autoAck,
            string consumerTag,
            bool noLocal,
            bool exclusive,
            IDictionary<string, object> arguments,
            IBasicConsumer consumer)
        {
            string result = _delegate.BasicConsume(queue, autoAck, consumerTag, noLocal,
                exclusive, arguments, consumer);
            RecordedConsumer rc = new RecordedConsumer(this, queue).
                WithConsumerTag(result).
                WithConsumer(consumer).
                WithExclusive(exclusive).
                WithAutoAck(autoAck).
                WithArguments(arguments);
            _connection.RecordConsumer(result, rc);
            return result;
        }

        public BasicGetResult BasicGet(string queue,
            bool autoAck)
        {
            return _delegate.BasicGet(queue, autoAck);
        }

        public void BasicNack(ulong deliveryTag,
            bool multiple,
            bool requeue)
        {
            _delegate.BasicNack(deliveryTag, multiple, requeue);
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

            _delegate.BasicPublish(exchange,
                routingKey,
                mandatory,
                basicProperties,
                body);
        }

        public void BasicQos(uint prefetchSize,
            ushort prefetchCount,
            bool global)
        {
            if (global)
            {
                _prefetchCountGlobal = prefetchCount;
            }
            else
            {
                _prefetchCountConsumer = prefetchCount;
            }
            _delegate.BasicQos(prefetchSize, prefetchCount, global);
        }

        public void BasicRecover(bool requeue)
        {
            _delegate.BasicRecover(requeue);
        }

        public void BasicRecoverAsync(bool requeue)
        {
            _delegate.BasicRecoverAsync(requeue);
        }

        public void BasicReject(ulong deliveryTag,
            bool requeue)
        {
            _delegate.BasicReject(deliveryTag, requeue);
        }

        public void Close()
        {
            try
            {
                _delegate.Close();
            }
            finally
            {
                _connection.UnregisterModel(this);
            }
        }

        public void Close(ushort replyCode, string replyText)
        {
            try
            {
                _delegate.Close(replyCode, replyText);
            }
            finally
            {
                _connection.UnregisterModel(this);
            }
        }

        public void ConfirmSelect()
        {
            _usesPublisherConfirms = true;
            _delegate.ConfirmSelect();
        }

        public IBasicProperties CreateBasicProperties()
        {
            return _delegate.CreateBasicProperties();
        }

        public void ExchangeBind(string destination,
            string source,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            RecordedBinding eb = new RecordedExchangeBinding(this).
                WithSource(source).
                WithDestination(destination).
                WithRoutingKey(routingKey).
                WithArguments(arguments);
            _connection.RecordBinding(eb);
            _delegate.ExchangeBind(destination, source, routingKey, arguments);
        }

        public void ExchangeBindNoWait(string destination,
            string source,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            _delegate.ExchangeBindNoWait(destination, source, routingKey, arguments);
        }

        public void ExchangeDeclare(string exchange, string type, bool durable,
            bool autoDelete, IDictionary<string, object> arguments)
        {
            RecordedExchange rx = new RecordedExchange(this, exchange).
                WithType(type).
                WithDurable(durable).
                WithAutoDelete(autoDelete).
                WithArguments(arguments);
            _delegate.ExchangeDeclare(exchange, type, durable,
                autoDelete, arguments);
            _connection.RecordExchange(exchange, rx);
        }

        public void ExchangeDeclareNoWait(string exchange,
            string type,
            bool durable,
            bool autoDelete,
            IDictionary<string, object> arguments)
        {
            RecordedExchange rx = new RecordedExchange(this, exchange).
                WithType(type).
                WithDurable(durable).
                WithAutoDelete(autoDelete).
                WithArguments(arguments);
            _delegate.ExchangeDeclareNoWait(exchange, type, durable,
                autoDelete, arguments);
            _connection.RecordExchange(exchange, rx);
        }

        public void ExchangeDeclarePassive(string exchange)
        {
            _delegate.ExchangeDeclarePassive(exchange);
        }

        public void ExchangeDelete(string exchange,
            bool ifUnused)
        {
            _delegate.ExchangeDelete(exchange, ifUnused);
            _connection.DeleteRecordedExchange(exchange);
        }

        public void ExchangeDeleteNoWait(string exchange,
            bool ifUnused)
        {
            _delegate.ExchangeDeleteNoWait(exchange, ifUnused);
            _connection.DeleteRecordedExchange(exchange);
        }

        public void ExchangeUnbind(string destination,
            string source,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            RecordedBinding eb = new RecordedExchangeBinding(this).
                WithSource(source).
                WithDestination(destination).
                WithRoutingKey(routingKey).
                WithArguments(arguments);
            _connection.DeleteRecordedBinding(eb);
            _delegate.ExchangeUnbind(destination, source, routingKey, arguments);
            _connection.MaybeDeleteRecordedAutoDeleteExchange(source);
        }

        public void ExchangeUnbindNoWait(string destination,
            string source,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            _delegate.ExchangeUnbind(destination, source, routingKey, arguments);
        }

        public void QueueBind(string queue,
            string exchange,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            RecordedBinding qb = new RecordedQueueBinding(this).
                WithSource(exchange).
                WithDestination(queue).
                WithRoutingKey(routingKey).
                WithArguments(arguments);
            _connection.RecordBinding(qb);
            _delegate.QueueBind(queue, exchange, routingKey, arguments);
        }

        public void QueueBindNoWait(string queue,
            string exchange,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            _delegate.QueueBind(queue, exchange, routingKey, arguments);
        }

        public QueueDeclareOk QueueDeclare(string queue, bool durable,
                                           bool exclusive, bool autoDelete,
                                           IDictionary<string, object> arguments)
        {
            QueueDeclareOk result = _delegate.QueueDeclare(queue, durable, exclusive,
                autoDelete, arguments);
            RecordedQueue rq = new RecordedQueue(this, result.QueueName).
                Durable(durable).
                Exclusive(exclusive).
                AutoDelete(autoDelete).
                Arguments(arguments).
                ServerNamed(string.Empty.Equals(queue));
            _connection.RecordQueue(result.QueueName, rq);
            return result;
        }

        public void QueueDeclareNoWait(string queue, bool durable,
                                       bool exclusive, bool autoDelete,
                                       IDictionary<string, object> arguments)
        {
            _delegate.QueueDeclareNoWait(queue, durable, exclusive,
                autoDelete, arguments);
            RecordedQueue rq = new RecordedQueue(this, queue).
                Durable(durable).
                Exclusive(exclusive).
                AutoDelete(autoDelete).
                Arguments(arguments).
                ServerNamed(string.Empty.Equals(queue));
            _connection.RecordQueue(queue, rq);
        }

        public QueueDeclareOk QueueDeclarePassive(string queue)
        {
            return _delegate.QueueDeclarePassive(queue);
        }

        public uint MessageCount(string queue)
        {
            return _delegate.MessageCount(queue);
        }

        public uint ConsumerCount(string queue)
        {
            return _delegate.ConsumerCount(queue);
        }

        public uint QueueDelete(string queue,
            bool ifUnused,
            bool ifEmpty)
        {
            uint result = _delegate.QueueDelete(queue, ifUnused, ifEmpty);
            _connection.DeleteRecordedQueue(queue);
            return result;
        }

        public void QueueDeleteNoWait(string queue,
            bool ifUnused,
            bool ifEmpty)
        {
            _delegate.QueueDeleteNoWait(queue, ifUnused, ifEmpty);
            _connection.DeleteRecordedQueue(queue);
        }

        public uint QueuePurge(string queue)
        {
            return _delegate.QueuePurge(queue);
        }

        public void QueueUnbind(string queue,
            string exchange,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            RecordedBinding qb = new RecordedQueueBinding(this).
                WithSource(exchange).
                WithDestination(queue).
                WithRoutingKey(routingKey).
                WithArguments(arguments);
            _connection.DeleteRecordedBinding(qb);
            _delegate.QueueUnbind(queue, exchange, routingKey, arguments);
            _connection.MaybeDeleteRecordedAutoDeleteExchange(exchange);
        }

        public void TxCommit()
        {
            _delegate.TxCommit();
        }

        public void TxRollback()
        {
            _delegate.TxRollback();
        }

        public void TxSelect()
        {
            _usesTransactions = true;
            _delegate.TxSelect();
        }

        public bool WaitForConfirms(TimeSpan timeout, out bool timedOut)
        {
            return _delegate.WaitForConfirms(timeout, out timedOut);
        }

        public bool WaitForConfirms(TimeSpan timeout)
        {
            return _delegate.WaitForConfirms(timeout);
        }

        public bool WaitForConfirms()
        {
            return _delegate.WaitForConfirms();
        }

        public void WaitForConfirmsOrDie()
        {
            _delegate.WaitForConfirmsOrDie();
        }

        public void WaitForConfirmsOrDie(TimeSpan timeout)
        {
            _delegate.WaitForConfirmsOrDie(timeout);
        }

        private void RecoverBasicAckHandlers()
        {
            lock (_eventLock)
            {
                _delegate.BasicAcks += _recordedBasicAckEventHandlers;
            }
        }

        private void RecoverBasicNackHandlers()
        {
            lock (_eventLock)
            {
                _delegate.BasicNacks += _recordedBasicNackEventHandlers;
            }
        }

        private void RecoverBasicReturnHandlers()
        {
            lock (_eventLock)
            {
                _delegate.BasicReturn += _recordedBasicReturnEventHandlers;
            }
        }

        private void RecoverCallbackExceptionHandlers()
        {
            lock (_eventLock)
            {
                _delegate.CallbackException += _recordedCallbackExceptionEventHandlers;
            }
        }

        private void RecoverModelShutdownHandlers()
        {
            lock (_eventLock)
            {
                _delegate.ModelShutdown += _recordedShutdownEventHandlers;
            }
        }

        private void RecoverState()
        {
            if (_prefetchCountConsumer != 0)
            {
                BasicQos(_prefetchCountConsumer, false);
            }

            if (_prefetchCountGlobal != 0)
            {
                BasicQos(_prefetchCountGlobal, true);
            }

            if (_usesPublisherConfirms)
            {
                ConfirmSelect();
            }

            if (_usesTransactions)
            {
                TxSelect();
            }
        }

        private void RunRecoveryEventHandlers()
        {
            foreach (EventHandler<EventArgs> reh in Recovery?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try
                {
                    reh(this, EventArgs.Empty);
                }
                catch (Exception e)
                {
                    var args = new CallbackExceptionEventArgs(e);
                    args.Detail["context"] = "OnModelRecovery";
                    _delegate.OnCallbackException(args);
                }
            }
        }

        public IBasicPublishBatch CreateBasicPublishBatch()
        {
            return ((IFullModel)_delegate).CreateBasicPublishBatch();
        }
    }
}
