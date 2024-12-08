// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.ConsumerDispatching
{
    internal abstract class ConsumerDispatcherChannelBase : ConsumerDispatcherBase, IConsumerDispatcher
    {
        protected readonly Impl.Channel _channel;
        protected readonly System.Threading.Channels.ChannelReader<WorkStruct> _reader;
        private readonly System.Threading.Channels.ChannelWriter<WorkStruct> _writer;
        private readonly Task _worker;
        private readonly ushort _concurrency;
        private long _isQuiescing;
        private bool _disposed;
        private readonly CancellationTokenSource _shutdownCts = new CancellationTokenSource();

        internal ConsumerDispatcherChannelBase(Impl.Channel channel, ushort concurrency)
        {
            _channel = channel;
            _concurrency = concurrency;

            var channelOpts = new System.Threading.Channels.UnboundedChannelOptions
            {
                SingleReader = _concurrency == 1,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            };

            var workChannel = System.Threading.Channels.Channel.CreateUnbounded<WorkStruct>(channelOpts);
            _reader = workChannel.Reader;
            _writer = workChannel.Writer;

            Func<Task> loopStart = ProcessChannelAsync;
            if (_concurrency == 1)
            {
                _worker = Task.Run(loopStart);
            }
            else
            {
                var tasks = new Task[_concurrency];
                for (int i = 0; i < _concurrency; i++)
                {
                    tasks[i] = Task.Run(loopStart);
                }
                _worker = Task.WhenAll(tasks);
            }
        }

        public bool IsShutdown => IsQuiescing;

        public ushort Concurrency => _concurrency;

        public async ValueTask HandleBasicConsumeOkAsync(IAsyncBasicConsumer consumer, string consumerTag, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (false == _disposed && false == IsQuiescing)
            {
                try
                {
                    AddConsumer(consumer, consumerTag);
                    WorkStruct work = WorkStruct.CreateConsumeOk(consumer, consumerTag, _shutdownCts);
                    await _writer.WriteAsync(work, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch
                {
                    _ = GetAndRemoveConsumer(consumerTag);
                    throw;
                }
            }
        }

        public async ValueTask HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered,
            string exchange, string routingKey, IReadOnlyBasicProperties basicProperties, RentedMemory body,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (false == _disposed && false == IsQuiescing)
            {
                IAsyncBasicConsumer consumer = GetConsumerOrDefault(consumerTag);
                var work = WorkStruct.CreateDeliver(consumer, consumerTag, deliveryTag, redelivered, exchange, routingKey, basicProperties, body, _shutdownCts);
                await _writer.WriteAsync(work, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        public async ValueTask HandleBasicCancelOkAsync(string consumerTag, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (false == _disposed && false == IsQuiescing)
            {
                IAsyncBasicConsumer consumer = GetAndRemoveConsumer(consumerTag);
                WorkStruct work = WorkStruct.CreateCancelOk(consumer, consumerTag, _shutdownCts);
                await _writer.WriteAsync(work, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        public async ValueTask HandleBasicCancelAsync(string consumerTag, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (false == _disposed && false == IsQuiescing)
            {
                IAsyncBasicConsumer consumer = GetAndRemoveConsumer(consumerTag);
                WorkStruct work = WorkStruct.CreateCancel(consumer, consumerTag, _shutdownCts);
                await _writer.WriteAsync(work, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        public void Quiesce()
        {
            if (IsQuiescing)
            {
                return;
            }

            Interlocked.Exchange(ref _isQuiescing, 1);
            try
            {
                _shutdownCts.Cancel();
            }
            catch
            {
                // ignore
            }
        }

        public async Task WaitForShutdownAsync(CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                return;
            }

            if (IsQuiescing)
            {
                try
                {
                    /*
                     * rabbitmq/rabbitmq-dotnet-client#1751
                     * Awaiting the work channel reader could deadlock - no idea why.
                     * Since we await the consumer dispatcher _worker task,
                     * that should suffice.
                     *
                     * await _reader.Completion.ConfigureAwait(false);
                     */
                    await _worker.WaitAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (AggregateException aex)
                {
                    AggregateException aexf = aex.Flatten();
                    bool foundUnexpectedException = false;
                    foreach (Exception innerAexf in aexf.InnerExceptions)
                    {
                        if (false == (innerAexf is OperationCanceledException))
                        {
                            foundUnexpectedException = true;
                            break;
                        }
                    }
                    if (foundUnexpectedException)
                    {
                        ESLog.Warn("consumer dispatcher task had unexpected exceptions (async)");
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }
            else
            {
                throw new InvalidOperationException("WaitForShutdownAsync called but _quiesce is false");
            }
        }

        protected bool IsQuiescing
        {
            get
            {
                return Interlocked.Read(ref _isQuiescing) == 1;
            }
        }

        protected sealed override void ShutdownConsumer(IAsyncBasicConsumer consumer, ShutdownEventArgs reason)
        {
            _writer.TryWrite(WorkStruct.CreateShutdown(consumer, reason, _shutdownCts));
        }

        protected override Task InternalShutdownAsync()
        {
            _writer.Complete();
            return _worker;
        }

        protected abstract Task ProcessChannelAsync();

        protected readonly struct WorkStruct : IDisposable
        {
            public readonly IAsyncBasicConsumer Consumer;
            public readonly string? ConsumerTag;
            public readonly ulong DeliveryTag;
            public readonly bool Redelivered;
            public readonly string? Exchange;
            public readonly string? RoutingKey;
            public readonly IReadOnlyBasicProperties? BasicProperties;
            public readonly RentedMemory Body;
            public readonly ShutdownEventArgs? Reason;
            public readonly WorkType WorkType;
            public readonly CancellationToken CancellationToken;
            private readonly CancellationTokenSource? _cancellationTokenSource;

            private WorkStruct(WorkType type, IAsyncBasicConsumer consumer, string consumerTag, CancellationToken cancellationToken)
                : this()
            {
                WorkType = type;
                Consumer = consumer;
                ConsumerTag = consumerTag;
                CancellationToken = cancellationToken;
                _cancellationTokenSource = null;
            }

            private WorkStruct(IAsyncBasicConsumer consumer, ShutdownEventArgs reason, CancellationTokenSource? cancellationTokenSource)
                : this()
            {
                WorkType = WorkType.Shutdown;
                Consumer = consumer;
                Reason = reason;
                CancellationToken = cancellationTokenSource?.Token ?? CancellationToken.None;
                this._cancellationTokenSource = cancellationTokenSource;
            }

            private WorkStruct(IAsyncBasicConsumer consumer, string consumerTag, ulong deliveryTag, bool redelivered,
                string exchange, string routingKey, IReadOnlyBasicProperties basicProperties, RentedMemory body,
                CancellationToken cancellationToken)
            {
                WorkType = WorkType.Deliver;
                Consumer = consumer;
                ConsumerTag = consumerTag;
                DeliveryTag = deliveryTag;
                Redelivered = redelivered;
                Exchange = exchange;
                RoutingKey = routingKey;
                BasicProperties = basicProperties;
                Body = body;
                Reason = null;
                CancellationToken = cancellationToken;
                _cancellationTokenSource = null;
            }

            public static WorkStruct CreateCancel(IAsyncBasicConsumer consumer, string consumerTag, CancellationTokenSource cancellationTokenSource)
            {
                return new WorkStruct(WorkType.Cancel, consumer, consumerTag, cancellationTokenSource.Token);
            }

            public static WorkStruct CreateCancelOk(IAsyncBasicConsumer consumer, string consumerTag, CancellationTokenSource cancellationTokenSource)
            {
                return new WorkStruct(WorkType.CancelOk, consumer, consumerTag, cancellationTokenSource.Token);
            }

            public static WorkStruct CreateConsumeOk(IAsyncBasicConsumer consumer, string consumerTag, CancellationTokenSource cancellationTokenSource)
            {
                return new WorkStruct(WorkType.ConsumeOk, consumer, consumerTag, cancellationTokenSource.Token);
            }

            public static WorkStruct CreateShutdown(IAsyncBasicConsumer consumer, ShutdownEventArgs reason, CancellationTokenSource cancellationTokenSource)
            {
                // Create a linked CTS so the shutdown args token reflects both dispatcher cancellation and any upstream token.
                CancellationTokenSource? linked = null;
                try
                {
                    if (reason.CancellationToken.CanBeCanceled)
                    {
                        linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, reason.CancellationToken);
                    }
                }
                catch
                {
                    linked = null;
                }

                CancellationToken token = linked?.Token ?? cancellationTokenSource.Token;
                ShutdownEventArgs argsWithToken = reason.Exception != null ?
                    new ShutdownEventArgs(reason.Initiator, reason.ReplyCode, reason.ReplyText, reason.Exception, token) :
                    new ShutdownEventArgs(reason.Initiator, reason.ReplyCode, reason.ReplyText, reason.ClassId, reason.MethodId, reason.Cause, token);

                return new WorkStruct(consumer, argsWithToken, linked);
            }

            public static WorkStruct CreateDeliver(IAsyncBasicConsumer consumer, string consumerTag, ulong deliveryTag, bool redelivered,
                string exchange, string routingKey, IReadOnlyBasicProperties basicProperties, RentedMemory body, CancellationTokenSource cancellationTokenSource)
            {
                return new WorkStruct(consumer, consumerTag, deliveryTag, redelivered,
                    exchange, routingKey, basicProperties, body, cancellationTokenSource.Token);
            }

            public void Dispose()
            {
                Body.Dispose();
                _cancellationTokenSource?.Dispose();
            }
        }

        protected enum WorkType : byte
        {
            Shutdown,
            Cancel,
            CancelOk,
            Deliver,
            ConsumeOk
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                try
                {
                    if (disposing)
                    {
                        Quiesce();
                        _shutdownCts.Dispose();
                    }
                }
                catch
                {
                    // CHOMP
                }
                finally
                {
                    _disposed = true;
                }
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
