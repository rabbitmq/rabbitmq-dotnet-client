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

using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    internal ref struct ContentHeaderPropertyWriter
    {
        private const ushort StartBitMask = 0b1000_0000_0000_0000;
        private const ushort EndBitMask = 0b0000_0000_0000_0001;

        private readonly Span<byte> _span;
        private ushort _bitAccumulator;
        private ushort _bitMask;

        public int Offset { get; private set; }

        private Span<byte> Span => _span.Slice(Offset);

        public ContentHeaderPropertyWriter(Span<byte> span)
        {
            _span = span;
            Offset = 0;
            _bitAccumulator = 0;
            _bitMask = StartBitMask;
        }

        public void FinishPresence()
        {
            WriteBits();
        }

        public void WriteBit(bool bit)
        {
            WritePresence(bit);
        }

        public void WriteLong(uint val)
        {
            Offset += WireFormatting.WriteLong(Span, val);
        }

        public void WriteLonglong(ulong val)
        {
            Offset += WireFormatting.WriteLonglong(Span, val);
        }

        public void WriteLongstr(byte[] val)
        {
            Offset += WireFormatting.WriteLongstr(Span, val);
        }

        public void WriteOctet(byte val)
        {
            _span[Offset++] = val;
        }

        public void WritePresence(bool present)
        {
            if (_bitMask == EndBitMask)
            {
                // Mark continuation
                _bitAccumulator |= _bitMask;
                WriteBits();
            }

            if (present)
            {
                _bitAccumulator |= _bitMask;
            }

            _bitMask >>= 1;
        }

        public void WriteShort(ushort val)
        {
            Offset += WireFormatting.WriteShort(Span, val);
        }

        public void WriteShortstr(string val)
        {
            Offset += WireFormatting.WriteShortstr(Span, val);
        }

        public void WriteTable(IDictionary<string, object> val)
        {
            Offset += WireFormatting.WriteTable(Span, val);
        }

        public void WriteTimestamp(AmqpTimestamp val)
        {
            Offset += WireFormatting.WriteTimestamp(Span, val);
        }

        private void WriteBits()
        {
            NetworkOrderSerializer.WriteUInt16(Span, _bitAccumulator);
            Offset += 2;
            _bitMask = StartBitMask;
            _bitAccumulator = 0;
        }
    }
}
