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
    internal sealed class ConnectionTuneOk : Client.Impl.MethodBase
    {
        public ushort _channelMax;
        public uint _frameMax;
        public ushort _heartbeat;

        public ConnectionTuneOk()
        {
        }

        public ConnectionTuneOk(ushort ChannelMax, uint FrameMax, ushort Heartbeat)
        {
            _channelMax = ChannelMax;
            _frameMax = FrameMax;
            _heartbeat = Heartbeat;
        }

        public ConnectionTuneOk(ReadOnlySpan<byte> span)
        {
            int offset = WireFormatting.ReadShort(span, 0, out _channelMax);
            offset += WireFormatting.ReadLong(span, offset, out _frameMax);
            WireFormatting.ReadShort(span, offset, out _heartbeat);
        }

        public override ProtocolCommandId ProtocolCommandId => ProtocolCommandId.ConnectionTuneOk;
        public override string ProtocolMethodName => "connection.tune-ok";
        public override bool HasContent => false;

        public override int WriteArgumentsTo(Span<byte> span)
        {
            int offset = WireFormatting.WriteShort(span, 0, _channelMax);
            offset += WireFormatting.WriteLong(span, offset, _frameMax);
            return offset + WireFormatting.WriteShort(span, offset, _heartbeat);
        }

        public override int GetRequiredBufferSize()
        {
            return 2 + 4 + 2; // bytes for _channelMax, _frameMax, _heartbeat
        }
    }
}
