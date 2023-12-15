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
using System.Buffers;
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
        private readonly List<string> _recordedConsumerTags = new();

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
                // TODO should this be copied "on its way out"?
                return _recordedConsumerTags;
            }
        }

        public int ChannelNumber => InnerChannel.ChannelNumber;

        public ShutdownEventArgs CloseReason => InnerChannel.CloseReason;

        public IBasicConsumer DefaultConsumer
        {
            get => InnerChannel.DefaultConsumer;
            set => InnerChannel.DefaultConsumer = value;
        }

        public bool IsClosed => !IsOpen;

        public bool IsOpen => _innerChannel != null && _innerChannel.IsOpen;

        public ulong NextPublishSeqNo => InnerChannel.NextPublishSeqNo;

        public string CurrentQueue => InnerChannel.CurrentQueue;

        internal async ValueTask AutomaticallyRecoverAsync(AutorecoveringConnection conn, bool recoverConsumers,
            bool recordedEntitiesSemaphoreHeld = false)
        {
            if (false == recordedEntitiesSemaphoreHeld)
            {
                throw new InvalidOperationException("recordedEntitiesSemaphore must be held");
            }

            ThrowIfDisposed();
            _connection = conn;

            RecoveryAwareChannel newChannel = await conn.CreateNonRecoveringChannelAsync()
                .ConfigureAwait(false);
            newChannel.TakeOver(_innerChannel);

            if (_prefetchCountConsumer != 0)
            {
                await newChannel.BasicQosAsync(0, _prefetchCountConsumer, false)
                    .ConfigureAwait(false);
            }

            if (_prefetchCountGlobal != 0)
            {
                await newChannel.BasicQosAsync(0, _prefetchCountGlobal, true)
                    .ConfigureAwait(false);
            }

            if (_usesPublisherConfirms)
            {
                await newChannel.ConfirmSelectAsync()
                    .ConfigureAwait(false);
            }

            if (_usesTransactions)
            {
                await newChannel.TxSelectAsync()
                    .ConfigureAwait(false);
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
                await _connection.RecoverConsumersAsync(this, newChannel, recordedEntitiesSemaphoreHeld)
                    .ConfigureAwait(false);
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
                _connection.DeleteRecordedChannel(this,
                    channelsSemaphoreHeld: false, recordedEntitiesSemaphoreHeld: false);
            }
        }

        public ValueTask CloseAsync(ushort replyCode, string replyText, bool abort)
        {
            ThrowIfDisposed();
            var args = new ShutdownEventArgs(ShutdownInitiator.Library, replyCode, replyText);
            return CloseAsync(args, abort);
        }

        public async ValueTask CloseAsync(ShutdownEventArgs args, bool abort)
        {
            ThrowIfDisposed();
            try
            {
                await _innerChannel.CloseAsync(args, abort)
                    .ConfigureAwait(false);
            }
            finally
            {
                await _connection.DeleteRecordedChannelAsync(this,
                    channelsSemaphoreHeld: false, recordedEntitiesSemaphoreHeld: false)
                        .ConfigureAwait(false);
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
            _connection = null;
            _innerChannel = null;
            _disposed = true;
        }

        public void BasicAck(ulong deliveryTag, bool multiple)
            => InnerChannel.BasicAck(deliveryTag, multiple);

        public ValueTask BasicAckAsync(ulong deliveryTag, bool multiple)
            => InnerChannel.BasicAckAsync(deliveryTag, multiple);

        public void BasicCancel(string consumerTag)
        {
            ThrowIfDisposed();
            _connection.DeleteRecordedConsumer(consumerTag, recordedEntitiesSemaphoreHeld: false);
            _innerChannel.BasicCancel(consumerTag);
        }

        public ValueTask BasicCancelAsync(string consumerTag)
        {
            ThrowIfDisposed();
            _connection.DeleteRecordedConsumer(consumerTag, recordedEntitiesSemaphoreHeld: false);
            return _innerChannel.BasicCancelAsync(consumerTag);
        }

        public void BasicCancelNoWait(string consumerTag)
        {
            ThrowIfDisposed();
            _connection.DeleteRecordedConsumer(consumerTag, recordedEntitiesSemaphoreHeld: false);
            _innerChannel.BasicCancelNoWait(consumerTag);
        }

        public string BasicConsume(string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive,
            IDictionary<string, object> arguments, IBasicConsumer consumer)
        {
            string resultConsumerTag = InnerChannel.BasicConsume(queue, autoAck, consumerTag, noLocal, exclusive, arguments, consumer);
            var rc = new RecordedConsumer(channel: this, consumer: consumer, consumerTag: resultConsumerTag,
                queue: queue, autoAck: autoAck, exclusive: exclusive, arguments: arguments);
            _connection.RecordConsumer(rc, recordedEntitiesSemaphoreHeld: false);
            _recordedConsumerTags.Add(resultConsumerTag);
            return resultConsumerTag;
        }

        public async ValueTask<string> BasicConsumeAsync(string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive,
            IDictionary<string, object> arguments, IBasicConsumer consumer)
        {
            string resultConsumerTag = await InnerChannel.BasicConsumeAsync(queue, autoAck, consumerTag, noLocal, exclusive, arguments, consumer)
                .ConfigureAwait(false);
            var rc = new RecordedConsumer(channel: this, consumer: consumer, consumerTag: resultConsumerTag,
                queue: queue, autoAck: autoAck, exclusive: exclusive, arguments: arguments);
            await _connection.RecordConsumerAsync(rc, recordedEntitiesSemaphoreHeld: false)
                .ConfigureAwait(false);
            _recordedConsumerTags.Add(resultConsumerTag);
            return resultConsumerTag;
        }

        public BasicGetResult BasicGet(string queue, bool autoAck)
            => InnerChannel.BasicGet(queue, autoAck);

        public ValueTask<BasicGetResult> BasicGetAsync(string queue, bool autoAck)
            => InnerChannel.BasicGetAsync(queue, autoAck);

        public void BasicNack(ulong deliveryTag, bool multiple, bool requeue)
            => InnerChannel.BasicNack(deliveryTag, multiple, requeue);

        public ValueTask BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue)
            => InnerChannel.BasicNackAsync(deliveryTag, multiple, requeue);

        public void BasicPublish<TProperties>(string exchange, string routingKey, in TProperties basicProperties, ReadOnlyMemory<byte> body, bool mandatory)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
            => InnerChannel.BasicPublish(exchange, routingKey, in basicProperties, body, mandatory);

        public void BasicPublish<TProperties>(CachedString exchange, CachedString routingKey, in TProperties basicProperties, ReadOnlyMemory<byte> body, bool mandatory)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
            => InnerChannel.BasicPublish(exchange, routingKey, in basicProperties, body, mandatory);

        public ValueTask BasicPublishAsync<TProperties>(string exchange, string routingKey, in TProperties basicProperties, ReadOnlyMemory<byte> body, bool mandatory, bool copyBody = true)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
            => InnerChannel.BasicPublishAsync(exchange, routingKey, in basicProperties, body, mandatory, copyBody);

        public ValueTask BasicPublishAsync<TProperties>(string exchange, string routingKey, in TProperties basicProperties, ReadOnlySequence<byte> body, bool mandatory, bool copyBody = true)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
            => InnerChannel.BasicPublishAsync(exchange, routingKey, in basicProperties, body, mandatory, copyBody);

        public ValueTask BasicPublishAsync<TProperties>(CachedString exchange, CachedString routingKey, in TProperties basicProperties, ReadOnlyMemory<byte> body, bool mandatory, bool copyBody = true)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
            => InnerChannel.BasicPublishAsync(exchange, routingKey, in basicProperties, body, mandatory, copyBody);

        public ValueTask BasicPublishAsync<TProperties>(CachedString exchange, CachedString routingKey, in TProperties basicProperties, ReadOnlySequence<byte> body, bool mandatory, bool copyBody = true)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
            => InnerChannel.BasicPublishAsync(exchange, routingKey, in basicProperties, body, mandatory, copyBody);

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

        public ValueTask BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global)
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

            return _innerChannel.BasicQosAsync(prefetchSize, prefetchCount, global);
        }

        public void BasicReject(ulong deliveryTag, bool requeue)
            => InnerChannel.BasicReject(deliveryTag, requeue);

        public ValueTask BasicRejectAsync(ulong deliveryTag, bool requeue)
            => InnerChannel.BasicRejectAsync(deliveryTag, requeue);

        public void ConfirmSelect()
        {
            InnerChannel.ConfirmSelect();
            _usesPublisherConfirms = true;
        }

        public async ValueTask ConfirmSelectAsync()
        {
            await InnerChannel.ConfirmSelectAsync()
                .ConfigureAwait(false);
            _usesPublisherConfirms = true;
        }

        public void ExchangeBind(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            var recordedBinding = new RecordedBinding(false, destination, source, routingKey, arguments);
            _connection.RecordBinding(recordedBinding, recordedEntitiesSemaphoreHeld: false);
            _innerChannel.ExchangeBind(destination, source, routingKey, arguments);
        }

        public async ValueTask ExchangeBindAsync(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            await InnerChannel.ExchangeBindAsync(destination, source, routingKey, arguments)
                .ConfigureAwait(false);
            var recordedBinding = new RecordedBinding(false, destination, source, routingKey, arguments);
            await _connection.RecordBindingAsync(recordedBinding, recordedEntitiesSemaphoreHeld: false)
                .ConfigureAwait(false);
        }

        public void ExchangeBindNoWait(string destination, string source, string routingKey, IDictionary<string, object> arguments)
            => InnerChannel.ExchangeBindNoWait(destination, source, routingKey, arguments);

        public void ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments)
        {
            InnerChannel.ExchangeDeclare(exchange, type, durable, autoDelete, arguments);
            var recordedExchange = new RecordedExchange(exchange, type, durable, autoDelete, arguments);
            _connection.RecordExchange(recordedExchange, recordedEntitiesSemaphoreHeld: false);
        }

        public async ValueTask ExchangeDeclareAsync(string exchange, string type, bool passive, bool durable, bool autoDelete, IDictionary<string, object> arguments)
        {
            await InnerChannel.ExchangeDeclareAsync(exchange, type, passive, durable, autoDelete, arguments)
                .ConfigureAwait(false);
            if (false == passive)
            {
                var recordedExchange = new RecordedExchange(exchange, type, durable, autoDelete, arguments);
                await _connection.RecordExchangeAsync(recordedExchange, recordedEntitiesSemaphoreHeld: false)
                    .ConfigureAwait(false);
            }
        }

        public void ExchangeDeclareNoWait(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments)
        {
            InnerChannel.ExchangeDeclareNoWait(exchange, type, durable, autoDelete, arguments);
            var recordedExchange = new RecordedExchange(exchange, type, durable, autoDelete, arguments);
            _connection.RecordExchange(recordedExchange, recordedEntitiesSemaphoreHeld: false);
        }

        public void ExchangeDeclarePassive(string exchange)
            => InnerChannel.ExchangeDeclarePassive(exchange);

        public void ExchangeDelete(string exchange, bool ifUnused)
        {
            InnerChannel.ExchangeDelete(exchange, ifUnused);
            _connection.DeleteRecordedExchange(exchange, recordedEntitiesSemaphoreHeld: false);
        }

        public async ValueTask ExchangeDeleteAsync(string exchange, bool ifUnused)
        {
            await InnerChannel.ExchangeDeleteAsync(exchange, ifUnused)
                .ConfigureAwait(false);
            await _connection.DeleteRecordedExchangeAsync(exchange, recordedEntitiesSemaphoreHeld: false)
                .ConfigureAwait(false);
        }

        public void ExchangeDeleteNoWait(string exchange, bool ifUnused)
        {
            InnerChannel.ExchangeDeleteNoWait(exchange, ifUnused);
            _connection.DeleteRecordedExchange(exchange, recordedEntitiesSemaphoreHeld: false);
        }

        public void ExchangeUnbind(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            var recordedBinding = new RecordedBinding(false, destination, source, routingKey, arguments);
            _connection.DeleteRecordedBinding(recordedBinding, recordedEntitiesSemaphoreHeld: false);
            _innerChannel.ExchangeUnbind(destination, source, routingKey, arguments);
            _connection.DeleteAutoDeleteExchange(source, recordedEntitiesSemaphoreHeld: false);
        }

        public async ValueTask ExchangeUnbindAsync(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            var recordedBinding = new RecordedBinding(false, destination, source, routingKey, arguments);
            await _connection.DeleteRecordedBindingAsync(recordedBinding, recordedEntitiesSemaphoreHeld: false)
                .ConfigureAwait(false);
            await InnerChannel.ExchangeUnbindAsync(destination, source, routingKey, arguments)
                .ConfigureAwait(false);
            await _connection.DeleteAutoDeleteExchangeAsync(source, recordedEntitiesSemaphoreHeld: false)
                .ConfigureAwait(false);
        }

        public void ExchangeUnbindNoWait(string destination, string source, string routingKey, IDictionary<string, object> arguments)
            => InnerChannel.ExchangeUnbind(destination, source, routingKey, arguments);

        public void QueueBind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            var recordedBinding = new RecordedBinding(true, queue, exchange, routingKey, arguments);
            _connection.RecordBinding(recordedBinding, recordedEntitiesSemaphoreHeld: false);
            _innerChannel.QueueBind(queue, exchange, routingKey, arguments);
        }

        public void QueueBindNoWait(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
            => InnerChannel.QueueBind(queue, exchange, routingKey, arguments);

        public QueueDeclareOk QueueDeclare(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments)
        {
            QueueDeclareOk result = InnerChannel.QueueDeclare(queue, durable, exclusive, autoDelete, arguments);
            var recordedQueue = new RecordedQueue(result.QueueName, queue.Length == 0, durable, exclusive, autoDelete, arguments);
            _connection.RecordQueue(recordedQueue, recordedEntitiesSemaphoreHeld: false);
            return result;
        }

        public void QueueDeclareNoWait(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments)
        {
            InnerChannel.QueueDeclareNoWait(queue, durable, exclusive, autoDelete, arguments);
            var recordedQueue = new RecordedQueue(queue, queue.Length == 0, durable, exclusive, autoDelete, arguments);
            _connection.RecordQueue(recordedQueue, recordedEntitiesSemaphoreHeld: false);
        }

        public async ValueTask<QueueDeclareOk> QueueDeclareAsync(string queue, bool passive, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments)
        {
            QueueDeclareOk result = await InnerChannel.QueueDeclareAsync(queue, passive, durable, exclusive, autoDelete, arguments)
                .ConfigureAwait(false);
            if (false == passive)
            {
                var recordedQueue = new RecordedQueue(result.QueueName, queue.Length == 0, durable, exclusive, autoDelete, arguments);
                await _connection.RecordQueueAsync(recordedQueue, recordedEntitiesSemaphoreHeld: false)
                    .ConfigureAwait(false);
            }
            return result;
        }

        public ValueTask QueueBindAsync(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
            => InnerChannel.QueueBindAsync(queue, exchange, routingKey, arguments);

        public QueueDeclareOk QueueDeclarePassive(string queue)
            => InnerChannel.QueueDeclarePassive(queue);

        public uint MessageCount(string queue)
            => InnerChannel.MessageCount(queue);

        public uint ConsumerCount(string queue)
            => InnerChannel.ConsumerCount(queue);

        public uint QueueDelete(string queue, bool ifUnused, bool ifEmpty)
        {
            uint result = InnerChannel.QueueDelete(queue, ifUnused, ifEmpty);
            _connection.DeleteRecordedQueue(queue, recordedEntitiesSemaphoreHeld: false);
            return result;
        }

        public async ValueTask<uint> QueueDeleteAsync(string queue, bool ifUnused, bool ifEmpty)
        {
            uint result = await InnerChannel.QueueDeleteAsync(queue, ifUnused, ifEmpty);
            await _connection.DeleteRecordedQueueAsync(queue, recordedEntitiesSemaphoreHeld: false)
                .ConfigureAwait(false);
            return result;
        }

        public void QueueDeleteNoWait(string queue, bool ifUnused, bool ifEmpty)
        {
            InnerChannel.QueueDeleteNoWait(queue, ifUnused, ifEmpty);
            _connection.DeleteRecordedQueue(queue, recordedEntitiesSemaphoreHeld: false);
        }

        public uint QueuePurge(string queue)
            => InnerChannel.QueuePurge(queue);

        public ValueTask<uint> QueuePurgeAsync(string queue)
            => InnerChannel.QueuePurgeAsync(queue);

        public void QueueUnbind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            var recordedBinding = new RecordedBinding(true, queue, exchange, routingKey, arguments);
            _connection.DeleteRecordedBinding(recordedBinding, recordedEntitiesSemaphoreHeld: false);
            _innerChannel.QueueUnbind(queue, exchange, routingKey, arguments);
            _connection.DeleteAutoDeleteExchange(exchange, recordedEntitiesSemaphoreHeld: false);
        }

        public async ValueTask QueueUnbindAsync(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfDisposed();
            var recordedBinding = new RecordedBinding(true, queue, exchange, routingKey, arguments);
            await _connection.DeleteRecordedBindingAsync(recordedBinding, recordedEntitiesSemaphoreHeld: false)
                .ConfigureAwait(false);
            await _innerChannel.QueueUnbindAsync(queue, exchange, routingKey, arguments)
                .ConfigureAwait(false);
            await _connection.DeleteAutoDeleteExchangeAsync(exchange, recordedEntitiesSemaphoreHeld: false)
                .ConfigureAwait(false);
        }

        public void TxCommit()
            => InnerChannel.TxCommit();

        public ValueTask TxCommitAsync()
            => InnerChannel.TxCommitAsync();

        public void TxRollback()
            => InnerChannel.TxRollback();

        public ValueTask TxRollbackAsync()
            => InnerChannel.TxRollbackAsync();

        public void TxSelect()
        {
            InnerChannel.TxSelect();
            _usesTransactions = true;
        }

        public ValueTask TxSelectAsync()
        {
            _usesTransactions = true;
            return InnerChannel.TxSelectAsync();
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
