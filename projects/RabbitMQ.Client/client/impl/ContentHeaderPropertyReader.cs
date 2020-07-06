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
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    internal ref struct ContentHeaderPropertyReader
    {
        private const int StartBitMask = 0b1000_0000_0000_0000;
        private const int EndBitMask = 0b0000_0000_0000_0001;

        private readonly ReadOnlySpan<byte> _span;
        private int _offset;
        private int _bitMask;
        private int _bits;

        private ReadOnlySpan<byte> Span => _span.Slice(_offset);

        public ContentHeaderPropertyReader(ReadOnlySpan<byte> span)
        {
            _span = span;
            _offset = 0;
            _bitMask = EndBitMask; // force a flag read
            _bits = 1; // just the continuation bit
        }

        private bool ContinuationBitSet => (_bits & EndBitMask) != 0;

        public void FinishPresence()
        {
            if (ContinuationBitSet)
            {
                throw new MalformedFrameException("Unexpected continuation flag word");
            }
        }

        public bool ReadBit()
        {
            return ReadPresence();
        }

        private void ReadBits()
        {
            if (!ContinuationBitSet)
            {
                throw new MalformedFrameException("Attempted to read flag word when none advertised");
            }
            _bits = NetworkOrderDeserializer.ReadUInt16(Span);
            _offset += 2;
            _bitMask = StartBitMask;
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

        public bool ReadPresence()
        {
            if (_bitMask == EndBitMask)
            {
                ReadBits();
            }

            bool result = (_bits & _bitMask) != 0;
            _bitMask >>= 1;
            return result;
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

        /// <returns>A type of <seealso cref="System.Collections.Generic.IDictionary{TKey,TValue}"/>.</returns>
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
