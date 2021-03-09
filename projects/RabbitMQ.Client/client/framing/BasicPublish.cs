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
using System.Runtime.CompilerServices;

using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing.Impl
{
    internal sealed class BasicPublish : Client.Impl.MethodBase
    {
        // deprecated
        // ushort _reserved1
        public string _exchange;
        public string _routingKey;
        public bool _mandatory;
        public bool _immediate;

        public BasicPublish()
        {
        }

        public BasicPublish(string Exchange, string RoutingKey, bool Mandatory, bool Immediate)
        {
            _exchange = Exchange;
            _routingKey = RoutingKey;
            _mandatory = Mandatory;
            _immediate = Immediate;
        }

        public BasicPublish(ReadOnlySpan<byte> span)
        {
            int offset = 2;
            offset += WireFormatting.ReadShortstr(span, offset, out _exchange);
            offset += WireFormatting.ReadShortstr(span, offset, out _routingKey);
            WireFormatting.ReadBits(span, offset, out _mandatory, out _immediate);
        }

        public override ProtocolCommandId ProtocolCommandId => ProtocolCommandId.BasicPublish;
        public override string ProtocolMethodName => "basic.publish";
        public override bool HasContent => true;

        public override int WriteArgumentsTo(Span<byte> span)
        {
            int offset = WireFormatting.WriteShort(span, 0, default);
            offset += WireFormatting.WriteShortstr(span, offset, _exchange);
            offset += WireFormatting.WriteShortstr(span, offset, _routingKey);
            return offset + WireFormatting.WriteBits(ref span[offset], _mandatory, _immediate);
        }

        public override int GetRequiredBufferSize()
        {
            int bufferSize = 2 + 1 + 1 + 1; // bytes for _reserved1, length of _exchange, length of _routingKey, bit fields
            bufferSize += WireFormatting.GetByteCount(_exchange); // _exchange in bytes
            bufferSize += WireFormatting.GetByteCount(_routingKey); // _routingKey in bytes
            return bufferSize;
        }
    }

    internal sealed class BasicPublishMemory : Client.Impl.MethodBase
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

        public override ProtocolCommandId ProtocolCommandId => ProtocolCommandId.BasicPublish;
        public override string ProtocolMethodName => "basic.publish";
        public override bool HasContent => true;

        public override int WriteArgumentsTo(Span<byte> span)
        {
            int offset = WireFormatting.WriteShort(span, 0, default);
            offset += WireFormatting.WriteShortstr(span, offset, _exchange.Span);
            offset += WireFormatting.WriteShortstr(span, offset, _routingKey.Span);
            return offset + WireFormatting.WriteBits(ref span[offset], _mandatory, _immediate);
        }

        public override int GetRequiredBufferSize()
        {
            return 2 + 1 + 1 + 1 + // bytes for _reserved1, length of _exchange, length of _routingKey, bit fields
                   _exchange.Length + _routingKey.Length;
        }
    }
}
