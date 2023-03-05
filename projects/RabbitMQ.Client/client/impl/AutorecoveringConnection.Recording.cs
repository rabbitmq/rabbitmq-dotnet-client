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

using System.Collections.Generic;
using System.Linq;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing.Impl
{
#nullable enable
    internal sealed partial class AutorecoveringConnection
    {
        private readonly object _recordedEntitiesLock = new object();
        private readonly Dictionary<string, RecordedExchange> _recordedExchanges = new Dictionary<string, RecordedExchange>();
        private readonly Dictionary<string, RecordedQueue> _recordedQueues = new Dictionary<string, RecordedQueue>();
        private readonly HashSet<RecordedBinding> _recordedBindings = new HashSet<RecordedBinding>();
        private readonly Dictionary<string, RecordedConsumer> _recordedConsumers = new Dictionary<string, RecordedConsumer>();
        private readonly List<AutorecoveringChannel> _channels = new List<AutorecoveringChannel>();

        internal int RecordedExchangesCount => _recordedExchanges.Count;

        internal void RecordExchange(in RecordedExchange exchange)
        {
            lock (_recordedEntitiesLock)
            {
                _recordedExchanges[exchange.Name] = exchange;
            }
        }

        internal void DeleteRecordedExchange(string exchangeName)
        {
            lock (_recordedEntitiesLock)
            {
                _recordedExchanges.Remove(exchangeName);

                // find bindings that need removal, check if some auto-delete exchanges might need the same
                foreach (RecordedBinding binding in _recordedBindings.ToArray())
                {
                    if (binding.Destination == exchangeName)
                    {
                        DeleteRecordedBinding(binding);
                        DeleteAutoDeleteExchange(binding.Source);
                    }
                }
            }
        }

        internal void DeleteAutoDeleteExchange(string exchangeName)
        {
            lock (_recordedEntitiesLock)
            {
                if (_recordedExchanges.TryGetValue(exchangeName, out var recordedExchange) && recordedExchange.AutoDelete)
                {
                    if (!AnyBindingsOnExchange(exchangeName))
                    {
                        // last binding where this exchange is the source is gone, remove recorded exchange if it is auto-deleted.
                        _recordedExchanges.Remove(exchangeName);
                    }
                }
            }

            bool AnyBindingsOnExchange(string exchange)
            {
                foreach (var recordedBinding in _recordedBindings)
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

        internal void RecordQueue(in RecordedQueue queue)
        {
            lock (_recordedEntitiesLock)
            {
                _recordedQueues[queue.Name] = queue;
            }
        }

        internal void DeleteRecordedQueue(string queueName)
        {
            lock (_recordedEntitiesLock)
            {
                _recordedQueues.Remove(queueName);

                // find bindings that need removal, check if some auto-delete exchanges might need the same
                foreach (var binding in _recordedBindings.ToArray())
                {
                    if (binding.Destination == queueName)
                    {
                        DeleteRecordedBinding(binding);
                        DeleteAutoDeleteExchange(binding.Source);
                    }
                }
            }
        }

        private void UpdateBindingsDestination(string oldName, string newName)
        {
            lock (_recordedEntitiesLock)
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
        }

        private void UpdateConsumerQueue(string oldName, string newName)
        {
            lock (_recordedEntitiesLock)
            {
                foreach (RecordedConsumer consumer in _recordedConsumers.Values.ToArray())
                {
                    if (consumer.Queue == oldName)
                    {
                        _recordedConsumers[consumer.ConsumerTag] = RecordedConsumer.WithNewQueueNameTag(newName, consumer);
                    }
                }
            }
        }

        internal void RecordBinding(in RecordedBinding rb)
        {
            lock (_recordedEntitiesLock)
            {
                _recordedBindings.Add(rb);
            }
        }

        internal void DeleteRecordedBinding(in RecordedBinding rb)
        {
            lock (_recordedEntitiesLock)
            {
                _recordedBindings.Remove(rb);
            }
        }

        internal void RecordConsumer(in RecordedConsumer consumer)
        {
            if (!_config.TopologyRecoveryEnabled)
            {
                return;
            }

            lock (_recordedEntitiesLock)
            {
                _recordedConsumers[consumer.ConsumerTag] = consumer;
            }
        }

        internal void DeleteRecordedConsumer(string consumerTag)
        {
            if (!_config.TopologyRecoveryEnabled)
            {
                return;
            }

            lock (_recordedEntitiesLock)
            {
                if (_recordedConsumers.Remove(consumerTag, out var recordedConsumer))
                {
                    DeleteAutoDeleteQueue(recordedConsumer.Queue);
                }
            }

            void DeleteAutoDeleteQueue(string queue)
            {
                if (_recordedQueues.TryGetValue(queue, out var recordedQueue) && recordedQueue.AutoDelete)
                {
                    // last consumer on this connection is gone, remove recorded queue if it is auto-deleted.
                    if (!AnyConsumersOnQueue(queue))
                    {
                        _recordedQueues.Remove(queue);
                    }
                }
            }

            bool AnyConsumersOnQueue(string queue)
            {
                foreach (var pair in _recordedConsumers)
                {
                    if (pair.Value.Queue == queue)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private void UpdateConsumer(string oldTag, string newTag, in RecordedConsumer consumer)
        {
            lock (_recordedEntitiesLock)
            {
                // make sure server-generated tags are re-added
                _recordedConsumers.Remove(oldTag);
                _recordedConsumers.Add(newTag, consumer);
            }
        }

        private void RecordChannel(AutorecoveringChannel m)
        {
            lock (_channels)
            {
                _channels.Add(m);
            }
        }

        internal void DeleteRecordedChannel(AutorecoveringChannel channel)
        {
            lock (_channels)
            {
                _channels.Remove(channel);
            }
        }
    }
}
