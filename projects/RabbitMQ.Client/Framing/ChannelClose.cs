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
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing
{
    internal readonly struct ChannelClose : IOutgoingAmqpMethod
    {
        public readonly ushort _replyCode;
        public readonly string _replyText;
        public readonly ushort _classId;
        public readonly ushort _methodId;

        public ChannelClose(ushort ReplyCode, string ReplyText, ushort ClassId, ushort MethodId)
        {
            _replyCode = ReplyCode;
            _replyText = ReplyText;
            _classId = ClassId;
            _methodId = MethodId;
        }

        public ChannelClose(ReadOnlySpan<byte> span)
        {
            int offset = WireFormatting.ReadShort(span, out _replyCode);
            offset += WireFormatting.ReadShortstr(span.Slice(offset), out _replyText);
            offset += WireFormatting.ReadShort(span.Slice(offset), out _classId);
            WireFormatting.ReadShort(span.Slice(offset), out _methodId);
        }

        public ProtocolCommandId ProtocolCommandId => ProtocolCommandId.ChannelClose;

        public int WriteTo(Span<byte> span)
        {
            int offset = WireFormatting.WriteShort(ref span.GetStart(), _replyCode);
            offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), _replyText);
            offset += WireFormatting.WriteShort(ref span.GetOffset(offset), _classId);
            return offset + WireFormatting.WriteShort(ref span.GetOffset(offset), _methodId);
        }

        public int GetRequiredBufferSize()
        {
            int bufferSize = 2 + 1 + 2 + 2; // bytes for _replyCode, length of _replyText, _classId, _methodId
            bufferSize += WireFormatting.GetByteCount(_replyText); // _replyText in bytes
            return bufferSize;
        }
    }
}
