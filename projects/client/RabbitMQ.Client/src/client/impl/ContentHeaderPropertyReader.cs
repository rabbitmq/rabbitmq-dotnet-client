// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2012 VMware, Inc.
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
//  Copyright (c) 2007-2012 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    public class ContentHeaderPropertyReader
    {
        private NetworkBinaryReader m_reader;

        public NetworkBinaryReader BaseReader { get { return m_reader; } }

        protected ushort m_flagWord;
        protected ushort m_bitCount;

        public ContentHeaderPropertyReader(NetworkBinaryReader reader)
        {
            m_reader = reader;
            m_flagWord = 1; // just the continuation bit
            m_bitCount = 15; // the correct position to force a m_flagWord read
        }

        public bool ContinuationBitSet { get { return ((m_flagWord & 1) != 0); } }

        public void ReadFlagWord()
        {
            if (!ContinuationBitSet)
            {
                throw new MalformedFrameException("Attempted to read flag word when none advertised");
            }
            m_flagWord = m_reader.ReadUInt16();
            m_bitCount = 0;
        }

        public bool ReadPresence()
        {
            if (m_bitCount == 15)
            {
                ReadFlagWord();
            }

            int bit = 15 - m_bitCount;
            bool result = (m_flagWord & (1 << bit)) != 0;
            m_bitCount++;
            return result;
        }
        
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

        public byte ReadOctet()
        {
            return WireFormatting.ReadOctet(m_reader);
        }

        public string ReadShortstr()
        {
            return WireFormatting.ReadShortstr(m_reader);
        }
        
        public byte[] ReadLongstr()
        {
            return WireFormatting.ReadLongstr(m_reader);
        }

        public ushort ReadShort()
        {
            return WireFormatting.ReadShort(m_reader);
        }

        public uint ReadLong()
        {
            return WireFormatting.ReadLong(m_reader);
        }

        public ulong ReadLonglong()
        {
            return WireFormatting.ReadLonglong(m_reader);
        }

        public IDictionary ReadTable()
        {
            return WireFormatting.ReadTable(m_reader);
        }

        public AmqpTimestamp ReadTimestamp()
        {
            return WireFormatting.ReadTimestamp(m_reader);
        }
    }
}
