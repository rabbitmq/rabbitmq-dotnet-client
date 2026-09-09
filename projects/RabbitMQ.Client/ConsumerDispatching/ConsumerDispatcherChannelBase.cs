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
         * Captured once, here, rather than read from _shutdownCts on every work item.
         * CancellationTokenSource.Token throws ObjectDisposedException once the source has been
         * disposed, even if it was cancelled first, while a token already copied out stays
         * usable. Reading it per work item meant a Dispose() racing an inbound frame threw from
         * the frame-receive loop, which no caller on that path catches, tearing down the whole
         * connection rather than the one channel. See issue #1988.
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
         * Each Handle*Async below checks _disposed/IsQuiescing and then writes, and the two are not
         * atomic. Dispose() completes the work channel, so a caller can pass the check and be
         * preempted across that completion; WriteAsync then raises ChannelClosedException. For a
         * delivery that unwinds through Channel.HandleCommandAsync, which has no catch, into the
         * connection's frame-receive loop - tearing down the whole connection rather than the one
         * channel, and abandoning the delivery's pooled body. That is the same failure the captured
         * _shutdownToken above was introduced to remove, reached by a different route, and this one
         * is reachable from an application thread rather than only from the serialized main loop.
         *
         * So each site treats a completed channel the way it already treats a quiescing one: drop
         * the work item. A completed channel is the only reason TryWrite/WriteAsync can fail here,
         * because the channel is unbounded. The delivery path additionally returns its pooled body,
         * which nothing else will now do.
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
                         * Disposal has to release the worker, and completing the writer is the only
                         * thing that can: ProcessChannelAsync awaits _reader.WaitToReadAsync() with
                         * no token, so cancelling _shutdownCts cannot wake it. Without this the
                         * worker stays parked for the process lifetime, rooting this dispatcher, its
                         * channel and its session, whenever a channel is disposed without its
                         * session having been shut down - for instance when an abort swallows a
                         * close that never got a close-ok.
                         *
                         * But completing is only half of a shutdown, and doing that half alone loses
                         * the other. ShutdownConsumer enqueues each consumer's Shutdown work item
                         * with _writer.TryWrite and discards the result, so once the writer is
                         * completed a later ConsumerDispatcher.ShutdownAsync - which is exactly what
                         * OnSessionShutdownAsync runs when the socket finally drops - silently
                         * enqueues nothing. Every consumer is then left reporting no shutdown reason
                         * with IsRunning true, on a channel that is gone, and nothing surfaces it
                         * because the channel's own ChannelShutdownAsync event does not go through
                         * the dispatcher.
                         *
                         * So run the whole shutdown rather than its tail. ShutdownAsync is not an
                         * async method: DoShutdownConsumers and the TryComplete inside
                         * InternalShutdownAsync both run before it returns, so the notifications are
                         * queued ahead of the completion and the worker drains them on its way out.
                         * That is the same enqueue-then-complete order the session-driven path uses.
                         * The returned task is _worker, which Dispose deliberately does not await:
                         * this runs on the caller's thread, a consumer callback may be arbitrarily
                         * slow, and the worker is already reachable through the field for anyone who
                         * needs to wait on it. DoShutdownConsumers clears the consumer collection,
                         * so a shutdown that has already happened makes this a no-op rather than a
                         * duplicate notification.
                         *
                         * The reason is the channel's own. Channel.CloseAsync sets it before it
                         * transmits channel.close, so it is already published on every path that
                         * reaches disposal, including an abort whose handshake never completed. The
                         * fallback covers a dispatcher built without a channel, which the unit tests
                         * do.
                         *
                         * _shutdownCts is deliberately NOT disposed, for the same reason the
                         * channel does not dispose its semaphores (see issue #1976). Quiesce()
                         * takes an early return when another caller has already set the quiescing
                         * flag, so a Quiesce racing a Dispose could dispose the source before
                         * Cancel() ran, leaving it disposed and never cancelled: work items then
                         * carry a token that can never fire, so a consumer awaiting it hangs
                         * silently, and reading its WaitHandle throws. The source arms no timer and
                         * holds no library registrations, so there is nothing to reclaim.
                         * See issue #1988.
                         */
                        _ = ShutdownAsync(DisposalReason());
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
         * The reason handed to consumers when disposal is what shuts the dispatcher down.
         *
         * Channel.CloseAsync sets the channel's close reason before it transmits channel.close, so
         * it is already published on every path that reaches disposal, including an abort whose
         * handshake never completed. The fallback covers a dispatcher built without a channel, which
         * the unit tests do; it is deliberately not an error code, because a consumer callback
         * should not be told the channel failed when nothing failed.
         */
        private ShutdownEventArgs DisposalReason()
        {
            return _channel?.CloseReason
                ?? new ShutdownEventArgs(ShutdownInitiator.Library,
                    Constants.ReplySuccess, "consumer dispatcher disposed");
        }

        /*
         * Async disposal waits, briefly, for the shutdown notifications queued above to actually
         * reach their consumers, which the synchronous path cannot do: Dispose runs on the caller's
         * thread and a consumer callback may be arbitrarily slow, so there it queues them and moves
         * on. Here the caller is already awaiting, so the wait is affordable and worth having,
         * because a caller who disposes and then inspects a consumer would otherwise still see the
         * stale state.
         *
         * WaitForShutdownAsync is the existing wait on _worker and carries the AggregateException
         * filtering that issue #1751 needed, so reuse it rather than write a second one. It returns
         * early once _disposed is set, hence the wait happening before the finally.
         *
         * Bounded by ConsumerDispatcherDrainTimeout and best effort: expiry means a consumer
         * callback is slow or stuck, which must not stop the channel being disposed. Everything is
         * swallowed for the same reason Dispose(bool) swallows.
         */
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                Quiesce();
                _ = ShutdownAsync(DisposalReason());

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
