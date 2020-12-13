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
using System.Runtime.CompilerServices;
using System.Xml.Schema;

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    internal sealed class AutorecoveringModel : IFullModel, IRecoverable
    {
        private bool _disposed = false;
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

        public IConsumerDispatcher ConsumerDispatcher => !_disposed ? _delegate.ConsumerDispatcher : throw new ObjectDisposedException(GetType().FullName);

        public TimeSpan ContinuationTimeout
        {
            get => Delegate.ContinuationTimeout;
            set => Delegate.ContinuationTimeout = value;
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
                ThrowIfDisposed();
                lock (_eventLock)
                {
                    _recordedBasicAckEventHandlers += value;
                    _delegate.BasicAcks += value;
                }
            }
            remove
            {
                ThrowIfDisposed();
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
                ThrowIfDisposed();
                lock (_eventLock)
                {
                    _recordedBasicNackEventHandlers += value;
                    _delegate.BasicNacks += value;
                }
            }
            remove
            {
                ThrowIfDisposed();
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
                Delegate.BasicRecoverOk += value;
            }
            remove
            {
                Delegate.BasicRecoverOk -= value;
            }
        }

        public event EventHandler<BasicReturnEventArgs> BasicReturn
        {
            add
            {
                ThrowIfDisposed();
                lock (_eventLock)
                {
                    _recordedBasicReturnEventHandlers += value;
                    _delegate.BasicReturn += value;
                }
            }
            remove
            {
                ThrowIfDisposed();
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
                ThrowIfDisposed();
                lock (_eventLock)
                {
                    _recordedCallbackExceptionEventHandlers += value;
                    _delegate.CallbackException += value;
                }
            }
            remove
            {
                ThrowIfDisposed();
                lock (_eventLock)
                {
                    _recordedCallbackExceptionEventHandlers -= value;
                    _delegate.CallbackException -= value;
                }
            }
        }

        public event EventHandler<FlowControlEventArgs> FlowControl
        {
            add { Delegate.FlowControl += value; }
            remove { Delegate.FlowControl -= value; }
        }

        public event EventHandler<ShutdownEventArgs> ModelShutdown
        {
            add
            {
                ThrowIfDisposed();
                lock (_eventLock)
                {
                    _recordedShutdownEventHandlers += value;
                    _delegate.ModelShutdown += value;
                }
            }
            remove
            {
                ThrowIfDisposed();
                lock (_eventLock)
                {
                    _recordedShutdownEventHandlers -= value;
                    _delegate.ModelShutdown -= value;
                }
            }
        }

        public event EventHandler<EventArgs> Recovery
        {
            add { RecoveryAwareDelegate.Recovery += value; }
            remove { RecoveryAwareDelegate.Recovery -= value; }
        }

        public int ChannelNumber => Delegate.ChannelNumber;

        public ShutdownEventArgs CloseReason => Delegate.CloseReason;

        public IBasicConsumer DefaultConsumer
        {
            get => Delegate.DefaultConsumer;
            set => Delegate.DefaultConsumer = value;
        }

        public IModel Delegate => RecoveryAwareDelegate;
        private RecoveryAwareModel RecoveryAwareDelegate => !_disposed ? _delegate : throw new ObjectDisposedException(GetType().FullName);

        public bool IsClosed => _delegate is object && _delegate.IsClosed;

        public bool IsOpen => _delegate is object && _delegate.IsOpen;

        public ulong NextPublishSeqNo => Delegate.NextPublishSeqNo;

        public void AutomaticallyRecover(AutorecoveringConnection conn)
        {
            ThrowIfDisposed();
            _connection = conn;
            RecoveryAwareModel defunctModel = _delegate;

            _delegate = conn.CreateNonRecoveringModel();
            _delegate.TakeOver(defunctModel);

            RecoverModelShutdownHandlers();
            RecoverState();

            RecoverBasicReturnHandlers();
            RecoverBasicAckHandlers();
            RecoverBasicNackHandlers();
            RecoverCallbackExceptionHandlers();

            RunRecoveryEventHandlers();
        }

        public void BasicQos(ushort prefetchCount,
            bool global) => Delegate.BasicQos(0, prefetchCount, global);

        public void Close(ushort replyCode, string replyText, bool abort)
        {
            ThrowIfDisposed();
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
            ThrowIfDisposed();
            try
            {
                _delegate.Close(reason, abort).GetAwaiter().GetResult();;
            }
            finally
            {
                _connection.UnregisterModel(this);
            }
        }

        public override string ToString() => Delegate.ToString();

        void IDisposable.Dispose() => Dispose(true);

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                Abort();

                _connection = null;
                _delegate = null;
                _recordedBasicAckEventHandlers = null;
                _recordedBasicNackEventHandlers = null;
                _recordedBasicReturnEventHandlers = null;
                _recordedCallbackExceptionEventHandlers = null;
                _recordedShutdownEventHandlers = null;

                _disposed = true;
            }
        }

        public void ConnectionTuneOk(ushort channelMax,
            uint frameMax,
            ushort heartbeat)
        {
            ThrowIfDisposed();
            _delegate.ConnectionTuneOk(channelMax, frameMax, heartbeat);
        }

        public void HandleBasicAck(ulong deliveryTag, bool multiple)
        {
            ThrowIfDisposed();
            _delegate.HandleBasicAck(deliveryTag, multiple);
        }

        public void HandleBasicCancel(string consumerTag, bool nowait)
        {
            ThrowIfDisposed();
            _delegate.HandleBasicCancel(consumerTag, nowait);
        }

        public void HandleBasicCancelOk(string consumerTag)
        {
            ThrowIfDisposed();
            _delegate.HandleBasicCancelOk(consumerTag);
        }

        public void HandleBasicConsumeOk(string consumerTag)
        {
            ThrowIfDisposed();
            _delegate.HandleBasicConsumeOk(consumerTag);
        }

        public void HandleBasicDeliver(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body,
            byte[] rentedArray)
        {
            ThrowIfDisposed();
            _delegate.HandleBasicDeliver(consumerTag, deliveryTag, redelivered, exchange, routingKey, basicProperties, body, rentedArray);
        }

        public void HandleBasicGetEmpty()
        {
            ThrowIfDisposed();
            _delegate.HandleBasicGetEmpty();
        }

        public void HandleBasicGetOk(ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            uint messageCount,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body,
            byte[] rentedArray)
        {
            ThrowIfDisposed();
            _delegate.HandleBasicGetOk(deliveryTag, redelivered, exchange, routingKey, messageCount, basicProperties, body, rentedArray);
        }

        public void HandleBasicNack(ulong deliveryTag,
            bool multiple,
            bool requeue)
        {
            ThrowIfDisposed();
            _delegate.HandleBasicNack(deliveryTag, multiple, requeue);
        }

        public void HandleBasicRecoverOk()
        {
            ThrowIfDisposed();
            _delegate.HandleBasicRecoverOk();
        }

        public void HandleBasicReturn(ushort replyCode,
            string replyText,
            string exchange,
            string routingKey,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body,
            byte[] rentedArray)
        {
            ThrowIfDisposed();
            _delegate.HandleBasicReturn(replyCode, replyText, exchange, routingKey, basicProperties, body, rentedArray);
        }

        public void HandleChannelClose(ushort replyCode,
            string replyText,
            ushort classId,
            ushort methodId)
        {
            ThrowIfDisposed();
            _delegate.HandleChannelClose(replyCode, replyText, classId, methodId);
        }

        public void HandleChannelCloseOk()
        {
            ThrowIfDisposed();
            _delegate.HandleChannelCloseOk();
        }

        public void HandleChannelFlow(bool active)
        {
            ThrowIfDisposed();
            _delegate.HandleChannelFlow(active);
        }

        public void HandleConnectionBlocked(string reason)
        {
            ThrowIfDisposed();
            _delegate.HandleConnectionBlocked(reason);
        }

        public void HandleConnectionClose(ushort replyCode,
            string replyText,
            ushort classId,
            ushort methodId)
        {
            ThrowIfDisposed();
            _delegate.HandleConnectionClose(replyCode, replyText, classId, methodId);
        }

        public void HandleConnectionOpenOk(string knownHosts)
        {
            ThrowIfDisposed();
            _delegate.HandleConnectionOpenOk(knownHosts);
        }

        public void HandleConnectionSecure(byte[] challenge)
        {
            ThrowIfDisposed();
            _delegate.HandleConnectionSecure(challenge);
        }

        public void HandleConnectionStart(byte versionMajor,
            byte versionMinor,
            IDictionary<string, object> serverProperties,
            byte[] mechanisms,
            byte[] locales)
        {
            ThrowIfDisposed();
            _delegate.HandleConnectionStart(versionMajor, versionMinor, serverProperties,
                mechanisms, locales);
        }

        public void HandleConnectionTune(ushort channelMax,
            uint frameMax,
            ushort heartbeat)
        {
            ThrowIfDisposed();
            _delegate.HandleConnectionTune(channelMax, frameMax, heartbeat);
        }

        public void HandleConnectionUnblocked()
        {
            ThrowIfDisposed();
            _delegate.HandleConnectionUnblocked();
        }

        public void HandleQueueDeclareOk(string queue,
            uint messageCount,
            uint consumerCount)
        {
            ThrowIfDisposed();
            _delegate.HandleQueueDeclareOk(queue, messageCount, consumerCount);
        }

        public void _Private_BasicCancel(string consumerTag,
            bool nowait)
        {
            ThrowIfDisposed();
            _delegate._Private_BasicCancel(consumerTag, nowait);
        }

        public void _Private_BasicConsume(string queue,
            string consumerTag,
            bool noLocal,
            bool autoAck,
            bool exclusive,
            bool nowait,
            IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
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
            ThrowIfDisposed();
            _delegate._Private_BasicGet(queue, autoAck);
        }

        public void _Private_BasicPublish(string exchange,
            string routingKey,
            bool mandatory,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body)
        {
            if (routingKey is null)
            {
                throw new ArgumentNullException(nameof(routingKey));
            }

            ThrowIfDisposed();
            _delegate._Private_BasicPublish(exchange, routingKey, mandatory,
                basicProperties, body);
        }

        public void _Private_BasicRecover(bool requeue)
        {
            ThrowIfDisposed();
            _delegate._Private_BasicRecover(requeue);
        }

        public void _Private_ChannelClose(ushort replyCode,
            string replyText,
            ushort classId,
            ushort methodId)
        {
            ThrowIfDisposed();
            _delegate._Private_ChannelClose(replyCode, replyText,
                classId, methodId);
        }

        public void _Private_ChannelCloseOk()
        {
            ThrowIfDisposed();
            _delegate._Private_ChannelCloseOk();
        }

        public void _Private_ChannelFlowOk(bool active)
        {
            ThrowIfDisposed();
            _delegate._Private_ChannelFlowOk(active);
        }

        public void _Private_ChannelOpen(string outOfBand)
        {
            ThrowIfDisposed();
            _delegate._Private_ChannelOpen(outOfBand);
        }

        public void _Private_ConfirmSelect(bool nowait)
        {
            ThrowIfDisposed();
            _delegate._Private_ConfirmSelect(nowait);
        }

        public void _Private_ConnectionClose(ushort replyCode,
            string replyText,
            ushort classId,
            ushort methodId)
        {
            ThrowIfDisposed();
            _delegate._Private_ConnectionClose(replyCode, replyText,
                classId, methodId);
        }

        public void _Private_ConnectionCloseOk()
        {
            ThrowIfDisposed();
            _delegate._Private_ConnectionCloseOk();
        }

        public void _Private_ConnectionOpen(string virtualHost,
            string capabilities,
            bool insist)
        {
            ThrowIfDisposed();
            _delegate._Private_ConnectionOpen(virtualHost, capabilities, insist);
        }

        public void _Private_ConnectionSecureOk(byte[] response)
        {
            ThrowIfDisposed();
            _delegate._Private_ConnectionSecureOk(response);
        }

        public void _Private_ConnectionStartOk(IDictionary<string, object> clientProperties,
            string mechanism, byte[] response, string locale)
        {
            ThrowIfDisposed();
            _delegate._Private_ConnectionStartOk(clientProperties, mechanism,
                response, locale);
        }

        public void _Private_UpdateSecret(byte[] newSecret, string reason)
        {
            ThrowIfDisposed();
            _delegate._Private_UpdateSecret(newSecret, reason);
        }

        public void _Private_ExchangeBind(string destination,
            string source,
            string routingKey,
            bool nowait,
            IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
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
            ThrowIfDisposed();
            _delegate._Private_ExchangeDeclare(exchange, type, passive,
                durable, autoDelete, @internal,
                nowait, arguments);
        }

        public void _Private_ExchangeDelete(string exchange,
            bool ifUnused,
            bool nowait)
        {
            ThrowIfDisposed();
            _delegate._Private_ExchangeDelete(exchange, ifUnused, nowait);
        }

        public void _Private_ExchangeUnbind(string destination,
            string source,
            string routingKey,
            bool nowait,
            IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            _delegate._Private_ExchangeUnbind(destination, source, routingKey,
                nowait, arguments);
        }

        public void _Private_QueueBind(string queue,
            string exchange,
            string routingKey,
            bool nowait,
            IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            _delegate._Private_QueueBind(queue, exchange, routingKey,
                nowait, arguments);
        }

        public void _Private_QueueDeclare(string queue,
            bool passive,
            bool durable,
            bool exclusive,
            bool autoDelete,
            bool nowait,
            IDictionary<string, object> arguments) => RecoveryAwareDelegate._Private_QueueDeclare(queue, passive,
                durable, exclusive, autoDelete,
                nowait, arguments);

        public uint _Private_QueueDelete(string queue, bool ifUnused, bool ifEmpty, bool nowait) => RecoveryAwareDelegate._Private_QueueDelete(queue, ifUnused, ifEmpty, nowait);

        public uint _Private_QueuePurge(string queue, bool nowait) => RecoveryAwareDelegate._Private_QueuePurge(queue, nowait);

        public void Abort()
        {
            ThrowIfDisposed();
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
            ThrowIfDisposed();
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
            bool multiple) => Delegate.BasicAck(deliveryTag, multiple);

        public void BasicCancel(string consumerTag)
        {
            ThrowIfDisposed();
            RecordedConsumer cons = _connection.DeleteRecordedConsumer(consumerTag);
            if (cons != null)
            {
                _connection.MaybeDeleteRecordedAutoDeleteQueue(cons.Queue);
            }
            _delegate.BasicCancel(consumerTag);
        }

        public void BasicCancelNoWait(string consumerTag)
        {
            ThrowIfDisposed();
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
            string result = Delegate.BasicConsume(queue, autoAck, consumerTag, noLocal,
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
            bool autoAck) => Delegate.BasicGet(queue, autoAck);

        public void BasicNack(ulong deliveryTag,
            bool multiple,
            bool requeue) => Delegate.BasicNack(deliveryTag, multiple, requeue);

        public void BasicPublish(string exchange,
            string routingKey,
            bool mandatory,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body)
        {
            if (routingKey is null)
            {
                throw new ArgumentNullException(nameof(routingKey));
            }

            Delegate.BasicPublish(exchange,
                routingKey,
                mandatory,
                basicProperties,
                body);
        }

        public void BasicQos(uint prefetchSize,
            ushort prefetchCount,
            bool global)
        {
            ThrowIfDisposed();
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

        public void BasicRecover(bool requeue) => Delegate.BasicRecover(requeue);

        public void BasicRecoverAsync(bool requeue) => Delegate.BasicRecoverAsync(requeue);

        public void BasicReject(ulong deliveryTag,
            bool requeue) => Delegate.BasicReject(deliveryTag, requeue);

        public void Close()
        {
            ThrowIfDisposed();
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
            ThrowIfDisposed();
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
            Delegate.ConfirmSelect();
            _usesPublisherConfirms = true;
        }

        public IBasicProperties CreateBasicProperties() => Delegate.CreateBasicProperties();

        public void ExchangeBind(string destination,
            string source,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
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
            IDictionary<string, object> arguments) => Delegate.ExchangeBindNoWait(destination, source, routingKey, arguments);

        public void ExchangeDeclare(string exchange, string type, bool durable,
            bool autoDelete, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
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
            ThrowIfDisposed();
            RecordedExchange rx = new RecordedExchange(this, exchange).
                WithType(type).
                WithDurable(durable).
                WithAutoDelete(autoDelete).
                WithArguments(arguments);
            _delegate.ExchangeDeclareNoWait(exchange, type, durable,
                autoDelete, arguments);
            _connection.RecordExchange(exchange, rx);
        }

        public void ExchangeDeclarePassive(string exchange) => Delegate.ExchangeDeclarePassive(exchange);

        public void ExchangeDelete(string exchange,
            bool ifUnused)
        {
            Delegate.ExchangeDelete(exchange, ifUnused);
            _connection.DeleteRecordedExchange(exchange);
        }

        public void ExchangeDeleteNoWait(string exchange,
            bool ifUnused)
        {
            Delegate.ExchangeDeleteNoWait(exchange, ifUnused);
            _connection.DeleteRecordedExchange(exchange);
        }

        public void ExchangeUnbind(string destination,
            string source,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
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
            IDictionary<string, object> arguments) => Delegate.ExchangeUnbind(destination, source, routingKey, arguments);

        public void QueueBind(string queue,
            string exchange,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
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
            IDictionary<string, object> arguments) => Delegate.QueueBind(queue, exchange, routingKey, arguments);

        public QueueDeclareOk QueueDeclare(string queue, bool durable,
                                           bool exclusive, bool autoDelete,
                                           IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
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
            ThrowIfDisposed();
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

        public QueueDeclareOk QueueDeclarePassive(string queue) => Delegate.QueueDeclarePassive(queue);

        public uint MessageCount(string queue) => Delegate.MessageCount(queue);

        public uint ConsumerCount(string queue) => Delegate.ConsumerCount(queue);

        public uint QueueDelete(string queue,
            bool ifUnused,
            bool ifEmpty)
        {
            ThrowIfDisposed();
            uint result = _delegate.QueueDelete(queue, ifUnused, ifEmpty);
            _connection.DeleteRecordedQueue(queue);
            return result;
        }

        public void QueueDeleteNoWait(string queue,
            bool ifUnused,
            bool ifEmpty)
        {
            Delegate.QueueDeleteNoWait(queue, ifUnused, ifEmpty);
            _connection.DeleteRecordedQueue(queue);
        }

        public uint QueuePurge(string queue) => Delegate.QueuePurge(queue);

        public void QueueUnbind(string queue,
            string exchange,
            string routingKey,
            IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            RecordedBinding qb = new RecordedQueueBinding(this).
                WithSource(exchange).
                WithDestination(queue).
                WithRoutingKey(routingKey).
                WithArguments(arguments);
            _connection.DeleteRecordedBinding(qb);
            _delegate.QueueUnbind(queue, exchange, routingKey, arguments);
            _connection.MaybeDeleteRecordedAutoDeleteExchange(exchange);
        }

        public void TxCommit() => Delegate.TxCommit();

        public void TxRollback() => Delegate.TxRollback();

        public void TxSelect()
        {
            Delegate.TxSelect();
            _usesTransactions = true;
        }

        public bool WaitForConfirms(TimeSpan timeout, out bool timedOut) => Delegate.WaitForConfirms(timeout, out timedOut);

        public bool WaitForConfirms(TimeSpan timeout) => Delegate.WaitForConfirms(timeout);

        public bool WaitForConfirms() => Delegate.WaitForConfirms();

        public void WaitForConfirmsOrDie() => Delegate.WaitForConfirmsOrDie();

        public void WaitForConfirmsOrDie(TimeSpan timeout) => Delegate.WaitForConfirmsOrDie(timeout);

        private void RecoverBasicAckHandlers()
        {
            ThrowIfDisposed();
            lock (_eventLock)
            {
                _delegate.BasicAcks += _recordedBasicAckEventHandlers;
            }
        }

        private void RecoverBasicNackHandlers()
        {
            ThrowIfDisposed();
            lock (_eventLock)
            {
                _delegate.BasicNacks += _recordedBasicNackEventHandlers;
            }
        }

        private void RecoverBasicReturnHandlers()
        {
            ThrowIfDisposed();
            lock (_eventLock)
            {
                _delegate.BasicReturn += _recordedBasicReturnEventHandlers;
            }
        }

        private void RecoverCallbackExceptionHandlers()
        {
            ThrowIfDisposed();
            lock (_eventLock)
            {
                _delegate.CallbackException += _recordedCallbackExceptionEventHandlers;
            }
        }

        private void RecoverModelShutdownHandlers()
        {
            ThrowIfDisposed();
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
            ThrowIfDisposed();
            _delegate.RunRecoveryEventHandlers(this);
        }

        public IBasicPublishBatch CreateBasicPublishBatch() => Delegate.CreateBasicPublishBatch();

        public IBasicPublishBatch CreateBasicPublishBatch(int sizeHint) => Delegate.CreateBasicPublishBatch(sizeHint);

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
