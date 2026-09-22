// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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

        /*
         * Captured once: CancellationTokenSource.Token throws ObjectDisposedException after the
         * source is disposed, so reading it per work item let a Dispose() racing an inbound frame
         * tear down the whole connection. Issue #1988.
         */
        private readonly CancellationToken _shutdownToken;

        internal ConsumerDispatcherChannelBase(Impl.Channel channel, ushort concurrency)
        {
            _channel = channel;

            /*
             * Zero would build no reader loops at all, so nothing would ever drain the work channel:
             * consumers would register successfully and never fire. The guard is here rather than at
             * the callers because this is the type whose invariant it is, and callers can bypass the
             * options layer entirely - the benchmarks construct a dispatcher directly.
             *
             * See docs/internal/consumer-dispatch-concurrency.md and #2035.
             */
            _concurrency = concurrency == 0 ? InternalConstants.MinConsumerDispatchConcurrency : concurrency;
            _shutdownToken = _shutdownCts.Token;

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

        /*
         * The guard and the write are not atomic, so drop the work item rather than let
         * ChannelClosedException unwind into the frame-receive loop and tear down the connection.
         * The delivery path owns the pooled body once TakeoverBody() has cleared cmd.Body upstream.
         * These catches cover only the rare exits; the guard above drops far more, which is issue
         * #2039. See docs/internal/consumer-dispatch-concurrency.md.
         */
        public async ValueTask HandleBasicConsumeOkAsync(IAsyncBasicConsumer consumer, string consumerTag, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (false == _disposed && false == IsQuiescing)
            {
                try
                {
                    AddConsumer(consumer, consumerTag);
                    WorkStruct work = WorkStruct.CreateConsumeOk(consumer, consumerTag, _shutdownToken);
                    await _writer.WriteAsync(work, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (System.Threading.Channels.ChannelClosedException)
                {
                    // The dispatcher was disposed after the check above; drop the registration.
                    _ = GetAndRemoveConsumer(consumerTag);
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
                var work = WorkStruct.CreateDeliver(consumer, consumerTag, deliveryTag, redelivered, exchange, routingKey, basicProperties, body, _shutdownToken);
                try
                {
                    await _writer.WriteAsync(work, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (System.Threading.Channels.ChannelClosedException)
                {
                    // Nothing will drain this item, so return its pooled body to the pool here.
                    work.Dispose();
                }
                catch (OperationCanceledException)
                {
                    /*
                     * WriteAsync observes the token before the channel's completion, so ordinary
                     * teardown lands here rather than above. The item never reaches a consumer, so
                     * return its body; rethrow, because cancellation propagated before this catch.
                     */
                    work.Dispose();
                    throw;
                }
            }
        }

        public async ValueTask HandleBasicCancelOkAsync(string consumerTag, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (false == _disposed && false == IsQuiescing)
            {
                IAsyncBasicConsumer consumer = GetAndRemoveConsumer(consumerTag);
                WorkStruct work = WorkStruct.CreateCancelOk(consumer, consumerTag, _shutdownToken);
                try
                {
                    await _writer.WriteAsync(work, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (System.Threading.Channels.ChannelClosedException)
                {
                    // The dispatcher was disposed after the check above; the item has no body.
                }
            }
        }

        public async ValueTask HandleBasicCancelAsync(string consumerTag, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (false == _disposed && false == IsQuiescing)
            {
                IAsyncBasicConsumer consumer = GetAndRemoveConsumer(consumerTag);
                WorkStruct work = WorkStruct.CreateCancel(consumer, consumerTag, _shutdownToken);
                try
                {
                    await _writer.WriteAsync(work, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (System.Threading.Channels.ChannelClosedException)
                {
                    // The dispatcher was disposed after the check above; the item has no body.
                }
            }
        }

        public void Quiesce()
        {
            /*
             * No early return on IsQuiescing: the flag is set before the token is cancelled, so a second
             * caller that returned here could leave IsQuiescing true with the token still live, and
             * consumers treat that token as "the channel is going down" (#2006). Cancel() is idempotent
             * and _shutdownCts is deliberately never disposed (#1976), so repeating it is free.
             */
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
            _writer.TryWrite(WorkStruct.CreateShutdown(consumer, reason));
        }

        protected override Task InternalShutdownAsync()
        {
            _writer.TryComplete();
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

            private WorkStruct(WorkType type, IAsyncBasicConsumer consumer, string consumerTag, CancellationToken cancellationToken)
                : this()
            {
                WorkType = type;
                Consumer = consumer;
                ConsumerTag = consumerTag;
                CancellationToken = cancellationToken;
            }

            private WorkStruct(IAsyncBasicConsumer consumer, ShutdownEventArgs reason)
                : this()
            {
                WorkType = WorkType.Shutdown;
                Consumer = consumer;
                Reason = reason;
                // The shutdown handler's token must reflect only whether the shutdown
                // operation itself was cancelled by the caller, so it flows directly
                // from the shutdown reason. It must NOT be linked to the dispatcher's
                // _shutdownCts: that source is cancelled by Quiesce() before shutdown
                // work is dispatched (to cancel in-flight deliveries), which would make
                // the handler's token always arrive already-cancelled (see #1888).
                CancellationToken = reason.CancellationToken;
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
            }

            public static WorkStruct CreateCancel(IAsyncBasicConsumer consumer, string consumerTag, CancellationToken cancellationToken)
            {
                return new WorkStruct(WorkType.Cancel, consumer, consumerTag, cancellationToken);
            }

            public static WorkStruct CreateCancelOk(IAsyncBasicConsumer consumer, string consumerTag, CancellationToken cancellationToken)
            {
                return new WorkStruct(WorkType.CancelOk, consumer, consumerTag, cancellationToken);
            }

            public static WorkStruct CreateConsumeOk(IAsyncBasicConsumer consumer, string consumerTag, CancellationToken cancellationToken)
            {
                return new WorkStruct(WorkType.ConsumeOk, consumer, consumerTag, cancellationToken);
            }

            public static WorkStruct CreateShutdown(IAsyncBasicConsumer consumer, ShutdownEventArgs reason)
            {
                // The shutdown reason already carries the correct cancellation token (the
                // token of the close operation, if any). It must be handed to the consumer
                // as-is: linking it to the dispatcher's _shutdownCts here would make the
                // handler's token always arrive already-cancelled, because Quiesce()
                // cancels _shutdownCts before shutdown work is dispatched (see #1888).
                return new WorkStruct(consumer, reason);
            }

            public static WorkStruct CreateDeliver(IAsyncBasicConsumer consumer, string consumerTag, ulong deliveryTag, bool redelivered,
                string exchange, string routingKey, IReadOnlyBasicProperties basicProperties, RentedMemory body, CancellationToken cancellationToken)
            {
                return new WorkStruct(consumer, consumerTag, deliveryTag, redelivered,
                    exchange, routingKey, basicProperties, body, cancellationToken);
            }

            // NOT idempotent: a readonly struct field means RentedMemory.Dispose() runs on a
            // defensive copy, so a second call returns the same array to the pool twice. Dispose a
            // given work item exactly once. See docs/internal/consumer-dispatch-concurrency.md.
            public void Dispose()
            {
                Body.Dispose();
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

                        /*
                         * Run the WHOLE shutdown, not just TryComplete: completing the writer alone
                         * leaves every consumer with a null ShutdownReason and IsRunning true on a
                         * dead channel. The returned task is _worker, deliberately not awaited.
                         * _shutdownCts is deliberately NOT disposed - read issue #1976 and
                         * docs/internal/consumer-dispatch-concurrency.md before "fixing" that.
                         */
                        ObserveFault(ShutdownAsync(DisposalReason()));
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

        /*
         * The reason handed to consumers when disposal shuts the dispatcher down. Channel.CloseAsync
         * publishes the close reason before transmitting channel.close, so it is already set on every
         * path here. The fallback covers a dispatcher built without a channel, as the unit tests do,
         * and is deliberately not an error code.
         */
        // The task returned by ShutdownAsync is _worker, which can fault, and no caller on the
        // dispose paths awaits it. Left unobserved that reaches TaskScheduler.UnobservedTaskException,
        // which is fatal for a host configured with ThrowUnobservedTaskExceptions.
        private static void ObserveFault(Task task)
        {
            _ = task.ContinueWith(static t => _ = t.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private ShutdownEventArgs DisposalReason()
        {
            return _channel?.CloseReason
                ?? new ShutdownEventArgs(ShutdownInitiator.Library,
                    Constants.ReplySuccess, "consumer dispatcher disposed");
        }

        // Async disposal additionally waits for the queued notifications to reach their consumers,
        // bounded by ConsumerDispatcherDrainTimeout and best effort. Reuses WaitForShutdownAsync for
        // its #1751 AggregateException filtering. See docs/internal/consumer-dispatch-concurrency.md.
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                Quiesce();
                ObserveFault(ShutdownAsync(DisposalReason()));

                using var cts = new CancellationTokenSource(InternalConstants.ConsumerDispatcherDrainTimeout);
                await WaitForShutdownAsync(cts.Token)
                    .ConfigureAwait(false);
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

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
