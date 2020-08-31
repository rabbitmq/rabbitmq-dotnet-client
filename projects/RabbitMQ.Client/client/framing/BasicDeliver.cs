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
using System.Buffers.Binary;
using System.Text;
using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing.Impl
{
    internal sealed class BasicDeliver : Client.Impl.MethodBase
    {
        public string _consumerTag;
        public ulong _deliveryTag;
        public bool _redelivered;
        public string _exchange;
        public string _routingKey;

        public BasicDeliver()
        {
        }

        public BasicDeliver(string ConsumerTag, ulong DeliveryTag, bool Redelivered, string Exchange, string RoutingKey)
        {
            _consumerTag = ConsumerTag;
            _deliveryTag = DeliveryTag;
            _redelivered = Redelivered;
            _exchange = Exchange;
            _routingKey = RoutingKey;
        }

        public BasicDeliver(ReadOnlySpan<byte> span)
        {
            int offset = WireFormatting.ReadShortstr(span, out _consumerTag);
            offset += WireFormatting.ReadLonglong(span.Slice(offset), out _deliveryTag);
            offset += WireFormatting.ReadBits(span.Slice(offset), out _redelivered);
            offset += WireFormatting.ReadShortstr(span.Slice(offset), out _exchange);
            WireFormatting.ReadShortstr(span.Slice(offset), out _routingKey);
        }

        public override ProtocolCommandId ProtocolCommandId => ProtocolCommandId.BasicDeliver;
        public override string ProtocolMethodName => "basic.deliver";
        public override bool HasContent => true;

        public override int WriteArgumentsTo(Span<byte> span)
        {
            int offset = WireFormatting.WriteShortstr(span, _consumerTag);
            offset += WireFormatting.WriteLonglong(span.Slice(offset), _deliveryTag);
            offset += WireFormatting.WriteBits(span.Slice(offset), _redelivered);
            offset += WireFormatting.WriteShortstr(span.Slice(offset), _exchange);
            return offset + WireFormatting.WriteShortstr(span.Slice(offset), _routingKey);
        }

        public override int GetRequiredBufferSize()
        {
            int bufferSize = 1 + 8 + 1 + 1 + 1; // bytes for length of _consumerTag, _deliveryTag, bit fields, length of _exchange, length of _routingKey
            bufferSize += Encoding.UTF8.GetByteCount(_consumerTag); // _consumerTag in bytes
            bufferSize += Encoding.UTF8.GetByteCount(_exchange); // _exchange in bytes
            bufferSize += Encoding.UTF8.GetByteCount(_routingKey); // _routingKey in bytes
            return bufferSize;
        }
    }
}
