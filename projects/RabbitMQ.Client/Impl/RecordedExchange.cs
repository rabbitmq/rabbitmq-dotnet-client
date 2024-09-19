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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    internal readonly struct RecordedExchange : IRecordedExchange
    {
        private readonly string _name;
        private readonly string _type;
        private readonly bool _durable;
        private readonly bool _autoDelete;
        private readonly IDictionary<string, object?>? _arguments;

        public string Name => _name;
        public bool AutoDelete => _autoDelete;
        public string Type => _type;
        public bool Durable => _durable;
        public IDictionary<string, object?>? Arguments => _arguments;

        public RecordedExchange(string name, string type, bool durable, bool autoDelete, IDictionary<string, object?>? arguments)
        {
            _name = name;
            _type = type;
            _durable = durable;
            _autoDelete = autoDelete;

            if (arguments is null)
            {
                _arguments = null;
            }
            else
            {
                _arguments = new Dictionary<string, object?>(arguments);
            }
        }

        public Task RecoverAsync(IChannel channel, CancellationToken cancellationToken)
        {
            return channel.ExchangeDeclareAsync(exchange: Name, type: _type, passive: false,
                durable: _durable, autoDelete: AutoDelete, noWait: false, arguments: _arguments,
                cancellationToken: cancellationToken);
        }

        public override string ToString()
        {
            return $"{nameof(RecordedExchange)}: name = '{Name}', type = '{_type}', durable = {_durable}, autoDelete = {AutoDelete}, arguments = '{_arguments}'";
        }
    }
}
