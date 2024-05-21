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
        private readonly List<ConsumerTag> _recordedConsumerTags = new List<ConsumerTag>();

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

        public IEnumerable<ConsumerTag> ConsumerTags
        {
            get
            {
                ThrowIfDisposed();
                return _recordedConsumerTags.ToArray();
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

        public QueueName CurrentQueue => InnerChannel.CurrentQueue;

        internal async Task AutomaticallyRecoverAsync(AutorecoveringConnection conn, bool recoverConsumers,
            bool recordedEntitiesSemaphoreHeld, CancellationToken cancellationToken)
        {
            if (false == recordedEntitiesSemaphoreHeld)
            {
                throw new InvalidOperationException("recordedEntitiesSemaphore must be held");
            }

            ThrowIfDisposed();
            _connection = conn;

            RecoveryAwareChannel newChannel = await conn.CreateNonRecoveringChannelAsync(cancellationToken)
                .ConfigureAwait(false);
            newChannel.TakeOver(_innerChannel);

            if (_prefetchCountConsumer != 0)
            {
                await newChannel.BasicQosAsync(0, _prefetchCountConsumer, false, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (_prefetchCountGlobal != 0)
            {
                await newChannel.BasicQosAsync(0, _prefetchCountGlobal, true, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (_usesPublisherConfirms)
            {
                await newChannel.ConfirmSelectAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            if (_usesTransactions)
            {
                await newChannel.TxSelectAsync(cancellationToken)
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

        public async Task CloseAsync(ushort replyCode, string replyText, bool abort,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            try
            {
                await _innerChannel.CloseAsync(replyCode, replyText, abort, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                await _connection.DeleteRecordedChannelAsync(this,
                    channelsSemaphoreHeld: false, recordedEntitiesSemaphoreHeld: false)
                        .ConfigureAwait(false);
            }
        }

        public async Task CloseAsync(ShutdownEventArgs args, bool abort,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            try
            {
                await _innerChannel.CloseAsync(args, abort, cancellationToken)
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

            if (IsOpen)
            {
                throw new InvalidOperationException("CloseAsync/AbortAsync must be called prior to Dispose");
            }

            _recordedConsumerTags.Clear();
            _connection = null;
            _innerChannel = null;
            _disposed = true;
        }

        public ValueTask BasicAckAsync(ulong deliveryTag, bool multiple, CancellationToken cancellationToken)
            => InnerChannel.BasicAckAsync(deliveryTag, multiple, cancellationToken);

        public ValueTask BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue, CancellationToken cancellationToken)
            => InnerChannel.BasicNackAsync(deliveryTag, multiple, requeue, cancellationToken);

        public Task BasicRejectAsync(ulong deliveryTag, bool requeue, CancellationToken cancellationToken)
            => InnerChannel.BasicRejectAsync(deliveryTag, requeue, cancellationToken);

        public async Task BasicCancelAsync(ConsumerTag consumerTag, bool noWait, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            await _connection.DeleteRecordedConsumerAsync(consumerTag, recordedEntitiesSemaphoreHeld: false)
                .ConfigureAwait(false);
            await _innerChannel.BasicCancelAsync(consumerTag, noWait, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<ConsumerTag> BasicConsumeAsync(QueueName queue, bool autoAck, ConsumerTag consumerTag, bool noLocal, bool exclusive,
            IDictionary<string, object> arguments, IBasicConsumer consumer,
            CancellationToken cancellationToken)
        {
            ConsumerTag resultConsumerTag = await InnerChannel.BasicConsumeAsync(queue, autoAck, consumerTag, noLocal,
                exclusive, arguments, consumer, cancellationToken)
                .ConfigureAwait(false);
            var rc = new RecordedConsumer(channel: this, consumer: consumer, consumerTag: resultConsumerTag,
                queue: queue, autoAck: autoAck, exclusive: exclusive, arguments: arguments);
            await _connection.RecordConsumerAsync(rc, recordedEntitiesSemaphoreHeld: false)
                .ConfigureAwait(false);
            _recordedConsumerTags.Add(resultConsumerTag);
            return resultConsumerTag;
        }

        public ValueTask<BasicGetResult> BasicGetAsync(QueueName queue, bool autoAck, CancellationToken cancellationToken)
            => InnerChannel.BasicGetAsync(queue, autoAck, cancellationToken);

        public ValueTask BasicPublishAsync<TProperties>(ExchangeName exchange, RoutingKey routingKey, TProperties basicProperties,
            ReadOnlyMemory<byte> body, bool mandatory,
            CancellationToken cancellationToken)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
            => InnerChannel.BasicPublishAsync(exchange, routingKey, basicProperties, body, mandatory, cancellationToken);

        public Task BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global,
            CancellationToken cancellationToken)
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

            return _innerChannel.BasicQosAsync(prefetchSize, prefetchCount, global, cancellationToken);
        }

        public async Task ConfirmSelectAsync(CancellationToken cancellationToken)
        {
            await InnerChannel.ConfirmSelectAsync(cancellationToken)
                .ConfigureAwait(false);
            _usesPublisherConfirms = true;
        }

        public async Task ExchangeBindAsync(ExchangeName destination, ExchangeName source, RoutingKey routingKey,
            IDictionary<string, object> arguments, bool noWait,
            CancellationToken cancellationToken)
        {
            await InnerChannel.ExchangeBindAsync(destination, source, routingKey, arguments, noWait, cancellationToken)
                .ConfigureAwait(false);
            var recordedBinding = new RecordedBinding(false, destination, source, routingKey, arguments);
            await _connection.RecordBindingAsync(recordedBinding, recordedEntitiesSemaphoreHeld: false)
                .ConfigureAwait(false);
        }

        public Task ExchangeDeclarePassiveAsync(ExchangeName exchange, CancellationToken cancellationToken)
            => InnerChannel.ExchangeDeclarePassiveAsync(exchange, cancellationToken);

        public async Task ExchangeDeclareAsync(ExchangeName exchange, ExchangeType type, bool durable, bool autoDelete,
            IDictionary<string, object> arguments, bool passive, bool noWait,
            CancellationToken cancellationToken)
        {
            await InnerChannel.ExchangeDeclareAsync(exchange, type, durable, autoDelete, arguments, passive, noWait, cancellationToken)
                .ConfigureAwait(false);
            if (false == passive)
            {
                var recordedExchange = new RecordedExchange(exchange, type, durable, autoDelete, arguments);
                await _connection.RecordExchangeAsync(recordedExchange, recordedEntitiesSemaphoreHeld: false)
                    .ConfigureAwait(false);
            }
        }

        public async Task ExchangeDeleteAsync(ExchangeName exchange, bool ifUnused, bool noWait,
            CancellationToken cancellationToken)
        {
            await InnerChannel.ExchangeDeleteAsync(exchange, ifUnused, noWait, cancellationToken)
                .ConfigureAwait(false);
            await _connection.DeleteRecordedExchangeAsync(exchange, recordedEntitiesSemaphoreHeld: false, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task ExchangeUnbindAsync(ExchangeName destination, ExchangeName source, RoutingKey routingKey,
            IDictionary<string, object> arguments, bool noWait,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            var recordedBinding = new RecordedBinding(false, destination, source, routingKey, arguments);
            await _connection.DeleteRecordedBindingAsync(recordedBinding, recordedEntitiesSemaphoreHeld: false, cancellationToken)
                .ConfigureAwait(false);
            await InnerChannel.ExchangeUnbindAsync(destination, source, routingKey, arguments, noWait, cancellationToken)
                .ConfigureAwait(false);
            await _connection.DeleteAutoDeleteExchangeAsync(source, recordedEntitiesSemaphoreHeld: false, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task QueueBindAsync(QueueName queue, ExchangeName exchange, RoutingKey routingKey,
            IDictionary<string, object> arguments, bool noWait,
            CancellationToken cancellationToken)
        {
            await InnerChannel.QueueBindAsync(queue, exchange, routingKey, arguments, noWait, cancellationToken)
                .ConfigureAwait(false);
            var recordedBinding = new RecordedBinding(true, queue, exchange, routingKey, arguments);
            await _connection.RecordBindingAsync(recordedBinding, recordedEntitiesSemaphoreHeld: false)
                .ConfigureAwait(false);
        }

        public Task<QueueDeclareOk> QueueDeclarePassiveAsync(QueueName queue, CancellationToken cancellationToken)
        {
            return QueueDeclareAsync(queue: queue, passive: true,
                durable: false, exclusive: false, autoDelete: false,
                arguments: null, noWait: false, cancellationToken: cancellationToken);
        }

        public async Task<QueueDeclareOk> QueueDeclareAsync(QueueName queue, bool durable, bool exclusive, bool autoDelete,
            IDictionary<string, object> arguments, bool passive, bool noWait,
            CancellationToken cancellationToken)
        {
            QueueDeclareOk result = await InnerChannel.QueueDeclareAsync(queue, durable, exclusive, autoDelete, arguments, passive, noWait, cancellationToken)
                .ConfigureAwait(false);
            if (false == passive)
            {
                var recordedQueue = new RecordedQueue((QueueName)result, queue.IsEmpty, durable, exclusive, autoDelete, arguments);
                await _connection.RecordQueueAsync(recordedQueue, recordedEntitiesSemaphoreHeld: false, cancellationToken)
                    .ConfigureAwait(false);
            }
            return result;
        }

        public Task<uint> MessageCountAsync(QueueName queue,
            CancellationToken cancellationToken)
            => InnerChannel.MessageCountAsync(queue, cancellationToken);

        public Task<uint> ConsumerCountAsync(QueueName queue,
            CancellationToken cancellationToken)
            => InnerChannel.ConsumerCountAsync(queue, cancellationToken);

        public async Task<uint> QueueDeleteAsync(QueueName queue, bool ifUnused, bool ifEmpty, bool noWait,
            CancellationToken cancellationToken)
        {
            uint result = await InnerChannel.QueueDeleteAsync(queue, ifUnused, ifEmpty, noWait, cancellationToken)
                .ConfigureAwait(false);
            await _connection.DeleteRecordedQueueAsync(queue, recordedEntitiesSemaphoreHeld: false, cancellationToken)
                .ConfigureAwait(false);
            return result;
        }

        public Task<uint> QueuePurgeAsync(QueueName queue, CancellationToken cancellationToken)
            => InnerChannel.QueuePurgeAsync(queue, cancellationToken);

        public async Task QueueUnbindAsync(QueueName queue, ExchangeName exchange, RoutingKey routingKey,
            IDictionary<string, object> arguments,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            var recordedBinding = new RecordedBinding(true, queue, exchange, routingKey, arguments);
            await _connection.DeleteRecordedBindingAsync(recordedBinding, recordedEntitiesSemaphoreHeld: false, cancellationToken)
                .ConfigureAwait(false);
            await _innerChannel.QueueUnbindAsync(queue, exchange, routingKey, arguments, cancellationToken)
                .ConfigureAwait(false);
            await _connection.DeleteAutoDeleteExchangeAsync(exchange, recordedEntitiesSemaphoreHeld: false, cancellationToken)
                .ConfigureAwait(false);
        }

        public Task TxCommitAsync(CancellationToken cancellationToken)
            => InnerChannel.TxCommitAsync(cancellationToken);


        public Task TxRollbackAsync(CancellationToken cancellationToken)
            => InnerChannel.TxRollbackAsync(cancellationToken);

        public Task TxSelectAsync(CancellationToken cancellationToken)
        {
            _usesTransactions = true;
            return InnerChannel.TxSelectAsync(cancellationToken);
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
