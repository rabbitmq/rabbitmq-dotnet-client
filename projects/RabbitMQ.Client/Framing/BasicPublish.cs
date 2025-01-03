// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing
{
    internal readonly struct BasicPublish : IOutgoingAmqpMethod
    {
        // deprecated
        // ushort _reserved1
        public readonly string _exchange;
        public readonly string _routingKey;
        public readonly bool _mandatory;
        public readonly bool _immediate;

        public BasicPublish(string Exchange, string RoutingKey, bool Mandatory, bool Immediate)
        {
            _exchange = Exchange;
            _routingKey = RoutingKey;
            _mandatory = Mandatory;
            _immediate = Immediate;
        }

        public ProtocolCommandId ProtocolCommandId => ProtocolCommandId.BasicPublish;

        public int WriteTo(Span<byte> span)
        {
            int offset = WireFormatting.WriteShort(ref span.GetStart(), default);
            offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), _exchange);
            offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), _routingKey);
            return offset + WireFormatting.WriteBits(ref span.GetOffset(offset), _mandatory, _immediate);
        }

        public int GetRequiredBufferSize()
        {
            int bufferSize = 2 + 1 + 1 + 1; // bytes for _reserved1, length of _exchange, length of _routingKey, bit fields
            bufferSize += WireFormatting.GetByteCount(_exchange); // _exchange in bytes
            bufferSize += WireFormatting.GetByteCount(_routingKey); // _routingKey in bytes
            return bufferSize;
        }
    }

    internal readonly struct BasicPublishMemory : IOutgoingAmqpMethod
    {
        // deprecated
        // ushort _reserved1
        public readonly ReadOnlyMemory<byte> _exchange;
        public readonly ReadOnlyMemory<byte> _routingKey;
        public readonly bool _mandatory;
        public readonly bool _immediate;

        public BasicPublishMemory(ReadOnlyMemory<byte> Exchange, ReadOnlyMemory<byte> RoutingKey, bool Mandatory, bool Immediate)
        {
            _exchange = Exchange;
            _routingKey = RoutingKey;
            _mandatory = Mandatory;
            _immediate = Immediate;
        }

        public ProtocolCommandId ProtocolCommandId => ProtocolCommandId.BasicPublish;

        public int WriteTo(Span<byte> span)
        {
            int offset = WireFormatting.WriteShort(ref span.GetStart(), default);
            offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), _exchange.Span);
            offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), _routingKey.Span);
            return offset + WireFormatting.WriteBits(ref span.GetOffset(offset), _mandatory, _immediate);
        }

        public int GetRequiredBufferSize()
        {
            return 2 + 1 + 1 + 1 + // bytes for _reserved1, length of _exchange, length of _routingKey, bit fields
                   _exchange.Length + _routingKey.Length;
        }
    }
}
