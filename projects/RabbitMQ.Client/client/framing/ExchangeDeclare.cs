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
using System.Collections.Generic;
using System.Text;
using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing.Impl
{
    internal sealed class ExchangeDeclare : MethodBase
    {
        public ushort _reserved1;
        public string _exchange;
        public string _type;
        public bool _passive;
        public bool _durable;
        public bool _autoDelete;
        public bool _internal;
        public bool _nowait;
        public IDictionary<string, object> _arguments;

        public ExchangeDeclare()
        {
        }

        public ExchangeDeclare(ushort Reserved1, string Exchange, string Type, bool Passive, bool Durable, bool AutoDelete, bool Internal, bool Nowait, IDictionary<string, object> Arguments)
        {
            _reserved1 = Reserved1;
            _exchange = Exchange;
            _type = Type;
            _passive = Passive;
            _durable = Durable;
            _autoDelete = AutoDelete;
            _internal = Internal;
            _nowait = Nowait;
            _arguments = Arguments;
        }

        public ExchangeDeclare(ReadOnlySpan<byte> span)
        {
            int offset = WireFormatting.ReadShort(span, out _reserved1);
            offset += WireFormatting.ReadShortstr(span.Slice(offset), out _exchange);
            offset += WireFormatting.ReadShortstr(span.Slice(offset), out _type);
            offset += WireFormatting.ReadBits(span.Slice(offset), out _passive, out _durable, out _autoDelete, out _internal, out _nowait);
            WireFormatting.ReadDictionary(span.Slice(offset), out var tmpDictionary);
            _arguments = tmpDictionary;
        }

        public override ProtocolCommandId ProtocolCommandId => ProtocolCommandId.ExchangeDeclare;
        public override string ProtocolMethodName => "exchange.declare";
        public override bool HasContent => false;

        public override int WriteArgumentsTo(Span<byte> span)
        {
            int offset = WireFormatting.WriteShort(span, _reserved1);
            offset += WireFormatting.WriteShortstr(span.Slice(offset), _exchange);
            offset += WireFormatting.WriteShortstr(span.Slice(offset), _type);
            offset += WireFormatting.WriteBits(span.Slice(offset), _passive, _durable, _autoDelete, _internal, _nowait);
            return offset + WireFormatting.WriteTable(span.Slice(offset), _arguments);
        }

        public override int GetRequiredBufferSize()
        {
            int bufferSize = 2 + 1 + 1 + 1; // bytes for _reserved1, length of _exchange, length of _type, bit fields
            bufferSize += WireFormatting.GetUTF8ByteCount(_exchange); // _exchange in bytes
            bufferSize += WireFormatting.GetUTF8ByteCount(_type); // _type in bytes
            bufferSize += WireFormatting.GetTableByteCount(_arguments); // _arguments in bytes
            return bufferSize;
        }
    }
}
