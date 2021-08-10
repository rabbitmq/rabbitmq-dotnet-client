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

namespace RabbitMQ.Client.Impl
{
    #nullable enable
    internal sealed class RecordedExchange : RecordedNamedEntity
    {
        private readonly IDictionary<string, object>? _arguments;
        private readonly bool _durable;
        private readonly string _type;

        public bool IsAutoDelete { get; }

        public RecordedExchange(AutorecoveringModel channel, string name, string type, bool durable, bool isAutoDelete, IDictionary<string, object>? arguments)
            : base(channel, name)
        {
            _type = type;
            _durable = durable;
            IsAutoDelete = isAutoDelete;
            _arguments = arguments;
        }

        public override void Recover(IModel model)
        {
            model.ExchangeDeclare(Name, _type, _durable, IsAutoDelete, _arguments);
        }

        public override string ToString()
        {
            return $"{GetType().Name}: name = '{Name}', type = '{_type}', durable = {_durable}, autoDelete = {IsAutoDelete}, arguments = '{_arguments}'";
        }
    }
}
