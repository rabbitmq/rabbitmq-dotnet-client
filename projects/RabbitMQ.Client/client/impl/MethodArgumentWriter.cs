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
using System.Collections;
using System.Collections.Generic;

namespace RabbitMQ.Client.Impl
{
    internal ref struct MethodArgumentWriter
    {
        private readonly Span<byte> _span;
        private int _offset;
        private int _bitAccumulator;
        private int _bitMask;

        public int Offset => _offset;

        private Span<byte> Span => _span.Slice(_offset);

        public MethodArgumentWriter(Span<byte> span)
        {
            _span = span;
            _offset = 0;
            _bitAccumulator = 0;
            _bitMask = 1;
        }

        public void WriteBit(bool val)
        {
            if (val)
            {
                _bitAccumulator |= _bitMask;
            }
            _bitMask <<= 1;
        }

        public void EndBits()
        {
            if (_bitMask > 1)
            {
                _span[_offset++] = (byte)_bitAccumulator;
                _bitAccumulator = 0;
                _bitMask = 1;
            }
        }

        public void WriteContent(byte[] val)
        {
            throw new NotSupportedException("WriteContent should not be called");
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

        public void WriteShort(ushort val)
        {
            _offset += WireFormatting.WriteShort(Span, val);
        }

        public void WriteShortstr(string val)
        {
            _offset += WireFormatting.WriteShortstr(Span, val);
        }

        public void WriteTable(IDictionary val)
        {
            _offset += WireFormatting.WriteTable(Span, val);
        }

        public void WriteTable(IDictionary<string, object> val)
        {
            _offset += WireFormatting.WriteTable(Span, val);
        }

        public void WriteTimestamp(AmqpTimestamp val)
        {
            _offset += WireFormatting.WriteTimestamp(Span, val);
        }
    }
}
