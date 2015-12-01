// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2015 Pivotal Software, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
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
//  at http://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    public class MethodArgumentReader
    {
        private int m_bit;
        private int m_bits;

        public MethodArgumentReader(NetworkBinaryReader reader)
        {
            BaseReader = reader;
            ClearBits();
        }

        public NetworkBinaryReader BaseReader { get; private set; }

        public bool ReadBit()
        {
            if (m_bit > 0x80)
            {
                m_bits = BaseReader.ReadByte();
                m_bit = 0x01;
            }

            bool result = (m_bits & m_bit) != 0;
            m_bit = m_bit << 1;
            return result;
        }

        public byte[] ReadContent()
        {
            throw new NotSupportedException("ReadContent should not be called");
        }

        public uint ReadLong()
        {
            ClearBits();
            return WireFormatting.ReadLong(BaseReader);
        }

        public ulong ReadLonglong()
        {
            ClearBits();
            return WireFormatting.ReadLonglong(BaseReader);
        }

        public byte[] ReadLongstr()
        {
            ClearBits();
            return WireFormatting.ReadLongstr(BaseReader);
        }

        public byte ReadOctet()
        {
            ClearBits();
            return WireFormatting.ReadOctet(BaseReader);
        }

        public ushort ReadShort()
        {
            ClearBits();
            return WireFormatting.ReadShort(BaseReader);
        }

        public string ReadShortstr()
        {
            ClearBits();
            return WireFormatting.ReadShortstr(BaseReader);
        }

        public IDictionary<string, object> ReadTable()
        {
            ClearBits();
            return WireFormatting.ReadTable(BaseReader);
        }

        public AmqpTimestamp ReadTimestamp()
        {
            ClearBits();
            return WireFormatting.ReadTimestamp(BaseReader);
        }

        private void ClearBits()
        {
            m_bits = 0;
            m_bit = 0x100;
        }

        // TODO: Consider using NotImplementedException (?)
        // This is a completely bizarre consequence of the way the
        // Message.Transfer method is marked up in the XML spec.
    }
}
