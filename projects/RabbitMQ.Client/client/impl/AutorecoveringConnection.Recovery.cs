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
        private CancellationTokenSource? _recoveryCancellationTokenSource;

        private CancellationTokenSource RecoveryCancellationTokenSource => _recoveryCancellationTokenSource ??= new CancellationTokenSource();

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

            static bool ShouldTriggerConnectionRecovery(ShutdownEventArgs args) =>
                args.Initiator == ShutdownInitiator.Peer ||
                // happens when EOF is reached, e.g. due to RabbitMQ node
                // connectivity loss or abrupt shutdown
                args.Initiator == ShutdownInitiator.Library;
        }

        private async Task RecoverConnectionAsync()
        {
            try
            {
                var token = RecoveryCancellationTokenSource.Token;
                bool success;
                do
                {
                    await Task.Delay(_factory.NetworkRecoveryInterval, token).ConfigureAwait(false);
                    success = TryPerformAutomaticRecovery();
                } while (!success && !token.IsCancellationRequested);
            }
            catch (OperationCanceledException)
            {
                // expected when recovery cancellation token is set.
            }
            catch (Exception e)
            {
                ESLog.Error("Main recovery loop threw unexpected exception.", e);
            }

            // clear recovery task
            _recoveryTask = null;
        }

        /// <summary>
        /// Cancels the main recovery loop and will block until the loop finishes, or the timeout
        /// expires, to prevent Close operations overlapping with recovery operations.
        /// </summary>
        private void StopRecoveryLoop()
        {
            var task = _recoveryTask;
            if (task is null)
            {
                return;
            }
            RecoveryCancellationTokenSource.Cancel();

            Task timeout = Task.Delay(_factory.RequestedConnectionTimeout);
            if (Task.WhenAny(task, timeout).Result == timeout)
            {
                ESLog.Warn("Timeout while trying to stop background AutorecoveringConnection recovery loop.");
            }
        }

        private static void HandleTopologyRecoveryException(TopologyRecoveryException e)
        {
            ESLog.Error("Topology recovery exception", e);
            if (e.InnerException is AlreadyClosedException or OperationInterruptedException or TimeoutException)
            {
                throw e;
            }
            ESLog.Info($"Will not retry recovery because of {e.InnerException?.GetType().FullName}: it's not a known problem with connectivity, ignoring it", e);
        }

        private bool TryPerformAutomaticRecovery()
        {
            ESLog.Info("Performing automatic recovery");

            try
            {
                ThrowIfDisposed();
                if (TryRecoverConnectionDelegate())
                {
                    lock (_recordedEntitiesLock)
                    {
                        ThrowIfDisposed();
                        if (_factory.TopologyRecoveryEnabled)
                        {
                            // The recovery sequence is the following:
                            //
                            // 1. Recover exchanges
                            // 2. Recover queues
                            // 3. Recover bindings
                            // 4. Recover consumers
                            using var recoveryChannel = _innerConnection.CreateModel();
                            RecoverExchanges(recoveryChannel);
                            RecoverQueues(recoveryChannel);
                            RecoverBindings(recoveryChannel);
                        }
                        RecoverModelsAndItsConsumers();
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
            }

            return false;
        }

        private bool TryRecoverConnectionDelegate()
        {
            try
            {
                var defunctConnection = _innerConnection;
                IFrameHandler fh = _endpoints.SelectOne(_factory.CreateFrameHandler);
                _innerConnection = new Connection(_factory, fh, ClientProvidedName);
                _innerConnection.TakeOver(defunctConnection);
                return true;
            }
            catch (Exception e)
            {
                ESLog.Error("Connection recovery exception.", e);
                // Trigger recovery error events
                if (!_connectionRecoveryErrorWrapper.IsEmpty)
                {
                    _connectionRecoveryErrorWrapper.Invoke(this, new ConnectionRecoveryErrorEventArgs(e));
                }
            }

            return false;
        }

        private void RecoverExchanges(IModel channel)
        {
            foreach (var recordedExchange in _recordedExchanges.Values)
            {
                try
                {
                    recordedExchange.Recover(channel);
                }
                catch (Exception ex)
                {
                    HandleTopologyRecoveryException(new TopologyRecoveryException($"Caught an exception while recovering exchange '{recordedExchange}'", ex));
                }
            }
        }

        private void RecoverQueues(IModel channel)
        {
            foreach (var recordedQueue in _recordedQueues.Values.ToArray())
            {
                try
                {
                    var newName = recordedQueue.Recover(channel);
                    var oldName = recordedQueue.Name;

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
                            DeleteRecordedQueue(oldName);
                        }
                        RecordQueue(new RecordedQueue(newName, recordedQueue));

                        if (!_queueNameChangeAfterRecoveryWrapper.IsEmpty)
                        {
                            _queueNameChangeAfterRecoveryWrapper.Invoke(this, new QueueNameChangedAfterRecoveryEventArgs(oldName, newName));
                        }
                    }
                }
                catch (Exception ex)
                {
                    HandleTopologyRecoveryException(new TopologyRecoveryException($"Caught an exception while recovering queue '{recordedQueue}'", ex));
                }
            }
        }

        private void RecoverBindings(IModel channel)
        {
            foreach (var binding in _recordedBindings)
            {
                try
                {
                    binding.Recover(channel);
                }
                catch (Exception ex)
                {
                    HandleTopologyRecoveryException(new TopologyRecoveryException($"Caught an exception while recovering binding between {binding.Source} and {binding.Destination}", ex));
                }
            }
        }

        internal void RecoverConsumers(AutorecoveringModel channelToRecover, IModel channelToUse)
        {
            foreach (var consumer in _recordedConsumers.Values.ToArray())
            {
                if (consumer.Channel != channelToRecover)
                {
                    continue;
                }

                var oldTag = consumer.ConsumerTag;
                try
                {
                    var newTag = consumer.Recover(channelToUse);
                    UpdateConsumer(oldTag, newTag, RecordedConsumer.WithNewConsumerTag(newTag, consumer));

                    if (!_consumerTagChangeAfterRecoveryWrapper.IsEmpty)
                    {
                        _consumerTagChangeAfterRecoveryWrapper.Invoke(this, new ConsumerTagChangedAfterRecoveryEventArgs(oldTag, newTag));
                    }
                }
                catch (Exception ex)
                {
                    HandleTopologyRecoveryException(new TopologyRecoveryException($"Caught an exception while recovering consumer {oldTag} on queue {consumer.Queue}", ex));
                }
            }
        }

        private void RecoverModelsAndItsConsumers()
        {
            lock (_models)
            {
                foreach (AutorecoveringModel m in _models)
                {
                    m.AutomaticallyRecover(this, _factory.TopologyRecoveryEnabled);
                }
            }
        }
    }
}
