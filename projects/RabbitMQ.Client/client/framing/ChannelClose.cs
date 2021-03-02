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
using RabbitMQ.Util;

namespace RabbitMQ.Client.Framing.Impl
{
    internal sealed class ChannelClose : Client.Impl.MethodBase
    {
        public ushort _replyCode;
        public string _replyText;
        public ProtocolCommandId _protocolCommandId;

        public ChannelClose()
        {
        }

        public ChannelClose(ushort ReplyCode, string ReplyText, ushort classId, ushort methodId)
        {
            _replyCode = ReplyCode;
            _replyText = ReplyText;
            _protocolCommandId = (ProtocolCommandId)((uint)(classId << 16) | methodId);
        }

        public ChannelClose(ushort ReplyCode, string ReplyText, ProtocolCommandId protocolCommandId)
        {
            _replyCode = ReplyCode;
            _replyText = ReplyText;
            _protocolCommandId = protocolCommandId;
        }

        public ChannelClose(ReadOnlySpan<byte> span)
        {
            int offset = WireFormatting.ReadShort(span, 0, out _replyCode);
            offset += WireFormatting.ReadShortstr(span, offset, out _replyText);
            offset += WireFormatting.ReadLong(span, offset, out uint _protocolCommand);
            _protocolCommandId = (ProtocolCommandId)_protocolCommand;
        }

        public override ProtocolCommandId ProtocolCommandId => ProtocolCommandId.ChannelClose;
        public override string ProtocolMethodName => "channel.close";
        public override bool HasContent => false;

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public override int WriteArgumentsTo(Span<byte> span)
        {
            NetworkOrderSerializer.WriteUInt16(ref span[0], _replyCode);
            int offset = WireFormatting.WriteShortstr(span, 2, _replyText);
            NetworkOrderSerializer.WriteUInt32(ref span[2 + offset], (uint)_protocolCommandId);
            return 6 + offset;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public override int GetRequiredBufferSize()
        {
            return WireFormatting.GetByteCount(_replyText) + 2 + 1 + 2 + 2; // _replyText in bytes
        }
    }
}
