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
    struct MethodArgumentReader
    {
        private int? _bit;
        private int _bits;

        public MethodArgumentReader(ReadOnlyMemory<byte> memory)
        {
            _memory = memory;
            _memoryOffset = 0;
            _bits = 0;
            _bit = null;
        }

        private readonly ReadOnlyMemory<byte> _memory;
        private int _memoryOffset;

        public bool ReadBit()
        {
            if (!_bit.HasValue)
            {
                _bits = _memory.Span[_memoryOffset++];
                _bit = 0x01;
            }

            bool result = (_bits & _bit.Value) != 0;
            _bit <<= 1;
            return result;
        }

        public byte[] ReadContent()
        {
            throw new NotSupportedException("ReadContent should not be called");
        }

        public uint ReadLong()
        {
            ClearBits();
            uint result = NetworkOrderDeserializer.ReadUInt32(_memory.Slice(_memoryOffset));
            _memoryOffset += 4;
            return result;
        }

        public ulong ReadLonglong()
        {
            ClearBits();
            ulong result = NetworkOrderDeserializer.ReadUInt64(_memory.Slice(_memoryOffset));
            _memoryOffset += 8;
            return result;
        }

        public byte[] ReadLongstr()
        {
            ClearBits();
            byte[] result = WireFormatting.ReadLongstr(_memory.Slice(_memoryOffset));
            _memoryOffset += 4 + result.Length;
            return result;
        }

        public byte ReadOctet()
        {
            ClearBits();
            return _memory.Span[_memoryOffset++];
        }

        public ushort ReadShort()
        {
            ClearBits();
            ushort result = NetworkOrderDeserializer.ReadUInt16(_memory.Slice(_memoryOffset));
            _memoryOffset += 2;
            return result;
        }

        public string ReadShortstr()
        {
            ClearBits();
            string result = WireFormatting.ReadShortstr(_memory.Slice(_memoryOffset), out int bytesRead);
            _memoryOffset += bytesRead;
            return result;
        }

        public IDictionary<string, object> ReadTable()
        {
            ClearBits();
            IDictionary<string, object> result = WireFormatting.ReadTable(_memory.Slice(_memoryOffset), out int bytesRead);
            _memoryOffset += bytesRead;
            return result;
        }

        public AmqpTimestamp ReadTimestamp()
        {
            ClearBits();
            AmqpTimestamp result = WireFormatting.ReadTimestamp(_memory.Slice(_memoryOffset));
            _memoryOffset += 8;
            return result;
        }

        private void ClearBits()
        {
            _bits = 0;
            _bit = null;
        }

        // TODO: Consider using NotImplementedException (?)
        // This is a completely bizarre consequence of the way the
        // Message.Transfer method is marked up in the XML spec.
    }
}
