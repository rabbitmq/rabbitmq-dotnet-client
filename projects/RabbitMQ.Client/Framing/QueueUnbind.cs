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
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing
{
    internal readonly struct QueueUnbind : IOutgoingAmqpMethod
    {
        // deprecated
        // ushort _reserved1
        public readonly string _queue;
        public readonly string _exchange;
        public readonly string _routingKey;
        public readonly IDictionary<string, object?>? _arguments;

        public QueueUnbind(string Queue, string Exchange, string RoutingKey, IDictionary<string, object?>? Arguments)
        {
            _queue = Queue;
            _exchange = Exchange;
            _routingKey = RoutingKey;
            _arguments = Arguments;
        }

        public ProtocolCommandId ProtocolCommandId => ProtocolCommandId.QueueUnbind;

        public int WriteTo(Span<byte> span)
        {
            int offset = WireFormatting.WriteShort(ref span.GetStart(), default);
            offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), _queue);
            offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), _exchange);
            offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), _routingKey);
            return offset + WireFormatting.WriteTable(ref span.GetOffset(offset), _arguments);
        }

        public int GetRequiredBufferSize()
        {
            int bufferSize = 2 + 1 + 1 + 1; // bytes for _reserved1, length of _queue, length of _exchange, length of _routingKey
            bufferSize += WireFormatting.GetByteCount(_queue); // _queue in bytes
            bufferSize += WireFormatting.GetByteCount(_exchange); // _exchange in bytes
            bufferSize += WireFormatting.GetByteCount(_routingKey); // _routingKey in bytes
            bufferSize += WireFormatting.GetTableByteCount(_arguments); // _arguments in bytes
            return bufferSize;
        }
    }
}
