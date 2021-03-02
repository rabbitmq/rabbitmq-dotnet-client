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
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.ConsumerDispatching;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    internal sealed class AutorecoveringModel : IModel, IRecoverable
    {
        private AutorecoveringConnection _connection;
        private RecoveryAwareModel _innerChannel;
        private bool _disposed;

        private ushort _prefetchCountConsumer;
        private ushort _prefetchCountGlobal;
        private bool _usesPublisherConfirms;
        private bool _usesTransactions;

        internal IConsumerDispatcher ConsumerDispatcher => InnerChannel.ConsumerDispatcher;

        internal RecoveryAwareModel InnerChannel
        {
            get
            {
                ThrowIfDisposed();
                return _innerChannel;
            }
        }

        public TimeSpan ContinuationTimeout
        {
            get => InnerChannel.ContinuationTimeout;
            set => InnerChannel.ContinuationTimeout = value;
        }

        public AutorecoveringModel(AutorecoveringConnection conn, RecoveryAwareModel innerChannel)
        {
            _connection = conn;
            _innerChannel = innerChannel;
        }

        public event EventHandler<BasicAckEventArgs> BasicAcks
        {
            add => InnerChannel.BasicAcks += value;
            remove => InnerChannel.BasicAcks -= value;
        }

        public event EventHandler<BasicNackEventArgs> BasicNacks
        {
            add => InnerChannel.BasicNacks += value;
            remove => InnerChannel.BasicNacks -= value;
        }

        public event EventHandler<EventArgs> BasicRecoverOk
        {
            add => InnerChannel.BasicRecoverOk += value;
            remove => InnerChannel.BasicRecoverOk -= value;
        }

        public event EventHandler<BasicReturnEventArgs> BasicReturn
        {
            add => InnerChannel.BasicReturn += value;
            remove => InnerChannel.BasicReturn -= value;
        }

        public event EventHandler<CallbackExceptionEventArgs> CallbackException
        {
            add => InnerChannel.CallbackException += value;
            remove => InnerChannel.CallbackException -= value;
        }

        public event EventHandler<FlowControlEventArgs> FlowControl
        {
            add { InnerChannel.FlowControl += value; }
            remove { InnerChannel.FlowControl -= value; }
        }

        public event EventHandler<ShutdownEventArgs> ModelShutdown
        {
            add => InnerChannel.ModelShutdown += value;
            remove => InnerChannel.ModelShutdown -= value;
        }

        public event EventHandler<EventArgs> Recovery
        {
            add { InnerChannel.Recovery += value; }
            remove { InnerChannel.Recovery -= value; }
        }

        public int ChannelNumber => InnerChannel.ChannelNumber;

        public ShutdownEventArgs CloseReason => InnerChannel.CloseReason;

        public IBasicConsumer DefaultConsumer
        {
            get => InnerChannel.DefaultConsumer;
            set => InnerChannel.DefaultConsumer = value;
        }

        public bool IsClosed => _innerChannel != null && _innerChannel.IsClosed;

        public bool IsOpen => _innerChannel != null && _innerChannel.IsOpen;

        public ulong NextPublishSeqNo => InnerChannel.NextPublishSeqNo;

        internal void AutomaticallyRecover(AutorecoveringConnection conn)
        {
            ThrowIfDisposed();
            _connection = conn;
            RecoveryAwareModel defunctModel = _innerChannel;

            _innerChannel = conn.CreateNonRecoveringModel();
            _innerChannel.TakeOver(defunctModel);

            RecoverState();

            InnerChannel.RunRecoveryEventHandlers(this);
        }

        public void BasicQos(ushort prefetchCount, bool global)
            => InnerChannel.BasicQos(0, prefetchCount, global);

        public void Close(ushort replyCode, string replyText, bool abort)
        {
            ThrowIfDisposed();
            try
            {
                _innerChannel.Close(replyCode, replyText, abort);
            }
            finally
            {
                _connection.DeleteRecordedChannel(this);
            }
        }

        public override string ToString()
            => InnerChannel.ToString();

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            this.Abort();

            _connection = null;
            _innerChannel = null;
            _disposed = true;
        }

        public void BasicAck(ulong deliveryTag, bool multiple)
            => InnerChannel.BasicAck(deliveryTag, multiple);

        public void BasicCancel(string consumerTag)
        {
            ThrowIfDisposed();
            _connection.DeleteRecordedConsumer(consumerTag);
            _innerChannel.BasicCancel(consumerTag);
        }

        public void BasicCancelNoWait(string consumerTag)
        {
            ThrowIfDisposed();
            _connection.DeleteRecordedConsumer(consumerTag);
            _innerChannel.BasicCancelNoWait(consumerTag);
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
            string result = InnerChannel.BasicConsume(queue, autoAck, consumerTag, noLocal, exclusive, arguments, consumer);
            RecordedConsumer rc = new RecordedConsumer(this, consumer, queue, autoAck, result, exclusive, arguments);
            _connection.RecordConsumer(result, rc);
            return result;
        }

        public BasicGetResult BasicGet(string queue, bool autoAck)
            => InnerChannel.BasicGet(queue, autoAck);

        public void BasicNack(ulong deliveryTag, bool multiple, bool requeue)
            => InnerChannel.BasicNack(deliveryTag, multiple, requeue);

        public void BasicPublish(string exchange, string routingKey, bool mandatory, IBasicProperties basicProperties, ReadOnlyMemory<byte> body)
            => InnerChannel.BasicPublish(exchange, routingKey, mandatory, basicProperties, body);

        public void BasicPublish(CachedString exchange, CachedString routingKey, bool mandatory, IBasicProperties basicProperties, ReadOnlyMemory<byte> body)
            => InnerChannel.BasicPublish(exchange, routingKey, mandatory, basicProperties, body);

        public void BasicQos(uint prefetchSize, ushort prefetchCount, bool global)
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
            _innerChannel.BasicQos(prefetchSize, prefetchCount, global);
        }

        public void BasicRecover(bool requeue)
            => InnerChannel.BasicRecover(requeue);

        public void BasicRecoverAsync(bool requeue)
            => InnerChannel.BasicRecoverAsync(requeue);

        public void BasicReject(ulong deliveryTag, bool requeue)
            => InnerChannel.BasicReject(deliveryTag, requeue);

        public void ConfirmSelect()
        {
            InnerChannel.ConfirmSelect();
            _usesPublisherConfirms = true;
        }

        public IBasicProperties CreateBasicProperties()
            => InnerChannel.CreateBasicProperties();

        public void ExchangeBind(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            RecordedBinding eb = new RecordedExchangeBinding(this, destination, source, routingKey, arguments);
            _connection.RecordBinding(eb);
            _innerChannel.ExchangeBind(destination, source, routingKey, arguments);
        }

        public void ExchangeBindNoWait(string destination, string source, string routingKey, IDictionary<string, object> arguments)
            => InnerChannel.ExchangeBindNoWait(destination, source, routingKey, arguments);

        public void ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            _innerChannel.ExchangeDeclare(exchange, type, durable, autoDelete, arguments);
            RecordedExchange rx = new RecordedExchange(this, exchange, type, durable, autoDelete, arguments);
            _connection.RecordExchange(rx);
        }

        public void ExchangeDeclareNoWait(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            _innerChannel.ExchangeDeclareNoWait(exchange, type, durable, autoDelete, arguments);
            RecordedExchange rx = new RecordedExchange(this, exchange, type, durable, autoDelete, arguments);
            _connection.RecordExchange(rx);
        }

        public void ExchangeDeclarePassive(string exchange)
            => InnerChannel.ExchangeDeclarePassive(exchange);

        public void ExchangeDelete(string exchange, bool ifUnused)
        {
            InnerChannel.ExchangeDelete(exchange, ifUnused);
            _connection.DeleteRecordedExchange(exchange);
        }

        public void ExchangeDeleteNoWait(string exchange, bool ifUnused)
        {
            InnerChannel.ExchangeDeleteNoWait(exchange, ifUnused);
            _connection.DeleteRecordedExchange(exchange);
        }

        public void ExchangeUnbind(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            RecordedBinding eb = new RecordedExchangeBinding(this, destination, source, routingKey, arguments);
            _connection.DeleteRecordedBinding(eb);
            _innerChannel.ExchangeUnbind(destination, source, routingKey, arguments);
            _connection.DeleteAutoDeleteExchange(source);
        }

        public void ExchangeUnbindNoWait(string destination, string source, string routingKey, IDictionary<string, object> arguments)
            => InnerChannel.ExchangeUnbind(destination, source, routingKey, arguments);

        public void QueueBind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            RecordedBinding qb = new RecordedQueueBinding(this, queue, exchange, routingKey, arguments);
            _connection.RecordBinding(qb);
            _innerChannel.QueueBind(queue, exchange, routingKey, arguments);
        }

        public void QueueBindNoWait(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
            => InnerChannel.QueueBind(queue, exchange, routingKey, arguments);

        public QueueDeclareOk QueueDeclare(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            QueueDeclareOk result = _innerChannel.QueueDeclare(queue, durable, exclusive, autoDelete, arguments);
            RecordedQueue rq = new RecordedQueue(this, result.QueueName, queue.Length == 0, durable, exclusive, autoDelete, arguments);
            _connection.RecordQueue(rq);
            return result;
        }

        public void QueueDeclareNoWait(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            _innerChannel.QueueDeclareNoWait(queue, durable, exclusive,
                autoDelete, arguments);
            RecordedQueue rq = new RecordedQueue(this, queue, queue.Length == 0, durable, exclusive, autoDelete, arguments);
            _connection.RecordQueue(rq);
        }

        public QueueDeclareOk QueueDeclarePassive(string queue)
            => InnerChannel.QueueDeclarePassive(queue);

        public uint MessageCount(string queue)
            => InnerChannel.MessageCount(queue);

        public uint ConsumerCount(string queue)
            => InnerChannel.ConsumerCount(queue);

        public uint QueueDelete(string queue, bool ifUnused, bool ifEmpty)
        {
            ThrowIfDisposed();
            uint result = _innerChannel.QueueDelete(queue, ifUnused, ifEmpty);
            _connection.DeleteRecordedQueue(queue);
            return result;
        }

        public void QueueDeleteNoWait(string queue, bool ifUnused, bool ifEmpty)
        {
            InnerChannel.QueueDeleteNoWait(queue, ifUnused, ifEmpty);
            _connection.DeleteRecordedQueue(queue);
        }

        public uint QueuePurge(string queue)
            => InnerChannel.QueuePurge(queue);

        public void QueueUnbind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            RecordedBinding qb = new RecordedQueueBinding(this, queue, exchange, routingKey, arguments);
            _connection.DeleteRecordedBinding(qb);
            _innerChannel.QueueUnbind(queue, exchange, routingKey, arguments);
            _connection.DeleteAutoDeleteExchange(exchange);
        }

        public void TxCommit()
            => InnerChannel.TxCommit();

        public void TxRollback()
            => InnerChannel.TxRollback();

        public void TxSelect()
        {
            InnerChannel.TxSelect();
            _usesTransactions = true;
        }

        public Task<bool> WaitForConfirmsAsync(CancellationToken token = default)
            => InnerChannel.WaitForConfirmsAsync(token);

        public Task WaitForConfirmsOrDieAsync(CancellationToken token = default)
            => InnerChannel.WaitForConfirmsOrDieAsync(token);

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

        public IBasicPublishBatch CreateBasicPublishBatch()
            => InnerChannel.CreateBasicPublishBatch();

        public IBasicPublishBatch CreateBasicPublishBatch(int sizeHint)
            => InnerChannel.CreateBasicPublishBatch(sizeHint);

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                ThrowDisposed();
            }

            static void ThrowDisposed() => throw new ObjectDisposedException(typeof(AutorecoveringModel).FullName);
        }
    }
}
