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
using System.Text;
using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing.Impl
{
    internal sealed class BasicGet : Client.Impl.MethodBase
    {
        public ushort _reserved1;
        public string _queue;
        public bool _noAck;

        public BasicGet()
        {
        }

        public BasicGet(ushort Reserved1, string Queue, bool NoAck)
        {
            _reserved1 = Reserved1;
            _queue = Queue;
            _noAck = NoAck;
        }

        public BasicGet(ReadOnlySpan<byte> span)
        {
            int offset = WireFormatting.ReadShort(span, out _reserved1);
            offset += WireFormatting.ReadShortstr(span.Slice(offset), out _queue);
            WireFormatting.ReadBits(span.Slice(offset), out _noAck);
        }

        public override ProtocolCommandId ProtocolCommandId => ProtocolCommandId.BasicGet;
        public override string ProtocolMethodName => "basic.get";
        public override bool HasContent => false;

        public override int WriteArgumentsTo(Span<byte> span)
        {
            int offset = WireFormatting.WriteShort(span, _reserved1);
            offset += WireFormatting.WriteShortstr(span.Slice(offset), _queue);
            return offset + WireFormatting.WriteBits(span.Slice(offset), _noAck);
        }

        public override int GetRequiredBufferSize()
        {
            int bufferSize = 2 + 1 + 1; // bytes for _reserved1, length of _queue, bit fields
            bufferSize += Encoding.UTF8.GetByteCount(_queue); // _queue in bytes
            return bufferSize;
        }
    }
}
