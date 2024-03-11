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
        private readonly List<string> _recordedConsumerTags = new List<string>();

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

        public Task CloseAsync(ushort replyCode, string replyText, bool abort)
        {
            ThrowIfDisposed();
            var args = new ShutdownEventArgs(ShutdownInitiator.Library, replyCode, replyText);
            return CloseAsync(args, abort);
        }

        public async Task CloseAsync(ShutdownEventArgs args, bool abort)
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

            if (IsOpen)
            {
                this.AbortAsync().GetAwaiter().GetResult();
            }

            _recordedConsumerTags.Clear();
            _connection = null;
            _innerChannel = null;
            _disposed = true;
        }

        public ValueTask BasicAckAsync(ulong deliveryTag, bool multiple)
            => InnerChannel.BasicAckAsync(deliveryTag, multiple);

        public Task BasicCancelAsync(string consumerTag, bool noWait)
        {
            ThrowIfDisposed();
            _connection.DeleteRecordedConsumer(consumerTag, recordedEntitiesSemaphoreHeld: false);
            return _innerChannel.BasicCancelAsync(consumerTag, noWait);
        }

        public async Task<string> BasicConsumeAsync(string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive,
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

        public ValueTask<BasicGetResult> BasicGetAsync(string queue, bool autoAck)
            => InnerChannel.BasicGetAsync(queue, autoAck);

        public ValueTask BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue)
            => InnerChannel.BasicNackAsync(deliveryTag, multiple, requeue);

        public ValueTask BasicPublishAsync<TProperties>(string exchange, string routingKey, TProperties basicProperties, ReadOnlyMemory<byte> body, bool mandatory)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
            => InnerChannel.BasicPublishAsync(exchange, routingKey, basicProperties, body, mandatory);

        public ValueTask BasicPublishAsync<TProperties>(CachedString exchange, CachedString routingKey, TProperties basicProperties, ReadOnlyMemory<byte> body, bool mandatory)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
            => InnerChannel.BasicPublishAsync(exchange, routingKey, basicProperties, body, mandatory);

        public Task BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global)
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

        public Task BasicRejectAsync(ulong deliveryTag, bool requeue)
            => InnerChannel.BasicRejectAsync(deliveryTag, requeue);

        public async Task ConfirmSelectAsync()
        {
            await InnerChannel.ConfirmSelectAsync()
                .ConfigureAwait(false);
            _usesPublisherConfirms = true;
        }

        public async Task ExchangeBindAsync(string destination, string source, string routingKey,
            IDictionary<string, object> arguments, bool noWait)
        {
            await InnerChannel.ExchangeBindAsync(destination, source, routingKey, arguments, noWait)
                .ConfigureAwait(false);
            var recordedBinding = new RecordedBinding(false, destination, source, routingKey, arguments);
            await _connection.RecordBindingAsync(recordedBinding, recordedEntitiesSemaphoreHeld: false)
                .ConfigureAwait(false);
        }

        public Task ExchangeDeclarePassiveAsync(string exchange)
            => InnerChannel.ExchangeDeclarePassiveAsync(exchange);

        public async Task ExchangeDeclareAsync(string exchange, string type, bool durable, bool autoDelete,
            IDictionary<string, object> arguments, bool passive, bool noWait)
        {
            await InnerChannel.ExchangeDeclareAsync(exchange, type, durable, autoDelete, arguments, passive, noWait)
                .ConfigureAwait(false);
            if (false == passive)
            {
                var recordedExchange = new RecordedExchange(exchange, type, durable, autoDelete, arguments);
                await _connection.RecordExchangeAsync(recordedExchange, recordedEntitiesSemaphoreHeld: false)
                    .ConfigureAwait(false);
            }
        }

        public async Task ExchangeDeleteAsync(string exchange, bool ifUnused, bool noWait)
        {
            await InnerChannel.ExchangeDeleteAsync(exchange, ifUnused, noWait)
                .ConfigureAwait(false);
            await _connection.DeleteRecordedExchangeAsync(exchange, recordedEntitiesSemaphoreHeld: false)
                .ConfigureAwait(false);
        }

        public async Task ExchangeUnbindAsync(string destination, string source, string routingKey,
            IDictionary<string, object> arguments, bool noWait)
        {
            ThrowIfDisposed();
            var recordedBinding = new RecordedBinding(false, destination, source, routingKey, arguments);
            await _connection.DeleteRecordedBindingAsync(recordedBinding, recordedEntitiesSemaphoreHeld: false)
                .ConfigureAwait(false);
            await InnerChannel.ExchangeUnbindAsync(destination, source, routingKey, arguments, noWait)
                .ConfigureAwait(false);
            await _connection.DeleteAutoDeleteExchangeAsync(source, recordedEntitiesSemaphoreHeld: false)
                .ConfigureAwait(false);
        }

        public async Task QueueBindAsync(string queue, string exchange, string routingKey,
            IDictionary<string, object> arguments, bool noWait)
        {
            await InnerChannel.QueueBindAsync(queue, exchange, routingKey, arguments, noWait)
                .ConfigureAwait(false);
            var recordedBinding = new RecordedBinding(true, queue, exchange, routingKey, arguments);
            await _connection.RecordBindingAsync(recordedBinding, recordedEntitiesSemaphoreHeld: false)
                .ConfigureAwait(false);
        }

        public Task<QueueDeclareOk> QueueDeclarePassiveAsync(string queue)
        {
            return QueueDeclareAsync(queue: queue, passive: true,
                durable: false, exclusive: false, autoDelete: false,
                arguments: null, noWait: false);
        }

        public async Task<QueueDeclareOk> QueueDeclareAsync(string queue, bool durable, bool exclusive, bool autoDelete,
            IDictionary<string, object> arguments, bool passive, bool noWait)
        {
            QueueDeclareOk result = await InnerChannel.QueueDeclareAsync(queue, durable, exclusive, autoDelete, arguments, passive, noWait)
                .ConfigureAwait(false);
            if (false == passive)
            {
                var recordedQueue = new RecordedQueue(result.QueueName, queue.Length == 0, durable, exclusive, autoDelete, arguments);
                await _connection.RecordQueueAsync(recordedQueue, recordedEntitiesSemaphoreHeld: false)
                    .ConfigureAwait(false);
            }
            return result;
        }

        public Task<uint> MessageCountAsync(string queue)
            => InnerChannel.MessageCountAsync(queue);

        public Task<uint> ConsumerCountAsync(string queue)
            => InnerChannel.ConsumerCountAsync(queue);

        public async Task<uint> QueueDeleteAsync(string queue, bool ifUnused, bool ifEmpty, bool noWait)
        {
            uint result = await InnerChannel.QueueDeleteAsync(queue, ifUnused, ifEmpty, noWait);
            await _connection.DeleteRecordedQueueAsync(queue, recordedEntitiesSemaphoreHeld: false)
                .ConfigureAwait(false);
            return result;
        }

        public Task<uint> QueuePurgeAsync(string queue)
            => InnerChannel.QueuePurgeAsync(queue);

        public async Task QueueUnbindAsync(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
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

        public Task TxCommitAsync()
            => InnerChannel.TxCommitAsync();


        public Task TxRollbackAsync()
            => InnerChannel.TxRollbackAsync();

        public Task TxSelectAsync()
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
