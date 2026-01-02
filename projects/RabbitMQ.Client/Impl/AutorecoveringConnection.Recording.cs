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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing
{
    internal sealed partial class AutorecoveringConnection
    {
        private readonly SemaphoreSlim _recordedEntitiesSemaphore = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _channelsSemaphore = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, RecordedExchange> _recordedExchanges = new Dictionary<string, RecordedExchange>();
        private readonly Dictionary<string, RecordedQueue> _recordedQueues = new Dictionary<string, RecordedQueue>();
        private readonly HashSet<RecordedBinding> _recordedBindings = new HashSet<RecordedBinding>();
        private readonly Dictionary<string, RecordedConsumer> _recordedConsumers = new Dictionary<string, RecordedConsumer>();
        private readonly List<AutorecoveringChannel> _channels = new List<AutorecoveringChannel>();

        internal int RecordedExchangesCount => _recordedExchanges.Count;

        internal async ValueTask RecordExchangeAsync(RecordedExchange exchange,
            bool recordedEntitiesSemaphoreHeld)
        {
            if (_disposed)
            {
                return;
            }

            if (recordedEntitiesSemaphoreHeld)
            {
                DoRecordExchange(exchange);
            }
            else
            {
                await _recordedEntitiesSemaphore.WaitAsync()
                    .ConfigureAwait(false);
                try
                {
                    DoRecordExchange(exchange);
                }
                finally
                {
                    _recordedEntitiesSemaphore.Release();
                }
            }
        }

        private void DoRecordExchange(in RecordedExchange exchange)
        {
            _recordedExchanges[exchange.Name] = exchange;
        }

        internal async ValueTask DeleteRecordedExchangeAsync(string exchangeName,
            bool recordedEntitiesSemaphoreHeld, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                return;
            }

            if (recordedEntitiesSemaphoreHeld)
            {
                await DoDeleteRecordedExchangeAsync(exchangeName, cancellationToken)
                        .ConfigureAwait(false);
            }
            else
            {
                await _recordedEntitiesSemaphore.WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    await DoDeleteRecordedExchangeAsync(exchangeName, cancellationToken)
                        .ConfigureAwait(false);
                }
                finally
                {
                    _recordedEntitiesSemaphore.Release();
                }
            }

            async Task DoDeleteRecordedExchangeAsync(string exchangeName, CancellationToken cancellationToken)
            {
                _recordedExchanges.Remove(exchangeName);

                // find bindings that need removal, check if some auto-delete exchanges might need the same
                foreach (RecordedBinding binding in _recordedBindings.ToArray())
                {
                    if (binding.Destination == exchangeName)
                    {
                        await DeleteRecordedBindingAsync(binding,
                            recordedEntitiesSemaphoreHeld: true, cancellationToken)
                                .ConfigureAwait(false);
                        await DeleteAutoDeleteExchangeAsync(binding.Source,
                            recordedEntitiesSemaphoreHeld: true, cancellationToken)
                                .ConfigureAwait(false);
                    }
                }
            }
        }

        internal async ValueTask DeleteAutoDeleteExchangeAsync(string exchangeName,
            bool recordedEntitiesSemaphoreHeld, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                return;
            }

            if (recordedEntitiesSemaphoreHeld)
            {
                DoDeleteAutoDeleteExchange(exchangeName);
            }
            else
            {
                await _recordedEntitiesSemaphore.WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    DoDeleteAutoDeleteExchange(exchangeName);
                }
                finally
                {
                    _recordedEntitiesSemaphore.Release();
                }
            }
        }

        private void DoDeleteAutoDeleteExchange(string exchangeName)
        {
            if (_recordedExchanges.TryGetValue(exchangeName, out var recordedExchange) && recordedExchange.AutoDelete)
            {
                if (!AnyBindingsOnExchange(exchangeName))
                {
                    // last binding where this exchange is the source is gone, remove recorded exchange if it is auto-deleted.
                    _recordedExchanges.Remove(exchangeName);
                }
            }

            bool AnyBindingsOnExchange(string exchange)
            {
                foreach (RecordedBinding recordedBinding in _recordedBindings)
                {
                    if (recordedBinding.Source == exchange)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        internal int RecordedQueuesCount => _recordedQueues.Count;

        internal async ValueTask RecordQueueAsync(RecordedQueue queue,
            bool recordedEntitiesSemaphoreHeld, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                return;
            }

            if (recordedEntitiesSemaphoreHeld)
            {
                DoRecordQueue(queue);
            }
            else
            {
                await _recordedEntitiesSemaphore.WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    DoRecordQueue(queue);
                }
                finally
                {
                    _recordedEntitiesSemaphore.Release();
                }
            }
        }

        private void DoRecordQueue(RecordedQueue queue)
        {
            _recordedQueues[queue.Name] = queue;
        }

        internal async ValueTask DeleteRecordedQueueAsync(string queueName,
            bool recordedEntitiesSemaphoreHeld, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                return;
            }

            if (recordedEntitiesSemaphoreHeld)
            {
                await DoDeleteRecordedQueueAsync(queueName, cancellationToken)
                        .ConfigureAwait(false);
            }
            else
            {
                await _recordedEntitiesSemaphore.WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    await DoDeleteRecordedQueueAsync(queueName, cancellationToken)
                            .ConfigureAwait(false);
                }
                finally
                {
                    _recordedEntitiesSemaphore.Release();
                }
            }

            async ValueTask DoDeleteRecordedQueueAsync(string queueName, CancellationToken cancellationToken)
            {
                _recordedQueues.Remove(queueName);

                // find bindings that need removal, check if some auto-delete exchanges might need the same
                foreach (RecordedBinding binding in _recordedBindings.ToArray())
                {
                    if (binding.Destination == queueName)
                    {
                        await DeleteRecordedBindingAsync(binding,
                            recordedEntitiesSemaphoreHeld: true, cancellationToken)
                                .ConfigureAwait(false);
                        await DeleteAutoDeleteExchangeAsync(binding.Source,
                            recordedEntitiesSemaphoreHeld: true, cancellationToken)
                                .ConfigureAwait(false);
                    }
                }
            }

        }

        internal async ValueTask RecordBindingAsync(RecordedBinding binding,
            bool recordedEntitiesSemaphoreHeld)
        {
            if (_disposed)
            {
                return;
            }

            if (recordedEntitiesSemaphoreHeld)
            {
                DoRecordBinding(binding);
            }
            else
            {
                await _recordedEntitiesSemaphore.WaitAsync()
                    .ConfigureAwait(false);
                try
                {
                    DoRecordBinding(binding);
                }
                finally
                {
                    _recordedEntitiesSemaphore.Release();
                }
            }
        }

        private void DoRecordBinding(in RecordedBinding binding)
        {
            _recordedBindings.Add(binding);
        }

        internal async ValueTask DeleteRecordedBindingAsync(RecordedBinding rb,
            bool recordedEntitiesSemaphoreHeld, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                return;
            }

            if (recordedEntitiesSemaphoreHeld)
            {
                DoDeleteRecordedBinding(rb);
            }
            else
            {
                await _recordedEntitiesSemaphore.WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    DoDeleteRecordedBinding(rb);
                }
                finally
                {
                    _recordedEntitiesSemaphore.Release();
                }
            }
        }

        private void DoDeleteRecordedBinding(in RecordedBinding rb)
        {
            _recordedBindings.Remove(rb);
        }

        internal async ValueTask RecordConsumerAsync(RecordedConsumer consumer,
            bool recordedEntitiesSemaphoreHeld)
        {
            if (_disposed)
            {
                return;
            }

            if (!_config.TopologyRecoveryEnabled)
            {
                return;
            }

            if (recordedEntitiesSemaphoreHeld)
            {
                DoRecordConsumer(consumer);
            }
            else
            {
                await _recordedEntitiesSemaphore.WaitAsync()
                    .ConfigureAwait(false);
                try
                {
                    DoRecordConsumer(consumer);
                }
                finally
                {
                    _recordedEntitiesSemaphore.Release();
                }
            }
        }

        private void DoRecordConsumer(in RecordedConsumer consumer)
        {
            _recordedConsumers[consumer.ConsumerTag] = consumer;
        }

        internal async ValueTask DeleteRecordedConsumerAsync(string consumerTag,
            bool recordedEntitiesSemaphoreHeld)
        {
            if (_disposed)
            {
                return;
            }

            if (!_config.TopologyRecoveryEnabled)
            {
                return;
            }

            if (recordedEntitiesSemaphoreHeld)
            {
                DoDeleteRecordedConsumer(consumerTag);
            }
            else
            {
                await _recordedEntitiesSemaphore.WaitAsync()
                    .ConfigureAwait(false);
                try
                {
                    DoDeleteRecordedConsumer(consumerTag);
                }
                finally
                {
                    _recordedEntitiesSemaphore.Release();
                }
            }
        }

        private void DoDeleteRecordedConsumer(string consumerTag)
        {
            if (consumerTag is null)
            {
                throw new ArgumentNullException(nameof(consumerTag));
            }

            if (_recordedConsumers.Remove(consumerTag, out RecordedConsumer recordedConsumer))
            {
                DeleteAutoDeleteQueue(recordedConsumer.Queue);
            }
        }

        private void DeleteAutoDeleteQueue(string queue)
        {
            if (_recordedQueues.TryGetValue(queue, out RecordedQueue recordedQueue) && recordedQueue.AutoDelete)
            {
                // last consumer on this connection is gone, remove recorded queue if it is auto-deleted.
                if (!AnyConsumersOnQueue(queue))
                {
                    _recordedQueues.Remove(queue);
                }
            }
        }

        private bool AnyConsumersOnQueue(string queue)
        {
            foreach (KeyValuePair<string, RecordedConsumer> pair in _recordedConsumers)
            {
                if (pair.Value.Queue == queue)
                {
                    return true;
                }
            }

            return false;
        }

        private async Task RecordChannelAsync(AutorecoveringChannel channel,
            bool channelsSemaphoreHeld, CancellationToken cancellationToken)
        {
            if (channelsSemaphoreHeld)
            {
                DoAddRecordedChannel(channel);
            }
            else
            {
                await _channelsSemaphore.WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    DoAddRecordedChannel(channel);
                }
                finally
                {
                    _channelsSemaphore.Release();
                }
            }
        }

        private void DoAddRecordedChannel(AutorecoveringChannel channel)
        {
            _channels.Add(channel);
        }

        internal async Task DeleteRecordedChannelAsync(AutorecoveringChannel channel,
            bool channelsSemaphoreHeld, bool recordedEntitiesSemaphoreHeld)
        {
            if (_disposed)
            {
                return;
            }

            if (recordedEntitiesSemaphoreHeld)
            {
                await DoDeleteRecordedConsumersAsync(channel)
                    .ConfigureAwait(false);
            }
            else
            {
                await _recordedEntitiesSemaphore.WaitAsync()
                    .ConfigureAwait(false);
                try
                {
                    await DoDeleteRecordedConsumersAsync(channel)
                        .ConfigureAwait(false);
                }
                finally
                {
                    _recordedEntitiesSemaphore.Release();
                }
            }

            if (channelsSemaphoreHeld)
            {
                DoDeleteRecordedChannel(channel);
            }
            else
            {
                await _channelsSemaphore.WaitAsync()
                    .ConfigureAwait(false);
                try
                {
                    DoDeleteRecordedChannel(channel);
                }
                finally
                {
                    _channelsSemaphore.Release();
                }
            }
        }

        private async Task DoDeleteRecordedConsumersAsync(AutorecoveringChannel channel)
        {
            foreach (string ct in channel.ConsumerTags)
            {
                await DeleteRecordedConsumerAsync(ct, recordedEntitiesSemaphoreHeld: true)
                    .ConfigureAwait(false);
            }
        }

        private void DoDeleteRecordedChannel(in AutorecoveringChannel channel)
        {
            _channels.Remove(channel);
        }
    }
}
