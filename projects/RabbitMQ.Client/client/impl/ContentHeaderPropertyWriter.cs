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
    internal struct ContentHeaderPropertyWriter
    {
        private int _bitCount;
        private ushort _flagWord;
        public int Offset { get; private set; }
        public Memory<byte> Memory { get; private set; }

        public ContentHeaderPropertyWriter(Memory<byte> memory)
        {
            Memory = memory;
            _flagWord = 0;
            _bitCount = 0;
            Offset = 0;
        }

        public void FinishPresence() => EmitFlagWord(false);

        public void WriteBit(bool bit) => WritePresence(bit);

        public void WriteLong(uint val) => Offset += WireFormatting.WriteLong(Memory.Slice(Offset), val);

        public void WriteLonglong(ulong val) => Offset += WireFormatting.WriteLonglong(Memory.Slice(Offset), val);

        public void WriteLongstr(byte[] val) => Offset += WireFormatting.WriteLongstr(Memory.Slice(Offset), val);

        public void WriteOctet(byte val) => Memory.Slice(Offset++).Span[0] = val;

        public void WritePresence(bool present)
        {
            if (_bitCount == 15)
            {
                EmitFlagWord(true);
            }

            if (present)
            {
                int bit = 15 - _bitCount;
                _flagWord = (ushort)(_flagWord | (1 << bit));
            }
            _bitCount++;
        }

        public void WriteShort(ushort val) => Offset += WireFormatting.WriteShort(Memory.Slice(Offset), val);

        public void WriteShortstr(string val) => Offset += WireFormatting.WriteShortstr(Memory.Slice(Offset), val);

        public void WriteTable(IDictionary<string, object> val) => Offset += WireFormatting.WriteTable(Memory.Slice(Offset), val);

        public void WriteTimestamp(AmqpTimestamp val) => Offset += WireFormatting.WriteTimestamp(Memory.Slice(Offset), val);

        private void EmitFlagWord(bool continuationBit)
        {
            NetworkOrderSerializer.WriteUInt16(Memory.Slice(Offset).Span, (ushort)(continuationBit ? (_flagWord | 1) : _flagWord));
            Offset += 2;
            _flagWord = 0;
            _bitCount = 0;
        }
    }
}
