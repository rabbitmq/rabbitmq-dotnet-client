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
    internal ref struct MethodArgumentReader
    {
        private readonly ReadOnlySpan<byte> _span;
        private int _offset;
        private int _bitMask;
        private int _bits;

        private ReadOnlySpan<byte> Span => _span.Slice(_offset);

        public MethodArgumentReader(ReadOnlySpan<byte> span)
        {
            _span = span;
            _offset = 0;
            _bitMask = 0;
            _bits = 0;
        }

        public bool ReadBit()
        {
            int bit = _bitMask;
            if (bit == 0)
            {
                _bits = _span[_offset++];
                bit = 1;
            }

            bool result = (_bits & bit) != 0;
            _bitMask = bit << 1;
            return result;
        }

        public byte[] ReadContent()
        {
            throw new NotSupportedException("ReadContent should not be called");
        }

        public uint ReadLong()
        {
            uint result = NetworkOrderDeserializer.ReadUInt32(Span);
            _offset += 4;
            return result;
        }

        public ulong ReadLonglong()
        {
            ulong result = NetworkOrderDeserializer.ReadUInt64(Span);
            _offset += 8;
            return result;
        }

        public byte[] ReadLongstr()
        {
            byte[] result = WireFormatting.ReadLongstr(Span);
            _offset += 4 + result.Length;
            return result;
        }

        public byte ReadOctet()
        {
            return _span[_offset++];
        }

        public ushort ReadShort()
        {
            ushort result = NetworkOrderDeserializer.ReadUInt16(Span);
            _offset += 2;
            return result;
        }

        public string ReadShortstr()
        {
            string result = WireFormatting.ReadShortstr(Span, out int bytesRead);
            _offset += bytesRead;
            return result;
        }

        public Dictionary<string, object> ReadTable()
        {
            Dictionary<string, object> result = WireFormatting.ReadTable(Span, out int bytesRead);
            _offset += bytesRead;
            return result;
        }

        public AmqpTimestamp ReadTimestamp()
        {
            AmqpTimestamp result = WireFormatting.ReadTimestamp(Span);
            _offset += 8;
            return result;
        }
    }
}
