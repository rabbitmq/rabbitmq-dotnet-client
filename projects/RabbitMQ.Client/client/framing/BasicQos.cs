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
    internal sealed class BasicQos : Client.Impl.MethodBase
    {
        public uint _prefetchSize;
        public ushort _prefetchCount;
        public bool _global;

        public BasicQos()
        {
        }

        public BasicQos(uint PrefetchSize, ushort PrefetchCount, bool Global)
        {
            _prefetchSize = PrefetchSize;
            _prefetchCount = PrefetchCount;
            _global = Global;
        }

        public BasicQos(ReadOnlySpan<byte> span)
        {
            int offset = WireFormatting.ReadLong(span, 0, out _prefetchSize);
            offset += WireFormatting.ReadShort(span, offset, out _prefetchCount);
            WireFormatting.ReadBits(span, offset, out _global);
        }

        public override ProtocolCommandId ProtocolCommandId => ProtocolCommandId.BasicQos;
        public override string ProtocolMethodName => "basic.qos";
        public override bool HasContent => false;

        public override int WriteArgumentsTo(Span<byte> span)
        {
            int offset = WireFormatting.WriteLong(span, 0, _prefetchSize);
            offset += WireFormatting.WriteShort(span, offset, _prefetchCount);
            return offset + WireFormatting.WriteBits(ref span[offset], _global);
        }

        public override int GetRequiredBufferSize()
        {
            return 4 + 2 + 1; // bytes for _prefetchSize, _prefetchCount, bit fields
        }
    }
}
