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

using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing.Impl
{
    internal sealed class BasicGetOk : Client.Impl.MethodBase
    {
        public ulong _deliveryTag;
        public bool _redelivered;
        public string _exchange;
        public string _routingKey;
        public uint _messageCount;

        public BasicGetOk()
        {
        }

        public BasicGetOk(ulong DeliveryTag, bool Redelivered, string Exchange, string RoutingKey, uint MessageCount)
        {
            _deliveryTag = DeliveryTag;
            _redelivered = Redelivered;
            _exchange = Exchange;
            _routingKey = RoutingKey;
            _messageCount = MessageCount;
        }

        public BasicGetOk(ReadOnlySpan<byte> span)
        {
            int offset = WireFormatting.ReadLonglong(span, out _deliveryTag);
            offset += WireFormatting.ReadBits(span.Slice(offset), out _redelivered);
            offset += WireFormatting.ReadShortstr(span.Slice(offset), out _exchange);
            offset += WireFormatting.ReadShortstr(span.Slice(offset), out _routingKey);
            WireFormatting.ReadLong(span.Slice(offset), out _messageCount);
        }

        public override ProtocolCommandId ProtocolCommandId => ProtocolCommandId.BasicGetOk;
        public override string ProtocolMethodName => "basic.get-ok";

        public override int WriteArgumentsTo(Span<byte> span)
        {
            int length = span.Length;
            int offset = WireFormatting.WriteLonglong(ref span.GetStart(), _deliveryTag);
            offset += WireFormatting.WriteBits(ref span.GetOffset(offset), _redelivered);
            offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), _exchange, length - offset);
            offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), _routingKey, length - offset);
            return offset + WireFormatting.WriteLong(ref span.GetOffset(offset), _messageCount);
        }

        public override int GetRequiredBufferSize()
        {
            int bufferSize = 8 + 1 + 1 + 1 + 4; // bytes for _deliveryTag, bit fields, length of _exchange, length of _routingKey, _messageCount
            bufferSize += WireFormatting.GetByteCount(_exchange); // _exchange in bytes
            bufferSize += WireFormatting.GetByteCount(_routingKey); // _routingKey in bytes
            return bufferSize;
        }
    }
}
