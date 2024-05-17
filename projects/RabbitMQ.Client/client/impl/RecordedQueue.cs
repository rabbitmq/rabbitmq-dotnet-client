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
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
#nullable enable
    internal readonly struct RecordedQueue : IRecordedQueue
    {
        private readonly QueueName _name;
        private readonly IDictionary<string, object>? _arguments;
        private readonly bool _durable;
        private readonly bool _exclusive;
        private readonly bool _autoDelete;
        private readonly bool _isServerNamed;

        public QueueName Name => _name;
        public bool AutoDelete => _autoDelete;
        public bool IsServerNamed => _isServerNamed;
        public bool Durable => _durable;
        public bool Exclusive => _exclusive;
        public IDictionary<string, object>? Arguments => _arguments;

        public RecordedQueue(QueueName queueName, bool isServerNamed, bool durable, bool exclusive, bool autoDelete,
            IDictionary<string, object>? arguments)
        {
            _name = queueName;
            _isServerNamed = isServerNamed;
            _durable = durable;
            _exclusive = exclusive;
            _autoDelete = autoDelete;
            _arguments = arguments;
        }

        public RecordedQueue(QueueName newName, in RecordedQueue old)
        {
            _name = newName;
            _isServerNamed = old._isServerNamed;
            _durable = old._durable;
            _exclusive = old._exclusive;
            _autoDelete = old._autoDelete;
            _arguments = old._arguments;
        }

        public Task<QueueDeclareOk> RecoverAsync(IChannel channel, CancellationToken cancellationToken)
        {
            QueueName queueName = _name;
            if (_name.IsEmpty)
            {
                queueName = QueueName.Empty;
            }
            return channel.QueueDeclareAsync(queue: queueName, passive: false,
                durable: _durable, exclusive: _exclusive, autoDelete: AutoDelete, arguments: _arguments,
                cancellationToken: cancellationToken);
        }

        public override string ToString()
        {
            return $"{nameof(RecordedQueue)}: name = '{Name}', durable = {_durable}, exclusive = {_exclusive}, autoDelete = {AutoDelete}, arguments = '{_arguments}'";
        }
    }
}
