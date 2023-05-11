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
    internal sealed class AutorecoveringChannel : IChannel, IRecoverable
    {
        private AutorecoveringConnection _connection;
        private RecoveryAwareChannel _innerChannel;
        private bool _disposed;
        private List<string> _recordedConsumerTags = new List<string>();

        private ushort _prefetchCountConsumer;
        private ushort _prefetchCountGlobal;
        private bool _usesPublisherConfirms;
        private bool _usesTransactions;

        internal IConsumerDispatcher ConsumerDispatcher => InnerChannel.ConsumerDispatcher;

        internal RecoveryAwareChannel InnerChannel
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

        public AutorecoveringChannel(AutorecoveringConnection conn, RecoveryAwareChannel innerChannel)
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

        public event EventHandler<ShutdownEventArgs> ChannelShutdown
        {
            add => InnerChannel.ChannelShutdown += value;
            remove => InnerChannel.ChannelShutdown -= value;
        }

        public event EventHandler<EventArgs> Recovery
        {
            add { InnerChannel.Recovery += value; }
            remove { InnerChannel.Recovery -= value; }
        }

        public IEnumerable<string> ConsumerTags
        {
            get
            {
                ThrowIfDisposed();
                return _recordedConsumerTags;
            }
        }

        public int ChannelNumber
        {
            get
            {
                ThrowIfDisposed();
                return InnerChannel.ChannelNumber;
            }
        }

        public ShutdownEventArgs CloseReason
        {
            get
            {
                ThrowIfDisposed();
                return InnerChannel.CloseReason;
            }
        }

        public IBasicConsumer DefaultConsumer
        {
            get
            {
                ThrowIfDisposed();
                return InnerChannel.DefaultConsumer;
            }

            set
            {
                ThrowIfDisposed();
                InnerChannel.DefaultConsumer = value;
            }
        }

        public bool IsClosed => !IsOpen;

        public bool IsOpen
        {
            get
            {
                ThrowIfDisposed();
                return _innerChannel != null && _innerChannel.IsOpen;
            }
        }

        public ulong NextPublishSeqNo
        {
            get
            {
                ThrowIfDisposed();
                return InnerChannel.NextPublishSeqNo;
            }
        }

        public string CurrentQueue
        {
            get
            {
                ThrowIfDisposed();
                return InnerChannel.CurrentQueue;
            }
        }

        internal void AutomaticallyRecover(AutorecoveringConnection conn, bool recoverConsumers)
        {
            ThrowIfDisposed();
            _connection = conn;

            var newChannel = conn.CreateNonRecoveringChannel();
            newChannel.TakeOver(_innerChannel);

            if (_prefetchCountConsumer != 0)
            {
                newChannel.BasicQos(0, _prefetchCountConsumer, false);
            }

            if (_prefetchCountGlobal != 0)
            {
                newChannel.BasicQos(0, _prefetchCountGlobal, true);
            }

            if (_usesPublisherConfirms)
            {
                newChannel.ConfirmSelect();
            }

            if (_usesTransactions)
            {
                newChannel.TxSelect();
            }

            /*
             * https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1140
             * If this assignment is not done before recovering consumers, there is a good
             * chance that an invalid Channel will be used to handle a basic.deliver frame,
             * with the resulting basic.ack never getting sent out.
             */
            _innerChannel = newChannel;

            if (recoverConsumers)
            {
                _connection.RecoverConsumers(this, newChannel);
            }

            _innerChannel.RunRecoveryEventHandlers(this);
        }

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

            _recordedConsumerTags.Clear();
            _recordedConsumerTags = null;
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
            string resultConsumerTag = InnerChannel.BasicConsume(queue, autoAck, consumerTag, noLocal, exclusive, arguments, consumer);
            var rc = new RecordedConsumer(channel: this, consumer: consumer, consumerTag: resultConsumerTag,
                queue: queue, autoAck: autoAck, exclusive: exclusive, arguments: arguments);
            _connection.RecordConsumer(rc);
            _recordedConsumerTags.Add(resultConsumerTag);
            return resultConsumerTag;
        }

        public BasicGetResult BasicGet(string queue, bool autoAck)
            => InnerChannel.BasicGet(queue, autoAck);

        public void BasicNack(ulong deliveryTag, bool multiple, bool requeue)
            => InnerChannel.BasicNack(deliveryTag, multiple, requeue);

        public void BasicPublish<TProperties>(string exchange, string routingKey, in TProperties basicProperties, ReadOnlyMemory<byte> body, bool mandatory)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
            => InnerChannel.BasicPublish(exchange, routingKey, in basicProperties, body, mandatory);

        public void BasicPublish<TProperties>(CachedString exchange, CachedString routingKey, in TProperties basicProperties, ReadOnlyMemory<byte> body, bool mandatory)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
            => InnerChannel.BasicPublish(exchange, routingKey, in basicProperties, body, mandatory);

        public ValueTask BasicPublishAsync<TProperties>(string exchange, string routingKey, in TProperties basicProperties, ReadOnlyMemory<byte> body, bool mandatory)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
            => InnerChannel.BasicPublishAsync(exchange, routingKey, in basicProperties, body, mandatory);

        public ValueTask BasicPublishAsync<TProperties>(CachedString exchange, CachedString routingKey, in TProperties basicProperties, ReadOnlyMemory<byte> body, bool mandatory)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
            => InnerChannel.BasicPublishAsync(exchange, routingKey, in basicProperties, body, mandatory);

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

        public void ExchangeBind(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            _connection.RecordBinding(new RecordedBinding(false, destination, source, routingKey, arguments));
            _innerChannel.ExchangeBind(destination, source, routingKey, arguments);
        }

        public void ExchangeBindNoWait(string destination, string source, string routingKey, IDictionary<string, object> arguments)
            => InnerChannel.ExchangeBindNoWait(destination, source, routingKey, arguments);

        public void ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            _innerChannel.ExchangeDeclare(exchange, type, durable, autoDelete, arguments);
            _connection.RecordExchange(new RecordedExchange(exchange, type, durable, autoDelete, arguments));
        }

        public async ValueTask ExchangeDeclareAsync(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            await _innerChannel.ExchangeDeclareAsync(exchange, type, durable, autoDelete, arguments);
            _connection.RecordExchange(new RecordedExchange(exchange, type, durable, autoDelete, arguments));
        }

        public void ExchangeDeclareNoWait(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            _innerChannel.ExchangeDeclareNoWait(exchange, type, durable, autoDelete, arguments);
            _connection.RecordExchange(new RecordedExchange(exchange, type, durable, autoDelete, arguments));
        }

        public void ExchangeDeclarePassive(string exchange)
            => InnerChannel.ExchangeDeclarePassive(exchange);

        public void ExchangeDelete(string exchange, bool ifUnused)
        {
            InnerChannel.ExchangeDelete(exchange, ifUnused);
            _connection.DeleteRecordedExchange(exchange);
        }

        public async ValueTask ExchangeDeleteAsync(string exchange, bool ifUnused)
        {
            await InnerChannel.ExchangeDeleteAsync(exchange, ifUnused);
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
            _connection.DeleteRecordedBinding(new RecordedBinding(false, destination, source, routingKey, arguments));
            _innerChannel.ExchangeUnbind(destination, source, routingKey, arguments);
            _connection.DeleteAutoDeleteExchange(source);
        }

        public void ExchangeUnbindNoWait(string destination, string source, string routingKey, IDictionary<string, object> arguments)
            => InnerChannel.ExchangeUnbind(destination, source, routingKey, arguments);

        public void QueueBind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            _connection.RecordBinding(new RecordedBinding(true, queue, exchange, routingKey, arguments));
            _innerChannel.QueueBind(queue, exchange, routingKey, arguments);
        }

        public void QueueBindNoWait(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
            => InnerChannel.QueueBind(queue, exchange, routingKey, arguments);

        public QueueDeclareOk QueueDeclare(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            QueueDeclareOk result = _innerChannel.QueueDeclare(queue, durable, exclusive, autoDelete, arguments);
            _connection.RecordQueue(new RecordedQueue(result.QueueName, queue.Length == 0, durable, exclusive, autoDelete, arguments));
            return result;
        }

        public void QueueDeclareNoWait(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            _innerChannel.QueueDeclareNoWait(queue, durable, exclusive, autoDelete, arguments);
            _connection.RecordQueue(new RecordedQueue(queue, queue.Length == 0, durable, exclusive, autoDelete, arguments));
        }

        public async ValueTask<QueueDeclareOk> QueueDeclareAsync(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            QueueDeclareOk result = await _innerChannel.QueueDeclareAsync(queue, durable, exclusive, autoDelete, arguments);
            _connection.RecordQueue(new RecordedQueue(result.QueueName, queue.Length == 0, durable, exclusive, autoDelete, arguments));
            return result;
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

        public async ValueTask<uint> QueueDeleteAsync(string queue, bool ifUnused, bool ifEmpty)
        {
            ThrowIfDisposed();
            uint result = await _innerChannel.QueueDeleteAsync(queue, ifUnused, ifEmpty);
            _connection.DeleteRecordedQueue(queue);
            return result;
        }

        public void QueueDeleteNoWait(string queue, bool ifUnused, bool ifEmpty)
        {
            InnerChannel.QueueDeleteNoWait(queue, ifUnused, ifEmpty);
            _connection.DeleteRecordedQueue(queue);
        }

        public async ValueTask<uint> QueueDeleteAsync(string queue, bool ifUnused, bool ifEmpty)
        {
            ThrowIfDisposed();
            uint result = await _innerChannel.QueueDeleteAsync(queue, ifUnused, ifEmpty);
            _connection.DeleteRecordedQueue(queue);
            return result;
        }

        public uint QueuePurge(string queue)
            => InnerChannel.QueuePurge(queue);

        public void QueueUnbind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            _connection.DeleteRecordedBinding(new RecordedBinding(true, queue, exchange, routingKey, arguments));
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                ThrowDisposed();
            }

            static void ThrowDisposed() => throw new ObjectDisposedException(typeof(AutorecoveringChannel).FullName);
        }
    }
}
