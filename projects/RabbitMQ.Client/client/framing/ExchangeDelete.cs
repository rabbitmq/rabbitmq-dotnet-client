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
    internal readonly struct ExchangeDelete : IOutgoingAmqpMethod
    {
        public readonly ExchangeName _exchange;
        public readonly bool _ifUnused;
        public readonly bool _nowait;

        public ExchangeDelete(ExchangeName exchange, bool ifUnused, bool noWait)
        {
            _exchange = exchange;
            _ifUnused = ifUnused;
            _nowait = noWait;
        }

        public ProtocolCommandId ProtocolCommandId => ProtocolCommandId.ExchangeDelete;

        public int WriteTo(Span<byte> span)
        {
            int offset = WireFormatting.WriteShort(ref span.GetStart(), default);
            offset += WireFormatting.WriteShortstr(ref span.GetOffset(offset), (ReadOnlySpan<byte>)_exchange);
            return offset + WireFormatting.WriteBits(ref span.GetOffset(offset), _ifUnused, _nowait);
        }

        public int GetRequiredBufferSize()
        {
            int bufferSize = 2 + 1 + 1; // bytes for _reserved1, length of _exchange, bit fields
            bufferSize += _exchange.ByteCount; // _exchange in bytes
            return bufferSize;
        }
    }
}
