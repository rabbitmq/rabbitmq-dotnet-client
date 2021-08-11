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
            if (e.InnerException is AlreadyClosedException || e.InnerException is OperationInterruptedException || e.InnerException is TimeoutException)
            {
                throw e;
            }
            ESLog.Info($"Will not retry recovery because of {e.InnerException?.GetType().FullName}: it's not a known problem with connectivty, ignoring it", e);

        }

        private bool TryPerformAutomaticRecovery()
        {
            ESLog.Info("Performing automatic recovery");

            try
            {
                ThrowIfDisposed();
                if (TryRecoverConnectionDelegate())
                {
                    ThrowIfDisposed();
                    RecoverModels();
                    if (_factory.TopologyRecoveryEnabled)
                    {
                        using (IModel model = _innerConnection.CreateModel())
                        {
                            // The recovery sequence is the following:
                            //
                            // 1. Recover exchanges
                            // 2. Recover queues
                            // 3. Recover bindings
                            // 4. Recover consumers
                            //
                            // New IModel is used to recover the steps 1, 2 and 3 in case the original channel is closed. 
                            RecoverExchanges(model);
                            RecoverQueues(model);
                            RecoverBindings(model);
                        }

                        // Original IModel is used to recover and take over the original consumers.
                        RecoverConsumers();
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

        private void RecoverExchanges(IModel model)
        {
            Dictionary<string, RecordedExchange> recordedExchangesCopy;
            lock (_recordedEntitiesLock)
            {
                recordedExchangesCopy = new Dictionary<string, RecordedExchange>(_recordedExchanges);
            }

            foreach (RecordedExchange rx in recordedExchangesCopy.Values)
            {
                try
                {
                    rx.Recover(model);
                }
                catch (Exception ex)
                {
                    HandleTopologyRecoveryException(new TopologyRecoveryException($"Caught an exception while recovering exchange {rx.Name}", ex));
                }
            }
        }

        private void RecoverQueues(IModel model)
        {
            Dictionary<string, RecordedQueue> recordedQueuesCopy;
            lock (_recordedEntitiesLock)
            {
                recordedQueuesCopy = new Dictionary<string, RecordedQueue>(_recordedQueues);
            }

            foreach (KeyValuePair<string, RecordedQueue> pair in recordedQueuesCopy)
            {
                string oldName = pair.Key;
                RecordedQueue rq = pair.Value;

                try
                {
                    rq.Recover(model);
                    string newName = rq.Name;

                    if (oldName != newName)
                    {
                        // Make sure server-named queues are re-added with their new names.
                        // We only remove old name after we've updated the bindings and consumers,
                        // plus only for server-named queues, both to make sure we don't lose
                        // anything to recover. MK.
                        UpdateBindingsDestination(oldName, newName);
                        UpdateConsumerQueue(oldName, newName);
                        // see rabbitmq/rabbitmq-dotnet-client#43
                        if (rq.IsServerNamed)
                        {
                            DeleteRecordedQueue(oldName);
                        }
                        RecordQueue(rq);

                        if (!_queueNameChangeAfterRecoveryWrapper.IsEmpty)
                        {
                            _queueNameChangeAfterRecoveryWrapper.Invoke(this, new QueueNameChangedAfterRecoveryEventArgs(oldName, newName));
                        }
                    }
                }
                catch (Exception ex)
                {
                    HandleTopologyRecoveryException(new TopologyRecoveryException($"Caught an exception while recovering queue {oldName}", ex));
                }
            }
        }

        private void RecoverBindings(IModel model)
        {
            HashSet<RecordedBinding> recordedBindingsCopy;
            lock (_recordedEntitiesLock)
            {
                recordedBindingsCopy = new HashSet<RecordedBinding>(_recordedBindings);
            }

            foreach (RecordedBinding b in recordedBindingsCopy)
            {
                try
                {
                    b.Recover(model);
                }
                catch (Exception ex)
                {
                    HandleTopologyRecoveryException(new TopologyRecoveryException($"Caught an exception while recovering binding between {b.Source} and {b.Destination}", ex));
                }
            }
        }

        private void RecoverConsumers()
        {
            Dictionary<string, RecordedConsumer> recordedConsumersCopy;
            lock (_recordedEntitiesLock)
            {
                recordedConsumersCopy = new Dictionary<string, RecordedConsumer>(_recordedConsumers);
            }

            foreach (KeyValuePair<string, RecordedConsumer> pair in recordedConsumersCopy)
            {
                string oldTag = pair.Key;
                RecordedConsumer cons = pair.Value;

                try
                {
                    cons.Recover();
                    string newTag = cons.ConsumerTag;
                    UpdateConsumer(oldTag, newTag, cons);

                    if (!_consumerTagChangeAfterRecoveryWrapper.IsEmpty)
                    {
                        _consumerTagChangeAfterRecoveryWrapper.Invoke(this, new ConsumerTagChangedAfterRecoveryEventArgs(oldTag, newTag));
                    }
                }
                catch (Exception ex)
                {
                    HandleTopologyRecoveryException(new TopologyRecoveryException($"Caught an exception while recovering consumer {oldTag} on queue {cons.Queue}", ex));
                }
            }
        }

        private void RecoverModels()
        {
            lock (_models)
            {
                foreach (AutorecoveringModel m in _models)
                {
                    m.AutomaticallyRecover(this);
                }
            }
        }
    }
}
