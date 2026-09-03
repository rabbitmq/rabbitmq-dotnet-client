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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.Impl
{
    internal sealed partial class AutorecoveringConnection
    {
        // TODO: Use Lock once update to .NET 9+
        // https://learn.microsoft.com/en-us/dotnet/api/system.threading.lock?view=net-10.0
        private readonly object _recoverySync = new object();
        private Task? _recoveryTask;
        private bool _recoveryPendingRequest;
        private readonly CancellationTokenSource _recoveryCancellationTokenSource = new CancellationTokenSource();

        private Task HandleConnectionShutdownAsync(object? _, ShutdownEventArgs args)
        {
            if (ShouldTriggerConnectionRecovery(args))
            {
                // Safe to lock here: no await runs while the lock is held (this method is
                // not async), so Monitor is entered and released on the same thread. An
                // await inside a lock is a C# compile error precisely because it would
                // break that thread affinity.
                lock (_recoverySync)
                {
                    if (_disposed)
                    {
                        // Disposal has begun; _recoveryCancellationTokenSource may already be
                        // disposed, so do not start a new recovery task. This is best-effort
                        // (DisposeAsync does not take _recoverySync); RecoverConnectionAsync
                        // still tolerates a disposed source by capturing the token inside its
                        // try.
                        return Task.CompletedTask;
                    }

                    if (_recoveryTask == null)
                    {
                        // Run the recovery loop on the thread pool. Assigning the task
                        // returned by Task.Run (rather than invoking RecoverConnectionAsync
                        // directly) guarantees _recoveryTask is set before the loop's finally
                        // can run: a direct call executes inline while we hold _recoverySync,
                        // and if recovery completes synchronously the finally would clear
                        // _recoveryTask before this assignment resurrected a completed task
                        // into it, permanently wedging recovery. Task.Run also uses
                        // TaskScheduler.Default rather than the ambient TaskScheduler.Current
                        // that a bare Task.Start() would capture.
                        _recoveryTask = Task.Run(RecoverConnectionAsync);
                    }
                    else
                    {
                        // Notify current recovery task about new recovery request,
                        // as there is no other task to catch it.
                        _recoveryPendingRequest = true;
                    }
                }
            }

            return Task.CompletedTask;

            static bool ShouldTriggerConnectionRecovery(ShutdownEventArgs args)
            {
                if (args.Initiator == ShutdownInitiator.Peer)
                {
                    if (args.ReplyCode == Constants.AccessRefused)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }

                if (args.Initiator == ShutdownInitiator.Library)
                {
                    /*
                     * https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/826
                     * Happens when an AppDomain is unloaded
                     */
                    if (args is { Exception: ThreadAbortException, ReplyCode: Constants.InternalError })
                    {
                        return false;
                    }
                    else
                    {
                        // happens when EOF is reached, e.g. due to RabbitMQ node
                        // connectivity loss or abrupt shutdown
                        return true;
                    }
                }

                return false;
            }
        }

        private async Task RecoverConnectionAsync()
        {
            bool retryRecovery = true;
            while (retryRecovery)
            {
                try
                {
                    // Capture the token inside the try. If _recoveryCancellationTokenSource
                    // was already disposed (a shutdown notification racing with DisposeAsync),
                    // reading Token throws ObjectDisposedException; catching it here logs it
                    // and lets the finally clear _recoveryTask, rather than faulting this task
                    // unobserved and leaving _recoveryTask non-null (which would permanently
                    // block further recovery).
                    CancellationToken token = _recoveryCancellationTokenSource.Token;

                    // Re-check if connection is not opened already, as we could execute it multiple times.
                    bool success = IsOpen;
                    while (false == success && false == token.IsCancellationRequested && false == _disposed)
                    {
                        await Task.Delay(_config.NetworkRecoveryInterval, token)
                            .ConfigureAwait(false);
                        success = await TryPerformAutomaticRecoveryAsync(token)
                            .ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // expected when recovery cancellation token is set.
                }
                catch (Exception e)
                {
                    ESLog.Error("Main recovery loop threw unexpected exception.", e);
                }
                finally
                {
                    // Safe to lock inside this async method: the lock body has no await
                    // (all awaits are in the try above), so Monitor is entered and released
                    // on the same thread.
                    lock (_recoverySync)
                    {
                        /*
                         * It is possible that the re-opened connection was again shut down while we executed recovery method above.
                         * In those cases the shutdown callback didn't enqueue a new recovery task, so recovery will not happen.
                         * There could be a delay between opening the connection and returning from the recovery method,
                         * so there is a race-condition that could lead to a permanently never recovered connection.
                         */
                        if (_recoveryPendingRequest)
                        {
                            _recoveryPendingRequest = false;
                            retryRecovery = true;
                        }
                        else
                        {
                            _recoveryTask = null;
                            retryRecovery = false;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Async cancels the main recovery loop and will block until the loop finishes, or the timeout
        /// expires, to prevent CloseAsync operations overlapping with recovery operations.
        /// </summary>
        private async ValueTask StopRecoveryLoopAsync(CancellationToken cancellationToken)
        {
            // We have to cancel the token regardless of whether there is a task,
            // as there could be a race condition that starts a new recovery task right after we checked.
            // It's safer to cancel it, so even if a new task is created - it will be a nop.
            _recoveryCancellationTokenSource.Cancel();

            // Read _recoveryTask under _recoverySync so we serialize with
            // HandleConnectionShutdownAsync: if it is concurrently starting a task, we wait
            // for it to publish _recoveryTask and then await that task. Any task started
            // strictly after this read sees the already-cancelled token and is a nop.
            //
            // Safe to lock inside this async method: the lock body only reads a field and
            // holds no await (the WaitAsync below runs after the lock is released), so
            // Monitor is entered and released on the same thread.
            Task? task;
            lock (_recoverySync)
            {
                task = _recoveryTask;
            }

            if (task != null)
            {
                using var timeoutTokenSource = new CancellationTokenSource(_config.RequestedConnectionTimeout);
                using var lts = CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, cancellationToken);
                try
                {
                    await task.WaitAsync(lts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (timeoutTokenSource.Token.IsCancellationRequested)
                    {
                        ESLog.Warn("Timeout while trying to stop background AutorecoveringConnection recovery loop.");
                    }
                    else if (cancellationToken.IsCancellationRequested)
                    {
                        // The caller's cancellationToken fired (possibly before this method was
                        // called), which aborted the WaitAsync above. We do NOT rethrow: a
                        // caller-cancelled close should complete quietly rather than surface an
                        // OperationCanceledException (this mirrors the abort path in
                        // Connection.CloseAsync). Note the recovery loop has been signalled to
                        // cancel via _recoveryCancellationTokenSource but is NOT awaited here, so
                        // it may still be unwinding when this returns; cancellation is cooperative.
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        private static void HandleTopologyRecoveryException(TopologyRecoveryException e,
            CancellationToken recoveryCancellationToken)
        {
            ESLog.Error("Topology recovery exception", e);
            if (ShouldRetryRecoveryAfter(e.InnerException, recoveryCancellationToken))
            {
                throw e;
            }
            ESLog.Info($"Will not retry recovery because of {e.InnerException?.GetType().FullName}: it's not a known problem with connectivity, ignoring it", e);
        }

        /// <summary>
        /// Decides whether an exception a custom <see cref="TopologyRecoveryExceptionHandler"/> has
        /// already run for still has to fail the whole recovery attempt.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A configured handler used to be the end of the story: its branch swallowed the exception
        /// and recovery went on to report success. That reopened issue #1993 for anyone who installed
        /// one, because <see cref="ShouldRetryRecoveryAfter"/> was only consulted on the branch
        /// without a handler. The default conditions are all
        /// <c>(entity, exception) =&gt; true</c>, so merely installing a handler, without also
        /// narrowing its condition, routed every exception away from that check, including the
        /// continuation timeouts it exists to catch.
        /// </para>
        /// <para>
        /// The classification cannot simply be reused here, because it is deliberately coarse about
        /// <see cref="OperationInterruptedException"/>: it treats every one as a connectivity problem
        /// on the reasoning that the connection is gone and nothing was applied. That does not hold
        /// for a soft error, which is a channel error in the AMQP 0-9-1 sense. There the broker
        /// closed one channel with a reply code explaining why it refused this specific operation and
        /// left the connection intact, so its refusal is final, no request is left for it to act on,
        /// and retrying the attempt cannot change the outcome. The usual case is
        /// <see cref="Constants.PreconditionFailed"/> from redeclaring an entity with different
        /// arguments, which is the very thing recovery exception handlers are installed to repair, so
        /// forcing a retry there would penalise the case this mechanism exists for.
        /// </para>
        /// <para>
        /// So the handler still runs first and still sees every exception its condition matches, and
        /// afterwards the attempt is failed only for a failure the broker may still act on: a
        /// timeout, or a connection that is gone. See issue #1995.
        /// </para>
        /// </remarks>
        internal static bool ShouldRetryAfterHandledRecoveryException(Exception? topologyRecoveryInnerException,
            bool connectionIsOpen, CancellationToken recoveryCancellationToken)
        {
            if (BrokerRefusalIsFinal(topologyRecoveryInnerException, connectionIsOpen))
            {
                return false;
            }

            return ShouldRetryRecoveryAfter(topologyRecoveryInnerException, recoveryCancellationToken);
        }

        /// <summary>
        /// Whether the exception carries a definitive per-channel refusal from the broker, leaving
        /// nothing in flight and nothing a retry could change.
        /// </summary>
        /// <remarks>
        /// Two things have to hold beyond the reply code, and neither is implied by it.
        ///
        /// The request must actually have been sent. <see cref="AlreadyClosedException"/> derives from
        /// <see cref="OperationInterruptedException"/> and is thrown by
        /// <c>SessionBase.ThrowAlreadyClosedException</c> carrying the *channel's* close reason, so it
        /// can present a soft reply code while the operation was never transmitted at all. That entity
        /// is definitely un-recovered and only a retry can fix it. This matters in practice because
        /// consumer recovery shares one channel across every consumer on it: once an earlier entity
        /// closes that channel, the rest fail this way, and treating them as final would report
        /// recovery successful with a closed channel and unrecovered consumers.
        ///
        /// The connection must still be usable. A soft reply code identifies a channel error only
        /// when the connection survived; the same codes appear on connection-level closes, which is
        /// why <c>ShouldTriggerConnectionRecovery</c> above has to special-case a peer-initiated
        /// <see cref="Constants.AccessRefused"/>. If the connection is gone, nothing was applied and
        /// everything has to be retried.
        /// </remarks>
        private static bool BrokerRefusalIsFinal(Exception? topologyRecoveryInnerException,
            bool connectionIsOpen)
        {
            if (false == connectionIsOpen || topologyRecoveryInnerException is AlreadyClosedException)
            {
                return false;
            }

            return topologyRecoveryInnerException is OperationInterruptedException operationInterrupted
                && operationInterrupted.ShutdownReason is not null
                && IsSoftError(operationInterrupted.ShutdownReason.ReplyCode);

            // The soft (channel-level) errors a topology recovery operation can be refused with;
            // these close a channel rather than the connection. 311 (content-too-large) and 313
            // (no-consumers) are AMQP soft errors too, but they are basic.publish / basic.return
            // codes that never close a channel during topology recovery, so they cannot appear here.
            static bool IsSoftError(ushort replyCode)
            {
                switch (replyCode)
                {
                    case Constants.AccessRefused:
                    case Constants.NotFound:
                    case Constants.ResourceLocked:
                    case Constants.PreconditionFailed:
                        return true;
                    default:
                        return false;
                }
            }
        }

        private void ThrowIfHandledExceptionStillRequiresRetry(TopologyRecoveryException e,
            CancellationToken recoveryCancellationToken)
        {
            if (ShouldRetryAfterHandledRecoveryException(e.InnerException,
                _innerConnection?.IsOpen == true, recoveryCancellationToken))
            {
                ESLog.Error("Topology recovery exception was passed to a recovery exception handler, " +
                    "but indicates a failure the broker may still act on, which the handler cannot " +
                    "resolve. The recovery attempt will be retried.", e);
                throw e;
            }

            // Parity with the no-handler path's "Will not retry recovery" log: record that a handler
            // ran and the failure is final, so an entity a handler could not actually repair is not
            // dropped without a trace.
            ESLog.Info("Topology recovery exception was handled by a recovery exception handler and " +
                "does not require the recovery attempt to be retried.", e);
        }

        /// <summary>
        /// Decides whether a failure to recover a single topology entity means the whole recovery
        /// attempt has to be abandoned and retried, rather than skipped over.
        /// </summary>
        /// <remarks>
        /// The distinction that matters is whether the broker may still act on the request after the
        /// client has given up on it. If it may, the client's view of the connection is no longer
        /// trustworthy and recovery must not be reported as successful.
        /// </remarks>
        internal static bool ShouldRetryRecoveryAfter(Exception? topologyRecoveryInnerException,
            CancellationToken recoveryCancellationToken)
        {
            switch (topologyRecoveryInnerException)
            {
                // Known connectivity problems. The connection is gone, so nothing was applied.
                case AlreadyClosedException:
                case OperationInterruptedException:
                case TimeoutException:
                    return true;

                /*
                 * A protocol operation that ran past ContinuationTimeout completes its continuation
                 * as cancelled (AsyncRpcContinuation.HandleContinuationTimeout), so by type alone it
                 * is indistinguishable from the caller cancelling recovery. Tell them apart with the
                 * recovery token: if recovery itself was not cancelled, the operation timed out.
                 *
                 * A timed-out operation must be retried, because its frame is already on the wire and
                 * the broker can still apply it after the client has stopped waiting. The case that
                 * brought this to light is basic.consume against a quorum queue that has lost quorum:
                 * the queue cannot answer until it elects a new leader, which can take longer than the
                 * 20s default. Skipping it leaves the broker with a registered, active consumer that
                 * the client never added to the channel's ConsumerDispatcher, so from then on every
                 * delivery for that consumer tag resolves to the FallbackConsumer and is discarded
                 * without being acked. With a non-zero prefetch the broker then waits forever for an
                 * acknowledgement that cannot come, and the queue stops being consumed for good -
                 * silently, on a connection and channel that both still report IsOpen.
                 *
                 * https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1993
                 */
                case OperationCanceledException:
                    return false == recoveryCancellationToken.IsCancellationRequested;

                default:
                    return false;
            }
        }

        private async ValueTask<bool> TryPerformAutomaticRecoveryAsync(CancellationToken cancellationToken)
        {
            ESLog.Info("Performing automatic recovery");

            try
            {
                ThrowIfDisposed();
                if (await TryRecoverConnectionDelegateAsync(cancellationToken).ConfigureAwait(false))
                {
                    await _recordedEntitiesSemaphore.WaitAsync(cancellationToken)
                        .ConfigureAwait(false);
                    try
                    {
                        ThrowIfDisposed();
                        if (_config.TopologyRecoveryEnabled)
                        {
                            // The recovery sequence is the following:
                            //
                            // 1. Recover exchanges
                            // 2. Recover queues
                            // 3. Recover bindings
                            // 4. Recover consumers
                            await RecoverExchangesAsync(_innerConnection, recordedEntitiesSemaphoreHeld: true, cancellationToken)
                                .ConfigureAwait(false);
                            await RecoverQueuesAsync(_innerConnection, recordedEntitiesSemaphoreHeld: true, cancellationToken)
                                .ConfigureAwait(false);
                            await RecoverBindingsAsync(_innerConnection, recordedEntitiesSemaphoreHeld: true, cancellationToken)
                                .ConfigureAwait(false);

                        }
                        await RecoverChannelsAndItsConsumersAsync(recordedEntitiesSemaphoreHeld: true, cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                    }
                    finally
                    {
                        _recordedEntitiesSemaphore.Release();
                    }

                    ESLog.Info("Connection recovery completed");
                    ThrowIfDisposed();
                    await _recoverySucceededAsyncWrapper.InvokeAsync(this, AsyncEventArgs.CreateOrDefault(cancellationToken))
                        .ConfigureAwait(false);

                    return true;
                }

                ESLog.Warn("Connection delegate was manually closed. Aborted recovery.");
            }
            catch (Exception e)
            {
                ESLog.Error("Exception when recovering connection. Will try again after retry interval.", e);
                try
                {
                    /*
                     * To prevent connection leaks on the next recovery loop,
                     * we abort the delegated connection if it is still open.
                     * We do not want to block the abort forever (potentially deadlocking recovery),
                     * so we specify the same configured timeout used for connection.
                     */
                    if (_innerConnection?.IsOpen == true)
                    {
                        await _innerConnection.AbortAsync(Constants.InternalError, "FailedAutoRecovery", _config.RequestedConnectionTimeout)
                            .ConfigureAwait(false);
                    }
                }
                catch (Exception e2)
                {
                    ESLog.Warn("Exception when aborting previous auto recovery connection.", e2);
                }
            }

            return false;
        }

        private async ValueTask<bool> TryRecoverConnectionDelegateAsync(CancellationToken cancellationToken)
        {
            Connection? maybeNewInnerConnection = null;
            using Activity? connectionActivity = RabbitMQActivitySource.OpenConnection(true);
            try
            {
                Connection defunctConnection = _innerConnection;
                IFrameHandler fh = await _endpoints.SelectOneAsync(_config.FrameHandlerFactoryAsync, cancellationToken)
                    .ConfigureAwait(false);
                connectionActivity?.SetNetworkTags(fh);
                maybeNewInnerConnection = new Connection(_config, fh);

                await maybeNewInnerConnection.OpenAsync(cancellationToken)
                    .ConfigureAwait(false);
                maybeNewInnerConnection.TakeOver(defunctConnection);

                /*
                 * Note: do this last in case something above throws an exception during re-connection
                 * We don't want to lose te old defunct connection in this case, since we have to take
                 * over its data / event handlers / etc when the re-connect eventually succeeds.
                 * https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1623 
                 */
                _innerConnection = maybeNewInnerConnection;
                return true;
            }
            catch (Exception e)
            {
                connectionActivity.SetActivityError(e);
                ESLog.Error("Connection recovery exception.", e);
                // Trigger recovery error events
                if (!_connectionRecoveryErrorAsyncWrapper.IsEmpty)
                {
                    // Note: recordedEntities semaphore is _NOT_ held at this point
                    await _connectionRecoveryErrorAsyncWrapper.InvokeAsync(this, new ConnectionRecoveryErrorEventArgs(e, cancellationToken))
                        .ConfigureAwait(false);
                }

                maybeNewInnerConnection?.Dispose();
            }

            return false;
        }

        private async ValueTask RecoverExchangesAsync(IConnection connection,
            bool recordedEntitiesSemaphoreHeld, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                return;
            }

            if (false == recordedEntitiesSemaphoreHeld)
            {
                throw new InvalidOperationException("recordedEntitiesSemaphore must be held");
            }

            foreach (RecordedExchange recordedExchange in _recordedExchanges.Values.Where(x => _config.TopologyRecoveryFilter?.ExchangeFilter(x) ?? true))
            {
                try
                {
                    IChannel channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                    await using (channel.ConfigureAwait(false))
                    {
                        await recordedExchange.RecoverAsync(channel, cancellationToken)
                            .ConfigureAwait(false);
                        await channel.CloseAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    var topologyRecoveryException = new TopologyRecoveryException($"Caught an exception while recovering exchange '{recordedExchange}'", ex);

                    if (_config.TopologyRecoveryExceptionHandler.ExchangeRecoveryExceptionHandlerAsync != null
                        && _config.TopologyRecoveryExceptionHandler.ExchangeRecoveryExceptionCondition(recordedExchange, ex))
                    {
                        try
                        {
                            _recordedEntitiesSemaphore.Release();
                            // FUTURE (?) cancellation token
                            await _config.TopologyRecoveryExceptionHandler.ExchangeRecoveryExceptionHandlerAsync(recordedExchange, ex, this)
                                .ConfigureAwait(false);
                        }
                        finally
                        {
                            await _recordedEntitiesSemaphore.WaitAsync(cancellationToken)
                                .ConfigureAwait(false);
                        }

                        ThrowIfHandledExceptionStillRequiresRetry(topologyRecoveryException,
                            cancellationToken);
                    }
                    else
                    {
                        HandleTopologyRecoveryException(topologyRecoveryException, cancellationToken);
                    }
                }
            }
        }

        private async Task RecoverQueuesAsync(IConnection connection,
            bool recordedEntitiesSemaphoreHeld, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                return;
            }

            if (false == recordedEntitiesSemaphoreHeld)
            {
                throw new InvalidOperationException("recordedEntitiesSemaphore must be held");
            }

            foreach (RecordedQueue recordedQueue in _recordedQueues.Values.Where(x => _config.TopologyRecoveryFilter?.QueueFilter(x) ?? true).ToArray())
            {
                try
                {
                    string newName = string.Empty;
                    IChannel channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                    await using (channel.ConfigureAwait(false))
                    {
                        newName = await recordedQueue.RecoverAsync(channel, cancellationToken)
                            .ConfigureAwait(false);
                        await channel.CloseAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }
                    string oldName = recordedQueue.Name;

                    if (oldName != newName)
                    {
                        // Make sure server-named queues are re-added with their new names.
                        // We only remove old name after we've updated the bindings and consumers,
                        // plus only for server-named queues, both to make sure we don't lose
                        // anything to recover. MK.
                        UpdateBindingsDestination(oldName, newName);
                        UpdateConsumerQueue(oldName, newName);

                        // see rabbitmq/rabbitmq-dotnet-client#43
                        if (recordedQueue.IsServerNamed)
                        {
                            await DeleteRecordedQueueAsync(oldName,
                                recordedEntitiesSemaphoreHeld: recordedEntitiesSemaphoreHeld, cancellationToken)
                                .ConfigureAwait(false);
                        }

                        await RecordQueueAsync(new RecordedQueue(newName, recordedQueue),
                            recordedEntitiesSemaphoreHeld: recordedEntitiesSemaphoreHeld, cancellationToken)
                            .ConfigureAwait(false);

                        if (!_queueNameChangedAfterRecoveryAsyncWrapper.IsEmpty)
                        {
                            try
                            {
                                _recordedEntitiesSemaphore.Release();
                                await _queueNameChangedAfterRecoveryAsyncWrapper.InvokeAsync(this, new QueueNameChangedAfterRecoveryEventArgs(oldName, newName, cancellationToken))
                                    .ConfigureAwait(false);
                            }
                            finally
                            {
                                await _recordedEntitiesSemaphore.WaitAsync(cancellationToken)
                                    .ConfigureAwait(false);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    var topologyRecoveryException = new TopologyRecoveryException($"Caught an exception while recovering queue '{recordedQueue}'", ex);

                    if (_config.TopologyRecoveryExceptionHandler.QueueRecoveryExceptionHandlerAsync != null
                        && _config.TopologyRecoveryExceptionHandler.QueueRecoveryExceptionCondition(recordedQueue, ex))
                    {
                        try
                        {
                            _recordedEntitiesSemaphore.Release();
                            // FUTURE (?) cancellation token
                            await _config.TopologyRecoveryExceptionHandler.QueueRecoveryExceptionHandlerAsync(recordedQueue, ex, this)
                                .ConfigureAwait(false);
                        }
                        finally
                        {
                            await _recordedEntitiesSemaphore.WaitAsync(cancellationToken)
                                .ConfigureAwait(false);
                        }

                        ThrowIfHandledExceptionStillRequiresRetry(topologyRecoveryException,
                            cancellationToken);
                    }
                    else
                    {
                        HandleTopologyRecoveryException(topologyRecoveryException, cancellationToken);
                    }
                }

                void UpdateBindingsDestination(string oldName, string newName)
                {
                    foreach (RecordedBinding b in _recordedBindings.ToArray())
                    {
                        if (b.Destination == oldName)
                        {
                            _recordedBindings.Remove(b);
                            _recordedBindings.Add(new RecordedBinding(newName, b));
                        }
                    }
                }

                void UpdateConsumerQueue(string oldName, string newName)
                {
                    foreach (RecordedConsumer consumer in _recordedConsumers.Values.ToArray())
                    {
                        if (consumer.Queue == oldName)
                        {
                            _recordedConsumers[consumer.ConsumerTag] = RecordedConsumer.WithNewQueueName(newName, consumer);
                        }
                    }
                }
            }
        }

        private async ValueTask RecoverBindingsAsync(IConnection connection,
            bool recordedEntitiesSemaphoreHeld, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                return;
            }

            if (false == recordedEntitiesSemaphoreHeld)
            {
                throw new InvalidOperationException("recordedEntitiesSemaphore must be held");
            }

            foreach (RecordedBinding binding in _recordedBindings.Where(x => _config.TopologyRecoveryFilter?.BindingFilter(x) ?? true))
            {
                try
                {
                    IChannel channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                    await using (channel.ConfigureAwait(false))
                    {
                        await binding.RecoverAsync(channel, cancellationToken)
                            .ConfigureAwait(false);
                        await channel.CloseAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    var topologyRecoveryException = new TopologyRecoveryException($"Caught an exception while recovering binding between {binding.Source} and {binding.Destination}", ex);

                    if (_config.TopologyRecoveryExceptionHandler.BindingRecoveryExceptionHandlerAsync != null
                        && _config.TopologyRecoveryExceptionHandler.BindingRecoveryExceptionCondition(binding, ex))
                    {
                        try
                        {
                            _recordedEntitiesSemaphore.Release();
                            // FUTURE (?) cancellation token
                            await _config.TopologyRecoveryExceptionHandler.BindingRecoveryExceptionHandlerAsync(binding, ex, this)
                                .ConfigureAwait(false);
                        }
                        finally
                        {
                            await _recordedEntitiesSemaphore.WaitAsync(cancellationToken)
                                .ConfigureAwait(false);
                        }

                        ThrowIfHandledExceptionStillRequiresRetry(topologyRecoveryException,
                            cancellationToken);
                    }
                    else
                    {
                        HandleTopologyRecoveryException(topologyRecoveryException, cancellationToken);
                    }
                }
            }
        }

        internal async ValueTask RecoverConsumersAsync(AutorecoveringChannel channelToRecover, IChannel channelToUse,
            bool recordedEntitiesSemaphoreHeld = false, CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                return;
            }

            if (false == recordedEntitiesSemaphoreHeld)
            {
                throw new InvalidOperationException("recordedEntitiesSemaphore must be held");
            }

            foreach (RecordedConsumer consumer in _recordedConsumers.Values.Where(x => _config.TopologyRecoveryFilter?.ConsumerFilter(x) ?? true).ToArray())
            {
                if (consumer.Channel != channelToRecover)
                {
                    continue;
                }

                try
                {
                    _recordedEntitiesSemaphore.Release();
                    await _recoveringConsumerAsyncWrapper.InvokeAsync(this, new RecoveringConsumerEventArgs(consumer.ConsumerTag, consumer.Arguments, cancellationToken))
                        .ConfigureAwait(false);
                }
                finally
                {
                    await _recordedEntitiesSemaphore.WaitAsync(cancellationToken)
                        .ConfigureAwait(false);
                }

                string oldTag = consumer.ConsumerTag;
                try
                {
                    string newTag = await consumer.RecoverAsync(channelToUse)
                        .ConfigureAwait(false);
                    RecordedConsumer consumerWithNewConsumerTag = RecordedConsumer.WithNewConsumerTag(newTag, consumer);
                    UpdateConsumer(oldTag, newTag, consumerWithNewConsumerTag);

                    if (!_consumerTagChangeAfterRecoveryAsyncWrapper.IsEmpty)
                    {
                        try
                        {
                            _recordedEntitiesSemaphore.Release();
                            await _consumerTagChangeAfterRecoveryAsyncWrapper.InvokeAsync(this, new ConsumerTagChangedAfterRecoveryEventArgs(oldTag, newTag, cancellationToken))
                                .ConfigureAwait(false);
                        }
                        finally
                        {
                            await _recordedEntitiesSemaphore.WaitAsync(cancellationToken)
                                .ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    var topologyRecoveryException = new TopologyRecoveryException($"Caught an exception while recovering consumer {oldTag} on queue {consumer.Queue}", ex);

                    if (_config.TopologyRecoveryExceptionHandler.ConsumerRecoveryExceptionHandlerAsync != null
                        && _config.TopologyRecoveryExceptionHandler.ConsumerRecoveryExceptionCondition(consumer, ex))
                    {
                        try
                        {
                            _recordedEntitiesSemaphore.Release();
                            await _config.TopologyRecoveryExceptionHandler.ConsumerRecoveryExceptionHandlerAsync(consumer, ex, this)
                                .ConfigureAwait(false);
                        }
                        finally
                        {
                            await _recordedEntitiesSemaphore.WaitAsync(cancellationToken)
                                .ConfigureAwait(false);
                        }

                        ThrowIfHandledExceptionStillRequiresRetry(topologyRecoveryException,
                            cancellationToken);
                    }
                    else
                    {
                        HandleTopologyRecoveryException(topologyRecoveryException, cancellationToken);
                    }
                }
            }

            void UpdateConsumer(string oldTag, string newTag, in RecordedConsumer consumer)
            {
                // make sure server-generated tags are re-added
                _recordedConsumers.Remove(oldTag);
                _recordedConsumers.Add(newTag, consumer);
            }
        }

        private async ValueTask RecoverChannelsAndItsConsumersAsync(bool recordedEntitiesSemaphoreHeld, CancellationToken cancellationToken)
        {
            if (false == recordedEntitiesSemaphoreHeld)
            {
                throw new InvalidOperationException("recordedEntitiesSemaphore must be held");
            }

            var channelsToRecover = new List<AutorecoveringChannel>();
            await _channelsSemaphore.WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            try
            {
                channelsToRecover.AddRange(_channels);
            }
            finally
            {
                _channelsSemaphore.Release();
            }

            var notRecoveredChannels = new List<AutorecoveringChannel>();
            foreach (AutorecoveringChannel channel in channelsToRecover)
            {
                bool recovered = await channel.AutomaticallyRecoverAsync(this, _config.TopologyRecoveryEnabled,
                    recordedEntitiesSemaphoreHeld: recordedEntitiesSemaphoreHeld,
                    cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (false == recovered)
                {
                    notRecoveredChannels.Add(channel);
                }
            }

            await _channelsSemaphore.WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            try
            {
                foreach (AutorecoveringChannel channel in notRecoveredChannels)
                {
                    _channels.Remove(channel);
                }
            }
            finally
            {
                _channelsSemaphore.Release();
            }
        }
    }
}
