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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.Framing.Impl
{
#nullable enable
    internal sealed partial class AutorecoveringConnection
    {
        private Task? _recoveryTask;
        private readonly CancellationTokenSource _recoveryCancellationTokenSource = new CancellationTokenSource();

        private void HandleConnectionShutdown(object _, ShutdownEventArgs args)
        {
            if (ShouldTriggerConnectionRecovery(args))
            {
                var recoverTask = new Task<Task>(RecoverConnectionAsync);
                if (Interlocked.CompareExchange(ref _recoveryTask, recoverTask.Unwrap(), null) is null)
                {
                    recoverTask.Start();
                }
            }

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

                // happens when EOF is reached, e.g. due to RabbitMQ node
                // connectivity loss or abrupt shutdown
                if (args.Initiator == ShutdownInitiator.Library)
                {
                    return true;
                }

                return false;
            }
        }

        private async Task RecoverConnectionAsync()
        {
            try
            {
                CancellationToken token = _recoveryCancellationTokenSource.Token;
                bool success;
                do
                {
                    await Task.Delay(_config.NetworkRecoveryInterval, token)
                        .ConfigureAwait(false);
                    success = await TryPerformAutomaticRecoveryAsync(token)
                        .ConfigureAwait(false);
                } while (false == success && false == token.IsCancellationRequested);
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
                // clear recovery task
                _recoveryTask = null;
            }
        }

        /// <summary>
        /// Async cancels the main recovery loop and will block until the loop finishes, or the timeout
        /// expires, to prevent Close operations overlapping with recovery operations.
        /// </summary>
        private async ValueTask StopRecoveryLoopAsync(CancellationToken cancellationToken)
        {
            Task? task = _recoveryTask;
            if (task != null)
            {
                _recoveryCancellationTokenSource.Cancel();
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
                    else
                    {
                        throw;
                    }
                }
            }
        }

        private static void HandleTopologyRecoveryException(TopologyRecoveryException e)
        {
            ESLog.Error("Topology recovery exception", e);
            if (e.InnerException is AlreadyClosedException ||
                    (e.InnerException is OperationInterruptedException) ||
                    (e.InnerException is TimeoutException))
            {
                throw e;
            }
            ESLog.Info($"Will not retry recovery because of {e.InnerException?.GetType().FullName}: it's not a known problem with connectivity, ignoring it", e);
        }

        // TODO cancellation token
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
                            // TODO cancellation token
                            await RecoverExchangesAsync(_innerConnection, recordedEntitiesSemaphoreHeld: true)
                                .ConfigureAwait(false);
                            // TODO cancellation token
                            await RecoverQueuesAsync(_innerConnection, recordedEntitiesSemaphoreHeld: true)
                                .ConfigureAwait(false);
                            // TODO cancellation token
                            await RecoverBindingsAsync(_innerConnection, recordedEntitiesSemaphoreHeld: true)
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
                    _recoverySucceededWrapper.Invoke(this, EventArgs.Empty);

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
            try
            {
                Connection defunctConnection = _innerConnection;
                IFrameHandler fh = await _endpoints.SelectOneAsync(_config.FrameHandlerFactoryAsync, cancellationToken)
                    .ConfigureAwait(false);
                _innerConnection = new Connection(_config, fh);
                await _innerConnection.OpenAsync(cancellationToken)
                    .ConfigureAwait(false);
                _innerConnection.TakeOver(defunctConnection);
                return true;
            }
            catch (Exception e)
            {
                ESLog.Error("Connection recovery exception.", e);
                // Trigger recovery error events
                if (!_connectionRecoveryErrorWrapper.IsEmpty)
                {
                    // Note: recordedEntities semaphore is _NOT_ held at this point
                    _connectionRecoveryErrorWrapper.Invoke(this, new ConnectionRecoveryErrorEventArgs(e));
                }
            }

            return false;
        }

        private async ValueTask RecoverExchangesAsync(IConnection connection,
            bool recordedEntitiesSemaphoreHeld = false)
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
                    using (IChannel ch = await connection.CreateChannelAsync().ConfigureAwait(false))
                    {
                        await recordedExchange.RecoverAsync(ch)
                            .ConfigureAwait(false);
                        await ch.CloseAsync()
                            .ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    if (_config.TopologyRecoveryExceptionHandler.ExchangeRecoveryExceptionHandlerAsync != null
                        && _config.TopologyRecoveryExceptionHandler.ExchangeRecoveryExceptionCondition(recordedExchange, ex))
                    {
                        try
                        {
                            _recordedEntitiesSemaphore.Release();
                            await _config.TopologyRecoveryExceptionHandler.ExchangeRecoveryExceptionHandlerAsync(recordedExchange, ex, this)
                                .ConfigureAwait(false);
                        }
                        finally
                        {
                            await _recordedEntitiesSemaphore.WaitAsync()
                                .ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        HandleTopologyRecoveryException(new TopologyRecoveryException($"Caught an exception while recovering exchange '{recordedExchange}'", ex));
                    }
                }
            }
        }

        private async Task RecoverQueuesAsync(IConnection connection,
            bool recordedEntitiesSemaphoreHeld = false)
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
                    using (IChannel ch = await connection.CreateChannelAsync().ConfigureAwait(false))
                    {
                        newName = await recordedQueue.RecoverAsync(ch)
                            .ConfigureAwait(false);
                        await ch.CloseAsync()
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
                                recordedEntitiesSemaphoreHeld: recordedEntitiesSemaphoreHeld)
                                .ConfigureAwait(false);
                        }

                        await RecordQueueAsync(new RecordedQueue(newName, recordedQueue),
                            recordedEntitiesSemaphoreHeld: recordedEntitiesSemaphoreHeld)
                            .ConfigureAwait(false);

                        if (!_queueNameChangedAfterRecoveryWrapper.IsEmpty)
                        {
                            try
                            {
                                _recordedEntitiesSemaphore.Release();
                                _queueNameChangedAfterRecoveryWrapper.Invoke(this, new QueueNameChangedAfterRecoveryEventArgs(oldName, newName));
                            }
                            finally
                            {
                                await _recordedEntitiesSemaphore.WaitAsync()
                                    .ConfigureAwait(false);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_config.TopologyRecoveryExceptionHandler.QueueRecoveryExceptionHandlerAsync != null
                        && _config.TopologyRecoveryExceptionHandler.QueueRecoveryExceptionCondition(recordedQueue, ex))
                    {
                        try
                        {
                            _recordedEntitiesSemaphore.Release();
                            await _config.TopologyRecoveryExceptionHandler.QueueRecoveryExceptionHandlerAsync(recordedQueue, ex, this)
                                .ConfigureAwait(false);
                        }
                        finally
                        {
                            await _recordedEntitiesSemaphore.WaitAsync()
                                .ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        HandleTopologyRecoveryException(new TopologyRecoveryException($"Caught an exception while recovering queue '{recordedQueue}'", ex));
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
            bool recordedEntitiesSemaphoreHeld = false)
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
                    using (IChannel ch = await connection.CreateChannelAsync().ConfigureAwait(false))
                    {
                        await binding.RecoverAsync(ch)
                            .ConfigureAwait(false);
                        await ch.CloseAsync()
                            .ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    if (_config.TopologyRecoveryExceptionHandler.BindingRecoveryExceptionHandlerAsync != null
                        && _config.TopologyRecoveryExceptionHandler.BindingRecoveryExceptionCondition(binding, ex))
                    {
                        try
                        {
                            _recordedEntitiesSemaphore.Release();
                            await _config.TopologyRecoveryExceptionHandler.BindingRecoveryExceptionHandlerAsync(binding, ex, this)
                                .ConfigureAwait(false);
                        }
                        finally
                        {
                            await _recordedEntitiesSemaphore.WaitAsync()
                                .ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        HandleTopologyRecoveryException(new TopologyRecoveryException($"Caught an exception while recovering binding between {binding.Source} and {binding.Destination}", ex));
                    }
                }
            }
        }

        internal async ValueTask RecoverConsumersAsync(AutorecoveringChannel channelToRecover, IChannel channelToUse,
            bool recordedEntitiesSemaphoreHeld = false)
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
                    _consumerAboutToBeRecovered.Invoke(this, new RecoveringConsumerEventArgs(consumer.ConsumerTag, consumer.Arguments));
                }
                finally
                {
                    _recordedEntitiesSemaphore.Wait();
                }

                string oldTag = consumer.ConsumerTag;
                try
                {
                    string newTag = await consumer.RecoverAsync(channelToUse)
                        .ConfigureAwait(false);
                    RecordedConsumer consumerWithNewConsumerTag = RecordedConsumer.WithNewConsumerTag(newTag, consumer);
                    UpdateConsumer(oldTag, newTag, consumerWithNewConsumerTag);

                    if (!_consumerTagChangeAfterRecoveryWrapper.IsEmpty)
                    {
                        try
                        {
                            _recordedEntitiesSemaphore.Release();
                            _consumerTagChangeAfterRecoveryWrapper.Invoke(this, new ConsumerTagChangedAfterRecoveryEventArgs(oldTag, newTag));
                        }
                        finally
                        {
                            await _recordedEntitiesSemaphore.WaitAsync()
                                .ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex)
                {
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
                            await _recordedEntitiesSemaphore.WaitAsync()
                                .ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        HandleTopologyRecoveryException(new TopologyRecoveryException($"Caught an exception while recovering consumer {oldTag} on queue {consumer.Queue}", ex));
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

            foreach (AutorecoveringChannel channel in _channels)
            {
                await channel.AutomaticallyRecoverAsync(this, _config.TopologyRecoveryEnabled,
                    recordedEntitiesSemaphoreHeld: recordedEntitiesSemaphoreHeld,
                    cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }
}
