// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2011 VMware, Inc.
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
//  The Initial Developer of the Original Code is VMware, Inc.
//  Copyright (c) 2007-2011 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.IO;
using System.Collections;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    public class ContentHeaderPropertyWriter
    {
        private NetworkBinaryWriter m_writer;

        public NetworkBinaryWriter BaseWriter { get { return m_writer; } }

        protected ushort m_flagWord;
        protected int m_bitCount;

        public ContentHeaderPropertyWriter(NetworkBinaryWriter writer)
        {
            m_writer = writer;
            m_flagWord = 0;
            m_bitCount = 0;
        }

        private void EmitFlagWord(bool continuationBit)
        {
            m_writer.Write((ushort)(continuationBit ? (m_flagWord | 1) : m_flagWord));
            m_flagWord = 0;
            m_bitCount = 0;
        }

        public void WritePresence(bool present)
        {
            if (m_bitCount == 15)
            {
                EmitFlagWord(true);
            }

            if (present)
            {
                int bit = 15 - m_bitCount;
                m_flagWord = (ushort)(m_flagWord | (1 << bit));
            }
            m_bitCount++;
        }

        public void FinishPresence()
        {
            EmitFlagWord(false);
        }

        public void WriteBit(bool bit)
        {
            WritePresence(bit);
        }

        public void WriteOctet(byte val)
        {
            WireFormatting.WriteOctet(m_writer, val);
        }

        public void WriteShortstr(string val)
        {
            WireFormatting.WriteShortstr(m_writer, val);
        }

        public void WriteLongstr(byte[] val)
        {
            WireFormatting.WriteLongstr(m_writer, val);
        }

        public void WriteShort(ushort val)
        {
            WireFormatting.WriteShort(m_writer, val);
        }

        public void WriteLong(uint val)
        {
            WireFormatting.WriteLong(m_writer, val);
        }

        public void WriteLonglong(ulong val)
        {
            WireFormatting.WriteLonglong(m_writer, val);
        }

        public void WriteTable(IDictionary val)
        {
            WireFormatting.WriteTable(m_writer, val);
        }

        public void WriteTimestamp(AmqpTimestamp val)
        {
            WireFormatting.WriteTimestamp(m_writer, val);
        }
    }
}
