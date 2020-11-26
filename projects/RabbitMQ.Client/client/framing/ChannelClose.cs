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
    internal sealed class ChannelClose : Client.Impl.MethodBase
    {
        public ushort _replyCode;
        public string _replyText;
        public ushort _classId;
        public ushort _methodId;

        public ChannelClose()
        {
        }

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

        public override ProtocolCommandId ProtocolCommandId => ProtocolCommandId.ChannelClose;
        public override string ProtocolMethodName => "channel.close";
        public override bool HasContent => false;

        public override int WriteArgumentsTo(Span<byte> span)
        {
            int offset = WireFormatting.WriteShort(span, _replyCode);
            offset += WireFormatting.WriteShortstr(span.Slice(offset), _replyText);
            offset += WireFormatting.WriteShort(span.Slice(offset), _classId);
            return offset + WireFormatting.WriteShort(span.Slice(offset), _methodId);
        }

        public override int GetRequiredBufferSize()
        {
            int bufferSize = 2 + 1 + 2 + 2; // bytes for _replyCode, length of _replyText, _classId, _methodId
            bufferSize += WireFormatting.GetUTF8ByteCount(_replyText); // _replyText in bytes
            return bufferSize;
        }
    }
}
