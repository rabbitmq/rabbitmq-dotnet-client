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
using System.Threading.Tasks;

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
        private AsyncEventHandler<ShutdownEventArgs> _recordedShutdownEventHandlers;

        private ushort _prefetchCountConsumer = 0;
        private ushort _prefetchCountGlobal = 0;
        private bool _usesPublisherConfirms = false;
        private bool _usesTransactions = false;

        public TimeSpan ContinuationTimeout
        {
            get
            {
                ThrowIfDisposed();
                return _delegate.ContinuationTimeout;
            }

            set
            {
                ThrowIfDisposed();
                _delegate.ContinuationTimeout = value;
            }
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
                ThrowIfDisposed();
                // TODO: record and re-add handlers
                _delegate.BasicRecoverOk += value;
            }
            remove
            {
                ThrowIfDisposed();
                _delegate.BasicRecoverOk -= value;
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
            add
            {
                ThrowIfDisposed();
                // TODO: record and re-add handlers
                _delegate.FlowControl += value;
            }
            remove
            {
                ThrowIfDisposed();
                _delegate.FlowControl -= value;
            }
        }

        public event AsyncEventHandler<ShutdownEventArgs> ModelShutdown
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

        public event EventHandler<EventArgs> Recovery;

        public int ChannelNumber
        {
            get
            {
                ThrowIfDisposed();
                return _delegate.ChannelNumber;
            }
        }

        public ShutdownEventArgs CloseReason
        {
            get
            {
                ThrowIfDisposed();
                return _delegate.CloseReason;
            }
        }

        public IAsyncBasicConsumer DefaultConsumer
        {
            get
            {
                ThrowIfDisposed();
                return _delegate.DefaultConsumer;
            }
            set
            {
                ThrowIfDisposed();
                _delegate.DefaultConsumer = value;
            }
        }

        public IModel Delegate
        {
            get
            {
                ThrowIfDisposed();
                return _delegate;
            }
        }

        public bool IsClosed => _delegate != null && _delegate.IsClosed;

        public bool IsOpen => _delegate != null && _delegate.IsOpen;

        public ulong NextPublishSeqNo
        {
            get
            {
                ThrowIfDisposed();
                return _delegate.NextPublishSeqNo;
            }
        }

        public async ValueTask AutomaticallyRecover(AutorecoveringConnection conn)
        {
            ThrowIfDisposed();
            _connection = conn;
            RecoveryAwareModel defunctModel = _delegate;

            _delegate = await conn.CreateNonRecoveringModel().ConfigureAwait(false);
            _delegate.InheritOffsetFrom(defunctModel);

            RecoverModelShutdownHandlers();
            await RecoverState().ConfigureAwait(false);

            RecoverBasicReturnHandlers();
            RecoverBasicAckHandlers();
            RecoverBasicNackHandlers();
            RecoverCallbackExceptionHandlers();

            RunRecoveryEventHandlers();
        }

        public ValueTask BasicQos(ushort prefetchCount, bool global)
        {
            ThrowIfDisposed();
            return _delegate.BasicQos(0, prefetchCount, global);
        }

        public async ValueTask Close(ushort replyCode, string replyText, bool abort)
        {
            ThrowIfDisposed();
            try
            {
                await _delegate.Close(replyCode, replyText, abort).ConfigureAwait(false);
            }
            finally
            {
                _connection.UnregisterModel(this);
            }
        }

        public async ValueTask Close(ShutdownEventArgs reason, bool abort)
        {
            ThrowIfDisposed();
            try
            {
                await _delegate.Close(reason, abort).ConfigureAwait(false);
            }
            finally
            {
                _connection.UnregisterModel(this);
            }
        }

        public bool DispatchAsynchronous(Command cmd)
        {
            ThrowIfDisposed();
            return _delegate.DispatchAsynchronous(cmd);
        }

        public ValueTask FinishClose()
        {
            ThrowIfDisposed();
            return _delegate.FinishClose();
        }

        public ValueTask HandleCommand(ISession session, Command cmd)
        {
            ThrowIfDisposed();
            return _delegate.HandleCommand(session, cmd);
        }

        public void OnBasicAck(BasicAckEventArgs args)
        {
            ThrowIfDisposed();
            _delegate.OnBasicAck(args);
        }

        public void OnBasicNack(BasicNackEventArgs args)
        {
            ThrowIfDisposed();
            _delegate.OnBasicNack(args);
        }

        public void OnBasicRecoverOk(EventArgs args)
        {
            ThrowIfDisposed();
            _delegate.OnBasicRecoverOk(args);
        }

        public void OnBasicReturn(BasicReturnEventArgs args)
        {
            ThrowIfDisposed();
            _delegate.OnBasicReturn(args);
        }

        public void OnCallbackException(CallbackExceptionEventArgs args)
        {
            ThrowIfDisposed();
            _delegate.OnCallbackException(args);
        }

        public void OnFlowControl(FlowControlEventArgs args)
        {
            ThrowIfDisposed();
            _delegate.OnFlowControl(args);
        }

        public ValueTask OnModelShutdown(ShutdownEventArgs reason)
        {
            ThrowIfDisposed();
            return _delegate.OnModelShutdown(reason);
        }

        public ValueTask OnSessionShutdown(ISession session, ShutdownEventArgs reason)
        {
            ThrowIfDisposed();
            return _delegate.OnSessionShutdown(session, reason);
        }

        public bool SetCloseReason(ShutdownEventArgs reason)
        {
            ThrowIfDisposed();
            return _delegate.SetCloseReason(reason);
        }

        public override string ToString()
        {
            ThrowIfDisposed();
            return _delegate.ToString();
        }

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

        public ValueTask HandleBasicAck(ulong deliveryTag,
            bool multiple)
        {
            ThrowIfDisposed();
            return _delegate.HandleBasicAck(deliveryTag, multiple);
        }

        public ValueTask HandleBasicCancel(string consumerTag, bool nowait)
        {
            ThrowIfDisposed();
            return _delegate.HandleBasicCancel(consumerTag, nowait);
        }

        public ValueTask HandleBasicCancelOk(string consumerTag)
        {
            ThrowIfDisposed();
            return _delegate.HandleBasicCancelOk(consumerTag);
        }

        public ValueTask HandleBasicConsumeOk(string consumerTag)
        {
            ThrowIfDisposed();
            return _delegate.HandleBasicConsumeOk(consumerTag);
        }

        public ValueTask HandleBasicDeliver(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body)
        {
            ThrowIfDisposed();
            return _delegate.HandleBasicDeliver(consumerTag, deliveryTag, redelivered, exchange,
                routingKey, basicProperties, body);
        }

        public ValueTask HandleBasicNack(ulong deliveryTag,
            bool multiple,
            bool requeue)
        {
            ThrowIfDisposed();
            return _delegate.HandleBasicNack(deliveryTag, multiple, requeue);
        }

        public ValueTask HandleBasicRecoverOk()
        {
            ThrowIfDisposed();
            return _delegate.HandleBasicRecoverOk();
        }

        public ValueTask HandleBasicReturn(ushort replyCode,
            string replyText,
            string exchange,
            string routingKey,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body)
        {
            ThrowIfDisposed();
            return _delegate.HandleBasicReturn(replyCode, replyText, exchange,
                routingKey, basicProperties, body);
        }

        public ValueTask HandleChannelClose(ushort replyCode,
            string replyText,
            ushort classId,
            ushort methodId)
        {
            ThrowIfDisposed();
            return _delegate.HandleChannelClose(replyCode, replyText, classId, methodId);
        }

        public ValueTask HandleChannelCloseOk()
        {
            ThrowIfDisposed();
            return _delegate.HandleChannelCloseOk();
        }

        public ValueTask HandleChannelFlow(bool active)
        {
            ThrowIfDisposed();
            return _delegate.HandleChannelFlow(active);
        }

        public ValueTask HandleQueueDeclareOk(string queue,
            uint messageCount,
            uint consumerCount)
        {
            ThrowIfDisposed();
            return _delegate.HandleQueueDeclareOk(queue, messageCount, consumerCount);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

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
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            try
            {
                _delegate.Abort(replyCode, replyText);
            }
            finally
            {
                _connection.UnregisterModel(this);
            }
        }

        public ValueTask BasicAck(ulong deliveryTag, bool multiple)
        {
            ThrowIfDisposed();
            return _delegate.BasicAck(deliveryTag, multiple);
        }

        public ValueTask BasicCancel(string consumerTag)
        {
            ThrowIfDisposed();

            RecordedConsumer cons = _connection.DeleteRecordedConsumer(consumerTag);
            if (cons != null)
            {
                _connection.MaybeDeleteRecordedAutoDeleteQueue(cons.Queue);
            }

            return _delegate.BasicCancel(consumerTag);
        }

        public ValueTask BasicCancelNoWait(string consumerTag)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            RecordedConsumer cons = _connection.DeleteRecordedConsumer(consumerTag);
            if (cons != null)
            {
                _connection.MaybeDeleteRecordedAutoDeleteQueue(cons.Queue);
            }

            return _delegate.BasicCancelNoWait(consumerTag);
        }

        public async ValueTask<string> BasicConsume(
            string queue,
            bool autoAck,
            string consumerTag,
            bool noLocal,
            bool exclusive,
            IDictionary<string, object> arguments,
            IAsyncBasicConsumer consumer)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            string result = await _delegate.BasicConsume(queue, autoAck, consumerTag, noLocal,
                exclusive, arguments, consumer).ConfigureAwait(false);
            RecordedConsumer rc = new RecordedConsumer(this, queue).
                WithConsumerTag(result).
                WithConsumer(consumer).
                WithExclusive(exclusive).
                WithAutoAck(autoAck).
                WithArguments(arguments);
            _connection.RecordConsumer(result, rc);
            return result;
        }

        public ValueTask<BasicGetResult> BasicGet(string queue,
            bool autoAck)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            return _delegate.BasicGet(queue, autoAck);
        }

        public ValueTask BasicNack(ulong deliveryTag, bool multiple, bool requeue)
        {
            ThrowIfDisposed();
            return _delegate.BasicNack(deliveryTag, multiple, requeue);
        }

        public ValueTask BasicPublish(string exchange, string routingKey, bool mandatory, IBasicProperties basicProperties, ReadOnlyMemory<byte> body)
        {
            ThrowIfDisposed();
            if (routingKey == null)
            {
                throw new ArgumentNullException(nameof(routingKey));
            }

            return _delegate.BasicPublish(exchange, routingKey, mandatory, basicProperties, body);
        }

        public ValueTask BasicQos(uint prefetchSize,
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

            return _delegate.BasicQos(prefetchSize, prefetchCount, global);
        }

        public ValueTask BasicRecover(bool requeue)
        {
            ThrowIfDisposed();
            return _delegate.BasicRecover(requeue);
        }

        public ValueTask BasicRecoverAsync(bool requeue)
        {
            ThrowIfDisposed();
            return _delegate.BasicRecoverAsync(requeue);
        }

        public ValueTask BasicReject(ulong deliveryTag, bool requeue)
        {
            ThrowIfDisposed();
            return _delegate.BasicReject(deliveryTag, requeue);
        }

        public async ValueTask Close()
        {
            ThrowIfDisposed();
            try
            {
                await _delegate.Close().ConfigureAwait(false);
            }
            finally
            {
                _connection.UnregisterModel(this);
            }
        }

        public async ValueTask Close(ushort replyCode, string replyText)
        {
            ThrowIfDisposed();
            try
            {
                await _delegate.Close(replyCode, replyText).ConfigureAwait(false);
            }
            finally
            {
                _connection.UnregisterModel(this);
            }
        }

        public ValueTask ConfirmSelect()
        {
            ThrowIfDisposed();
            _usesPublisherConfirms = true;
            return _delegate.ConfirmSelect();
        }

        public IBasicProperties CreateBasicProperties()
        {
            ThrowIfDisposed();
            return _delegate.CreateBasicProperties();
        }

        public ValueTask ExchangeBind(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            RecordedBinding eb = new RecordedExchangeBinding(this).
                WithSource(source).
                WithDestination(destination).
                WithRoutingKey(routingKey).
                WithArguments(arguments);
            _connection.RecordBinding(eb);
            return _delegate.ExchangeBind(destination, source, routingKey, arguments);
        }

        public ValueTask ExchangeBindNoWait(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            return _delegate.ExchangeBindNoWait(destination, source, routingKey, arguments);
        }

        public async ValueTask ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            RecordedExchange rx = new RecordedExchange(this, exchange).
                WithType(type).
                WithDurable(durable).
                WithAutoDelete(autoDelete).
                WithArguments(arguments);
            await _delegate.ExchangeDeclare(exchange, type, durable, autoDelete, arguments).ConfigureAwait(false);
            _connection.RecordExchange(exchange, rx);
        }

        public async ValueTask ExchangeDeclareNoWait(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            RecordedExchange rx = new RecordedExchange(this, exchange).
                WithType(type).
                WithDurable(durable).
                WithAutoDelete(autoDelete).
                WithArguments(arguments);
            await _delegate.ExchangeDeclareNoWait(exchange, type, durable, autoDelete, arguments).ConfigureAwait(false);
            _connection.RecordExchange(exchange, rx);
        }

        public ValueTask ExchangeDeclarePassive(string exchange)
        {
            ThrowIfDisposed();
            return _delegate.ExchangeDeclarePassive(exchange);
        }

        public async ValueTask ExchangeDelete(string exchange,
            bool ifUnused)
        {
            ThrowIfDisposed();
            await _delegate.ExchangeDelete(exchange, ifUnused).ConfigureAwait(false);
            _connection.DeleteRecordedExchange(exchange);
        }

        public async ValueTask ExchangeDeleteNoWait(string exchange, bool ifUnused)
        {
            ThrowIfDisposed();
            await _delegate.ExchangeDeleteNoWait(exchange, ifUnused).ConfigureAwait(false);
            _connection.DeleteRecordedExchange(exchange);
        }

        public async ValueTask ExchangeUnbind(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            RecordedBinding eb = new RecordedExchangeBinding(this).
                WithSource(source).
                WithDestination(destination).
                WithRoutingKey(routingKey).
                WithArguments(arguments);
            _connection.DeleteRecordedBinding(eb);
            await _delegate.ExchangeUnbind(destination, source, routingKey, arguments).ConfigureAwait(false);
            _connection.MaybeDeleteRecordedAutoDeleteExchange(source);
        }

        public ValueTask ExchangeUnbindNoWait(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            return _delegate.ExchangeUnbindNoWait(destination, source, routingKey, arguments);
        }

        public ValueTask QueueBind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            RecordedBinding qb = new RecordedQueueBinding(this).
                WithSource(exchange).
                WithDestination(queue).
                WithRoutingKey(routingKey).
                WithArguments(arguments);
            _connection.RecordBinding(qb);
            return _delegate.QueueBind(queue, exchange, routingKey, arguments);
        }

        public ValueTask QueueBindNoWait(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            return _delegate.QueueBindNoWait(queue, exchange, routingKey, arguments);
        }

        public async ValueTask<QueueDeclareOk> QueueDeclare(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            QueueDeclareOk result = await _delegate.QueueDeclare(queue, durable, exclusive,
                autoDelete, arguments).ConfigureAwait(false);
            RecordedQueue rq = new RecordedQueue(this, result.QueueName).
                Durable(durable).
                Exclusive(exclusive).
                AutoDelete(autoDelete).
                Arguments(arguments).
                ServerNamed(string.Empty.Equals(queue));
            _connection.RecordQueue(result.QueueName, rq);
            return result;
        }

        public async ValueTask QueueDeclareNoWait(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            await _delegate.QueueDeclareNoWait(queue, durable, exclusive, autoDelete, arguments).ConfigureAwait(false);
            RecordedQueue rq = new RecordedQueue(this, queue).
                Durable(durable).
                Exclusive(exclusive).
                AutoDelete(autoDelete).
                Arguments(arguments).
                ServerNamed(string.Empty.Equals(queue));
            _connection.RecordQueue(queue, rq);
        }

        public ValueTask<QueueDeclareOk> QueueDeclarePassive(string queue)
        {
            ThrowIfDisposed();
            return _delegate.QueueDeclarePassive(queue);
        }

        public ValueTask<uint> MessageCount(string queue)
        {
            ThrowIfDisposed();
            return _delegate.MessageCount(queue);
        }

        public ValueTask<uint> ConsumerCount(string queue)
        {
            ThrowIfDisposed();
            return _delegate.ConsumerCount(queue);
        }

        public async ValueTask<uint> QueueDelete(string queue, bool ifUnused, bool ifEmpty)
        {
            ThrowIfDisposed();
            uint result = await _delegate.QueueDelete(queue, ifUnused, ifEmpty).ConfigureAwait(false);
            _connection.DeleteRecordedQueue(queue);
            return result;
        }

        public async ValueTask QueueDeleteNoWait(string queue, bool ifUnused, bool ifEmpty)
        {
            ThrowIfDisposed();
            await _delegate.QueueDeleteNoWait(queue, ifUnused, ifEmpty).ConfigureAwait(false);
            _connection.DeleteRecordedQueue(queue);
        }

        public ValueTask<uint> QueuePurge(string queue)
        {
            ThrowIfDisposed();
            return _delegate.QueuePurge(queue);
        }

        public async ValueTask QueueUnbind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            RecordedBinding qb = new RecordedQueueBinding(this).
                WithSource(exchange).
                WithDestination(queue).
                WithRoutingKey(routingKey).
                WithArguments(arguments);
            _connection.DeleteRecordedBinding(qb);
            await _delegate.QueueUnbind(queue, exchange, routingKey, arguments).ConfigureAwait(false);
            _connection.MaybeDeleteRecordedAutoDeleteExchange(exchange);
        }

        public ValueTask TxCommit()
        {
            ThrowIfDisposed();
            return _delegate.TxCommit();
        }

        public ValueTask TxRollback()
        {
            ThrowIfDisposed();
            return _delegate.TxRollback();
        }

        public ValueTask TxSelect()
        {
            ThrowIfDisposed();
            _usesTransactions = true;
            return _delegate.TxSelect();
        }

        public ValueTask<bool> WaitForConfirms(TimeSpan timeout)
        {
            ThrowIfDisposed();
            return _delegate.WaitForConfirms(timeout);
        }

        public ValueTask<bool> WaitForConfirms()
        {
            ThrowIfDisposed();
            return _delegate.WaitForConfirms();
        }

        public ValueTask WaitForConfirmsOrDie()
        {
            ThrowIfDisposed();
            return _delegate.WaitForConfirmsOrDie();
        }

        public ValueTask WaitForConfirmsOrDie(TimeSpan timeout)
        {
            ThrowIfDisposed();
            return _delegate.WaitForConfirmsOrDie(timeout);
        }

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

        private async ValueTask RecoverState()
        {
            if (_prefetchCountConsumer != 0)
            {
                await BasicQos(_prefetchCountConsumer, false).ConfigureAwait(false);
            }

            if (_prefetchCountGlobal != 0)
            {
                await BasicQos(_prefetchCountGlobal, true).ConfigureAwait(false);
            }

            if (_usesPublisherConfirms)
            {
                await ConfirmSelect().ConfigureAwait(false);
            }

            if (_usesTransactions)
            {
                await TxSelect().ConfigureAwait(false);
            }
        }

        private void RunRecoveryEventHandlers()
        {
            ThrowIfDisposed();
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
            ThrowIfDisposed();
            return ((IFullModel)_delegate).CreateBasicPublishBatch();
        }
    }
}
