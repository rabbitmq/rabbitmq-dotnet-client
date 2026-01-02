// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing
{
    internal readonly struct BasicConsume : IOutgoingAmqpMethod
    {
        // deprecated
        // ushort _reserved1
        public readonly string _queue;
        public readonly string _consumerTag;
        public readonly bool _noLocal;
        public readonly bool _noAck;
        public readonly bool _exclusive;
        public readonly bool _nowait;
        public readonly IDictionary<string, object?>? _arguments;

        public BasicConsume(string Queue, string ConsumerTag, bool NoLocal, bool NoAck, bool Exclusive, bool Nowait, IDictionary<string, object?>? Arguments)
        {
            _queue = Queue;
            _consumerTag = ConsumerTag;
            _noLocal = NoLocal;
            _noAck = NoAck;
            _exclusive = Exclusive;
            _nowait = Nowait;
            _arguments = Arguments;
        }

        public ProtocolCommandId ProtocolCommandId => ProtocolCommandId.BasicConsume;

        public int WriteTo(Span<byte> span)
        {
            int offset = WireFormatting.WriteShort(ref span.GetStart(), default);
            offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), _queue);
            offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), _consumerTag);
            offset += WireFormatting.WriteBits(ref span.GetOffset(offset), _noLocal, _noAck, _exclusive, _nowait);
            return offset + WireFormatting.WriteTable(ref span.GetOffset(offset), _arguments);
        }

        public int GetRequiredBufferSize()
        {
            int bufferSize = 2 + 1 + 1 + 1; // bytes for _reserved1, length of _queue, length of _consumerTag, bit fields
            bufferSize += WireFormatting.GetByteCount(_queue); // _queue in bytes
            bufferSize += WireFormatting.GetByteCount(_consumerTag); // _consumerTag in bytes
            bufferSize += WireFormatting.GetTableByteCount(_arguments); // _arguments in bytes
            return bufferSize;
        }
    }
}
