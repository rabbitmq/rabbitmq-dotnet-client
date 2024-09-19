﻿// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.ConsumerDispatching
{
    internal abstract class ConsumerDispatcherChannelBase : ConsumerDispatcherBase, IConsumerDispatcher
    {
        protected readonly ChannelBase _channel;
        protected readonly ChannelReader<WorkStruct> _reader;
        private readonly ChannelWriter<WorkStruct> _writer;
        private readonly Task _worker;
        private readonly ushort _concurrency;
        private bool _quiesce = false;
        private bool _disposed;

        internal ConsumerDispatcherChannelBase(ChannelBase channel, ushort concurrency)
        {
            _channel = channel;
            _concurrency = concurrency;
            var workChannel = Channel.CreateUnbounded<WorkStruct>(new UnboundedChannelOptions
            {
                SingleReader = _concurrency == 1,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
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

        public bool IsShutdown => _quiesce;

        public ushort Concurrency => _concurrency;

        public ValueTask HandleBasicConsumeOkAsync(IAsyncBasicConsumer consumer, string consumerTag, CancellationToken cancellationToken)
        {
            if (false == _disposed && false == _quiesce)
            {
                AddConsumer(consumer, consumerTag);
                WorkStruct work = WorkStruct.CreateConsumeOk(consumer, consumerTag);
                return _writer.WriteAsync(work, cancellationToken);
            }
            else
            {
                return default;
            }
        }

        public ValueTask HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered,
            string exchange, string routingKey, IReadOnlyBasicProperties basicProperties, RentedMemory body,
            CancellationToken cancellationToken)
        {
            if (false == _disposed && false == _quiesce)
            {
                IAsyncBasicConsumer consumer = GetConsumerOrDefault(consumerTag);
                var work = WorkStruct.CreateDeliver(consumer, consumerTag, deliveryTag, redelivered, exchange, routingKey, basicProperties, body);
                return _writer.WriteAsync(work, cancellationToken);
            }
            else
            {
                return default;
            }
        }

        public ValueTask HandleBasicCancelOkAsync(string consumerTag, CancellationToken cancellationToken)
        {
            if (false == _disposed && false == _quiesce)
            {
                IAsyncBasicConsumer consumer = GetAndRemoveConsumer(consumerTag);
                WorkStruct work = WorkStruct.CreateCancelOk(consumer, consumerTag);
                return _writer.WriteAsync(work, cancellationToken);
            }
            else
            {
                return default;
            }
        }

        public ValueTask HandleBasicCancelAsync(string consumerTag, CancellationToken cancellationToken)
        {
            if (false == _disposed && false == _quiesce)
            {
                IAsyncBasicConsumer consumer = GetAndRemoveConsumer(consumerTag);
                WorkStruct work = WorkStruct.CreateCancel(consumer, consumerTag);
                return _writer.WriteAsync(work, cancellationToken);
            }
            else
            {
                return default;
            }
        }

        public void Quiesce()
        {
            _quiesce = true;
        }

        public async Task WaitForShutdownAsync()
        {
            if (_disposed)
            {
                return;
            }

            if (_quiesce)
            {
                try
                {
                    await _reader.Completion
                        .ConfigureAwait(false);
                    await _worker
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

        protected sealed override void ShutdownConsumer(IAsyncBasicConsumer consumer, ShutdownEventArgs reason)
        {
            _writer.TryWrite(WorkStruct.CreateShutdown(consumer, reason));
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

            private WorkStruct(WorkType type, IAsyncBasicConsumer consumer, string consumerTag)
                : this()
            {
                WorkType = type;
                Consumer = consumer;
                ConsumerTag = consumerTag;
            }

            private WorkStruct(IAsyncBasicConsumer consumer, ShutdownEventArgs reason)
                : this()
            {
                WorkType = WorkType.Shutdown;
                Consumer = consumer;
                Reason = reason;
            }

            private WorkStruct(IAsyncBasicConsumer consumer, string consumerTag, ulong deliveryTag, bool redelivered,
                string exchange, string routingKey, IReadOnlyBasicProperties basicProperties, RentedMemory body)
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
                Reason = default;
            }

            public static WorkStruct CreateCancel(IAsyncBasicConsumer consumer, string consumerTag)
            {
                return new WorkStruct(WorkType.Cancel, consumer, consumerTag);
            }

            public static WorkStruct CreateCancelOk(IAsyncBasicConsumer consumer, string consumerTag)
            {
                return new WorkStruct(WorkType.CancelOk, consumer, consumerTag);
            }

            public static WorkStruct CreateConsumeOk(IAsyncBasicConsumer consumer, string consumerTag)
            {
                return new WorkStruct(WorkType.ConsumeOk, consumer, consumerTag);
            }

            public static WorkStruct CreateShutdown(IAsyncBasicConsumer consumer, ShutdownEventArgs reason)
            {
                return new WorkStruct(consumer, reason);
            }

            public static WorkStruct CreateDeliver(IAsyncBasicConsumer consumer, string consumerTag, ulong deliveryTag, bool redelivered,
                string exchange, string routingKey, IReadOnlyBasicProperties basicProperties, RentedMemory body)
            {
                return new WorkStruct(consumer, consumerTag, deliveryTag, redelivered,
                    exchange, routingKey, basicProperties, body);
            }

            public void Dispose() => Body.Dispose();
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
