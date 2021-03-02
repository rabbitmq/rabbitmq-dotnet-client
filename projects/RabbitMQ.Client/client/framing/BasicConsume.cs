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

using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing.Impl
{
    internal sealed class BasicConsume : MethodBase
    {
        // deprecated
        // ushort _reserved1
        public string _queue;
        public string _consumerTag;
        public bool _noLocal;
        public bool _noAck;
        public bool _exclusive;
        public bool _nowait;
        public IDictionary<string, object> _arguments;

        public BasicConsume()
        {
        }

        public BasicConsume(string Queue, string ConsumerTag, bool NoLocal, bool NoAck, bool Exclusive, bool Nowait, IDictionary<string, object> Arguments)
        {
            _queue = Queue;
            _consumerTag = ConsumerTag;
            _noLocal = NoLocal;
            _noAck = NoAck;
            _exclusive = Exclusive;
            _nowait = Nowait;
            _arguments = Arguments;
        }

        public BasicConsume(ReadOnlySpan<byte> span)
        {
            int offset = 2;
            offset += WireFormatting.ReadShortstr(span, offset, out _queue);
            offset += WireFormatting.ReadShortstr(span, offset, out _consumerTag);
            offset += WireFormatting.ReadBits(span, offset, out _noLocal, out _noAck, out _exclusive, out _nowait);
            WireFormatting.ReadDictionary(span, offset, out var tmpDictionary);
            _arguments = tmpDictionary;
        }

        public override ProtocolCommandId ProtocolCommandId => ProtocolCommandId.BasicConsume;
        public override string ProtocolMethodName => "basic.consume";
        public override bool HasContent => false;

        public override int WriteArgumentsTo(Span<byte> span)
        {
            int offset = WireFormatting.WriteShort(span, 0, default);
            offset += WireFormatting.WriteShortstr(span, offset, _queue);
            offset += WireFormatting.WriteShortstr(span, offset, _consumerTag);
            offset += WireFormatting.WriteBits(ref span[offset], _noLocal, _noAck, _exclusive, _nowait);
            return offset + WireFormatting.WriteTable(span, offset, _arguments);
        }

        public override int GetRequiredBufferSize()
        {
            int bufferSize = 2 + 1 + 1 + 1; // bytes for _reserved1, length of _queue, length of _consumerTag, bit fields
            bufferSize += WireFormatting.GetByteCount(_queue); // _queue in bytes
            bufferSize += WireFormatting.GetByteCount(_consumerTag); // _consumerTag in bytes
            bufferSize += WireFormatting.GetTableByteCount(_arguments); // _arguments in bytes
            return bufferSize;
        }
    }
}
