// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
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
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at https://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
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
        private int _offset;
        private ushort _bitAccumulator;
        private ushort _bitMask;

        public int Offset => _offset;

        private Span<byte> Span => _span.Slice(_offset);

        public ContentHeaderPropertyWriter(Span<byte> span)
        {
            _span = span;
            _offset = 0;
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
            _offset += WireFormatting.WriteLong(Span, val);
        }

        public void WriteLonglong(ulong val)
        {
            _offset += WireFormatting.WriteLonglong(Span, val);
        }

        public void WriteLongstr(byte[] val)
        {
            _offset += WireFormatting.WriteLongstr(Span, val);
        }

        public void WriteOctet(byte val)
        {
            _span[_offset++] = val;
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
            _offset += WireFormatting.WriteShort(Span, val);
        }

        public void WriteShortstr(string val)
        {
            _offset += WireFormatting.WriteShortstr(Span, val);
        }

        public void WriteTable(IDictionary<string, object> val)
        {
            _offset += WireFormatting.WriteTable(Span, val);
        }

        public void WriteTimestamp(AmqpTimestamp val)
        {
            _offset += WireFormatting.WriteTimestamp(Span, val);
        }

        private void WriteBits()
        {
            NetworkOrderSerializer.WriteUInt16(Span, _bitAccumulator);
            _offset += 2;
            _bitMask = StartBitMask;
            _bitAccumulator = 0;
        }
    }
}
