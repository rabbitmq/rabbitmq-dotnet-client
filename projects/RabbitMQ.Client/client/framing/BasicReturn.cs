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
    internal sealed class BasicReturn : Client.Impl.MethodBase
    {
        public ushort _replyCode;
        public string _replyText;
        public string _exchange;
        public string _routingKey;

        public BasicReturn()
        {
        }

        public BasicReturn(ushort ReplyCode, string ReplyText, string Exchange, string RoutingKey)
        {
            _replyCode = ReplyCode;
            _replyText = ReplyText;
            _exchange = Exchange;
            _routingKey = RoutingKey;
        }

        public BasicReturn(ReadOnlySpan<byte> span)
        {
            int offset = WireFormatting.ReadShort(span, out _replyCode);
            offset += WireFormatting.ReadShortstr(span.Slice(offset), out _replyText);
            offset += WireFormatting.ReadShortstr(span.Slice(offset), out _exchange);
            WireFormatting.ReadShortstr(span.Slice(offset), out _routingKey);
        }

        public override ProtocolCommandId ProtocolCommandId => ProtocolCommandId.BasicReturn;
        public override string ProtocolMethodName => "basic.return";

        public override int WriteArgumentsTo(Span<byte> span)
        {
            int length = span.Length;
            int offset = WireFormatting.WriteShort(ref span.GetStart(), _replyCode);
            offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), _replyText, length - offset);
            offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), _exchange, length - offset);
            return offset + WireFormatting.WriteShortstr(ref span.GetOffset(offset), _routingKey, length - offset);
        }

        public override int GetRequiredBufferSize()
        {
            int bufferSize = 2 + 1 + 1 + 1; // bytes for _replyCode, length of _replyText, length of _exchange, length of _routingKey
            bufferSize += WireFormatting.GetByteCount(_replyText); // _replyText in bytes
            bufferSize += WireFormatting.GetByteCount(_exchange); // _exchange in bytes
            bufferSize += WireFormatting.GetByteCount(_routingKey); // _routingKey in bytes
            return bufferSize;
        }
    }
}
