// This source code is dual-licensed under the Apache License, version
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
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    internal readonly struct RecordedConsumer : IRecordedConsumer
    {
        private readonly AutorecoveringChannel _channel;
        private readonly IAsyncBasicConsumer _consumer;
        private readonly string _queue;
        private readonly bool _autoAck;
        private readonly string _consumerTag;
        private readonly bool _exclusive;
        private readonly IDictionary<string, object?>? _arguments;

        public RecordedConsumer(AutorecoveringChannel channel, IAsyncBasicConsumer consumer, string consumerTag, string queue, bool autoAck, bool exclusive, IDictionary<string, object?>? arguments)
        {
            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel));
            }
            _channel = channel;

            if (consumer == null)
            {
                throw new ArgumentNullException(nameof(consumer));
            }
            _consumer = consumer;

            if (queue is null)
            {
                throw new ArgumentNullException(nameof(queue));
            }
            else
            {
                if (queue == string.Empty)
                {
                    _queue = _channel.CurrentQueue ?? string.Empty;
                }
                else
                {
                    _queue = queue;
                }
            }

            if (string.IsNullOrEmpty(consumerTag))
            {
                throw new ArgumentNullException(nameof(consumerTag));
            }
            _consumerTag = consumerTag;

            _autoAck = autoAck;
            _exclusive = exclusive;

            if (arguments is null)
            {
                _arguments = null;
            }
            else
            {
                _arguments = new Dictionary<string, object?>(arguments);
            }
        }

        public AutorecoveringChannel Channel => _channel;
        public IAsyncBasicConsumer Consumer => _consumer;
        public string Queue => _queue;
        public bool AutoAck => _autoAck;
        public string ConsumerTag => _consumerTag;
        public bool Exclusive => _exclusive;
        public IDictionary<string, object?>? Arguments => _arguments;

        public static RecordedConsumer WithNewConsumerTag(string newTag, in RecordedConsumer old)
        {
            return new RecordedConsumer(old.Channel, old.Consumer, newTag, old.Queue, old.AutoAck, old.Exclusive, old.Arguments);
        }

        public static RecordedConsumer WithNewQueueName(string newQueueName, in RecordedConsumer old)
        {
            return new RecordedConsumer(old.Channel, old.Consumer, old.ConsumerTag, newQueueName, old.AutoAck, old.Exclusive, old.Arguments);
        }

        public Task<string> RecoverAsync(IChannel channel)
        {
            return channel.BasicConsumeAsync(Queue, AutoAck, ConsumerTag, false, Exclusive, Arguments, Consumer);
        }
    }
}
