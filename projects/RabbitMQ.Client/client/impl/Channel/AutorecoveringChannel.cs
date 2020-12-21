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
using System.Threading.Tasks;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.client.impl.Channel
{
    #nullable enable
    internal sealed class AutorecoveringChannel : IChannel
    {
        private AutorecoveringConnection _connection;
        private RecoveryAwareChannel _delegate;
        private bool _disposed;

        private ushort _prefetchCountConsumer;
        private ushort _prefetchCountGlobal;
        private bool _usesPublisherConfirms;
        private bool _usesTransactions;

        public TimeSpan ContinuationTimeout
        {
            get => NonDisposedDelegate.ContinuationTimeout;
            set => NonDisposedDelegate.ContinuationTimeout = value;
        }

        // only exist due to tests
        internal IConsumerDispatcher ConsumerDispatcher => NonDisposedDelegate.ConsumerDispatcher;

        public AutorecoveringChannel(AutorecoveringConnection conn, RecoveryAwareChannel @delegate)
        {
            _connection = conn;
            _delegate = @delegate;
        }

        public event Action<ulong, bool, bool>? PublishTagAcknowledged
        {
            add => NonDisposedDelegate.PublishTagAcknowledged += value;
            remove => NonDisposedDelegate.PublishTagAcknowledged -= value;
        }

        public event Action<ulong>? NewPublishTagUsed
        {
            add => NonDisposedDelegate.NewPublishTagUsed += value;
            remove => NonDisposedDelegate.NewPublishTagUsed -= value;
        }

        public event MessageDeliveryFailedDelegate? MessageDeliveryFailed
        {
            add => NonDisposedDelegate.MessageDeliveryFailed += value;
            remove => NonDisposedDelegate.MessageDeliveryFailed -= value;
        }

        public event Action<Exception, Dictionary<string, object>?>? UnhandledExceptionOccurred
        {
            add => NonDisposedDelegate.UnhandledExceptionOccurred += value;
            remove => NonDisposedDelegate.UnhandledExceptionOccurred -= value;
        }

        public event Action<bool>? FlowControlChanged
        {
            add { NonDisposedDelegate.FlowControlChanged += value; }
            remove { NonDisposedDelegate.FlowControlChanged -= value; }
        }

        public event Action<ShutdownEventArgs>? Shutdown
        {
            add => NonDisposedDelegate.Shutdown += value;
            remove => NonDisposedDelegate.Shutdown -= value;
        }

        public event EventHandler<EventArgs>? Recovery;

        public int ChannelNumber => NonDisposedDelegate.ChannelNumber;
        public bool IsOpen => NonDisposedDelegate.IsOpen;
        public ShutdownEventArgs? CloseReason => NonDisposedDelegate.CloseReason;

        internal RecoveryAwareChannel NonDisposedDelegate => !_disposed ? _delegate : ThrowDisposed();

        private RecoveryAwareChannel ThrowDisposed()
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        public async ValueTask AutomaticallyRecoverAsync(AutorecoveringConnection conn)
        {
            ThrowIfDisposed();
            _connection = conn;
            var defunctChannel = _delegate;

            _delegate = await conn.CreateNonRecoveringChannelAsync().ConfigureAwait(false);
            _delegate.TakeOverChannel(defunctChannel!);
            await RecoverStateAsync().ConfigureAwait(false);
            RunRecoveryEventHandlers();
        }

        public ValueTask SetQosAsync(uint prefetchSize, ushort prefetchCount, bool global)
        {
            // TODO 8.0 - Missing Tests
            ThrowIfDisposed();
            if (global)
            {
                _prefetchCountGlobal = prefetchCount;
            }
            else
            {
                _prefetchCountConsumer = prefetchCount;
            }

            return _delegate.SetQosAsync(prefetchSize, prefetchCount, global);
        }

        public ValueTask AckMessageAsync(ulong deliveryTag, bool multiple)
            => NonDisposedDelegate.AckMessageAsync(deliveryTag, multiple);

        public ValueTask NackMessageAsync(ulong deliveryTag, bool multiple, bool requeue)
            => NonDisposedDelegate.NackMessageAsync(deliveryTag, multiple, requeue);

        public async ValueTask<string> ActivateConsumerAsync(IBasicConsumer consumer, string queue, bool autoAck, string consumerTag = "", bool noLocal = false, bool exclusive = false, IDictionary<string, object>? arguments = null)
        {
            string result = await NonDisposedDelegate.ActivateConsumerAsync(consumer, queue, autoAck, consumerTag, noLocal, exclusive, arguments).ConfigureAwait(false);
            RecordedConsumer rc = new RecordedConsumer(this, consumer, queue, autoAck, result, exclusive, arguments);
            _connection.RecordConsumer(result, rc);
            return result;
        }

        public ValueTask CancelConsumerAsync(string consumerTag, bool waitForConfirmation = true)
        {
            ThrowIfDisposed();
            RecordedConsumer cons = _connection.DeleteRecordedConsumer(consumerTag);
            if (cons != null)
            {
                _connection.MaybeDeleteRecordedAutoDeleteQueue(cons.Queue);
            }

            return _delegate.CancelConsumerAsync(consumerTag, waitForConfirmation);
        }

        public ValueTask<SingleMessageRetrieval> RetrieveSingleMessageAsync(string queue, bool autoAck)
            => NonDisposedDelegate.RetrieveSingleMessageAsync(queue, autoAck);

        public ValueTask PublishMessageAsync(string exchange, string routingKey, IBasicProperties? basicProperties, ReadOnlyMemory<byte> body, bool mandatory = false)
            => NonDisposedDelegate.PublishMessageAsync(exchange, routingKey, basicProperties, body, mandatory);

        public ValueTask PublishBatchAsync(MessageBatch batch)
            => NonDisposedDelegate.PublishBatchAsync(batch);

        public async ValueTask ActivatePublishTagsAsync()
        {
            await NonDisposedDelegate.ActivatePublishTagsAsync().ConfigureAwait(false);
            _usesPublisherConfirms = true;
        }

        public async ValueTask DeclareExchangeAsync(string exchange, string type, bool durable, bool autoDelete, bool throwOnMismatch = true, IDictionary<string, object>? arguments = null, bool waitForConfirmation = true)
        {
            await NonDisposedDelegate.DeclareExchangeAsync(exchange, type, durable, autoDelete, throwOnMismatch, arguments, waitForConfirmation).ConfigureAwait(false);
            RecordedExchange rx = new RecordedExchange(this, exchange, type, durable, autoDelete, arguments);
            _connection.RecordExchange(exchange, rx);
        }

        public ValueTask DeleteExchangeAsync(string exchange, bool ifUnused = false, bool waitForConfirmation = true)
        {
            ThrowIfDisposed();
            _connection.DeleteRecordedExchange(exchange);
            return _delegate.DeleteExchangeAsync(exchange, ifUnused, waitForConfirmation);
        }

        public async ValueTask BindExchangeAsync(string destination, string source, string routingKey, IDictionary<string, object>? arguments = null, bool waitForConfirmation = true)
        {
            await NonDisposedDelegate.BindExchangeAsync(destination, source, routingKey, arguments, waitForConfirmation).ConfigureAwait(false);
            RecordedBinding eb = new RecordedExchangeBinding(this, destination, source, routingKey, arguments);
            _connection.RecordBinding(eb);
        }

        public ValueTask UnbindExchangeAsync(string destination, string source, string routingKey, IDictionary<string, object>? arguments = null, bool waitForConfirmation = true)
        {
            ThrowIfDisposed();
            RecordedBinding eb = new RecordedExchangeBinding(this, destination, source, routingKey, arguments);
            _connection.DeleteRecordedBinding(eb);
            _connection.MaybeDeleteRecordedAutoDeleteExchange(source);
            return _delegate.UnbindExchangeAsync(destination, source, routingKey, arguments, waitForConfirmation);
        }

        public async ValueTask<(string QueueName, uint MessageCount, uint ConsumerCount)> DeclareQueueAsync(string queue,
            bool durable, bool exclusive, bool autoDelete, bool throwOnMismatch = true, IDictionary<string, object>? arguments = null)
        {
            var result = await NonDisposedDelegate.DeclareQueueAsync(queue, durable, exclusive, autoDelete, throwOnMismatch, arguments).ConfigureAwait(false);
            RecordedQueue rq = new RecordedQueue(this, result.QueueName, queue.Length == 0, durable, exclusive, autoDelete, arguments);
            _connection.RecordQueue(result.QueueName, rq);
            return result;
        }

        public async ValueTask DeclareQueueWithoutConfirmationAsync(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object>? arguments = null)
        {
            await NonDisposedDelegate.DeclareQueueWithoutConfirmationAsync(queue, durable, exclusive, autoDelete, arguments).ConfigureAwait(false);
            RecordedQueue rq = new RecordedQueue(this, queue, queue.Length == 0, durable, exclusive, autoDelete, arguments);
            _connection.RecordQueue(queue, rq);
        }

        public ValueTask<uint> DeleteQueueAsync(string queue, bool ifUnused = false, bool ifEmpty = false)
        {
            _connection.DeleteRecordedQueue(queue);
            return NonDisposedDelegate.DeleteQueueAsync(queue, ifUnused, ifEmpty);
        }

        public ValueTask DeleteQueueWithoutConfirmationAsync(string queue, bool ifUnused = false, bool ifEmpty = false)
        {
            _connection.DeleteRecordedQueue(queue);
            return NonDisposedDelegate.DeleteQueueWithoutConfirmationAsync(queue, ifUnused, ifEmpty);
        }

        public async ValueTask BindQueueAsync(string queue, string exchange, string routingKey, IDictionary<string, object>? arguments = null, bool waitForConfirmation = true)
        {
            await NonDisposedDelegate.BindQueueAsync(queue, exchange, routingKey, arguments, waitForConfirmation).ConfigureAwait(false);
            RecordedBinding qb = new RecordedQueueBinding(this, queue, exchange, routingKey, arguments);
            _connection.RecordBinding(qb);
        }

        public ValueTask UnbindQueueAsync(string queue, string exchange, string routingKey, IDictionary<string, object>? arguments = null)
        {
            ThrowIfDisposed();
            RecordedBinding qb = new RecordedQueueBinding(this, queue, exchange, routingKey, arguments);
            _connection.DeleteRecordedBinding(qb);
            _connection.MaybeDeleteRecordedAutoDeleteExchange(exchange);
            return _delegate.UnbindQueueAsync(queue, exchange, routingKey, arguments);
        }

        public ValueTask<uint> PurgeQueueAsync(string queue)
            => NonDisposedDelegate.PurgeQueueAsync(queue);

        public async ValueTask ActivateTransactionsAsync()
        {
            await NonDisposedDelegate.ActivateTransactionsAsync().ConfigureAwait(false);
            _usesTransactions = true;
        }

        public ValueTask CommitTransactionAsync()
            => NonDisposedDelegate.CommitTransactionAsync();

        public ValueTask RollbackTransactionAsync()
            => NonDisposedDelegate.RollbackTransactionAsync();

        public ValueTask ResendUnackedMessages(bool requeue)
            => NonDisposedDelegate.ResendUnackedMessages(requeue);

        public ValueTask CloseAsync(ushort replyCode, string replyText)
        {
            _connection.UnregisterChannel(this);
            return NonDisposedDelegate.CloseAsync(replyCode, replyText);
        }

        public ValueTask AbortAsync(ushort replyCode, string replyText)
        {
            _connection.UnregisterChannel(this);
            return NonDisposedDelegate.AbortAsync(replyCode, replyText);
        }

        public bool WaitForConfirms(TimeSpan timeout) => NonDisposedDelegate.WaitForConfirms(timeout);

        public bool WaitForConfirms() => NonDisposedDelegate.WaitForConfirms();

        public void WaitForConfirmsOrDie() => NonDisposedDelegate.WaitForConfirmsOrDie();

        public void WaitForConfirmsOrDie(TimeSpan timeout) => NonDisposedDelegate.WaitForConfirmsOrDie(timeout);

        private async ValueTask RecoverStateAsync()
        {
            if (_prefetchCountConsumer != 0)
            {
                await SetQosAsync(0,  _prefetchCountConsumer, false).ConfigureAwait(false);
            }

            if (_prefetchCountGlobal != 0)
            {
                await SetQosAsync(0,  _prefetchCountGlobal, true).ConfigureAwait(false);
            }

            if (_usesPublisherConfirms)
            {
                await ActivatePublishTagsAsync().ConfigureAwait(false);
            }

            if (_usesTransactions)
            {
                await ActivateTransactionsAsync().ConfigureAwait(false);
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
                    _delegate.OnUnhandledExceptionOccurred(e, "OnModelRecovery");
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                ThrowDisposed();
            }
        }

        public override string? ToString() => NonDisposedDelegate.ToString();

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            await NonDisposedDelegate.DisposeAsync().ConfigureAwait(false);
            _disposed = true;
        }

        void IDisposable.Dispose()
        {
            DisposeAsync().GetAwaiter().GetResult();
        }
    }
}
