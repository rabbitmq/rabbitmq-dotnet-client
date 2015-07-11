// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2014 GoPivotal, Inc.
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
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;

using System.IO;

using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestNetworkBinaryCodec
    {
        public static NetworkBinaryWriter Writer()
        {
            return new NetworkBinaryWriter(new MemoryStream());
        }

        public byte[] Contents(NetworkBinaryWriter w)
        {
            return ((MemoryStream)w.BaseStream).ToArray();
        }

        public NetworkBinaryReader Reader(byte[] bytes)
        {
            return new NetworkBinaryReader(new MemoryStream(bytes));
        }

        public void Check(NetworkBinaryWriter w, byte[] bytes)
        {
            Assert.AreEqual(bytes, Contents(w));
        }

        public NetworkBinaryWriter m_w;

        [SetUp]
        public void SetUp()
        {
            m_w = Writer();
        }

        [Test]
        public void TestWriteInt16_positive()
        {
            m_w.Write((short)0x1234);
            Check(m_w, new byte[] { 0x12, 0x34 });
        }

        [Test]
        public void TestWriteInt16_negative()
        {
            m_w.Write((short)-0x1234);
            Check(m_w, new byte[] { 0xED, 0xCC });
        }

        [Test]
        public void TestWriteUInt16()
        {
            m_w.Write((ushort)0x89AB);
            Check(m_w, new byte[] { 0x89, 0xAB });
        }

        [Test]
        public void TestReadInt16()
        {
            Assert.AreEqual(0x1234, Reader(new byte[] { 0x12, 0x34 }).ReadInt16());
        }

        [Test]
        public void TestReadUInt16()
        {
            Assert.AreEqual(0x89AB, Reader(new byte[] { 0x89, 0xAB }).ReadUInt16());
        }


        [Test]
        public void TestWriteInt32_positive()
        {
            m_w.Write((int)0x12345678);
            Check(m_w, new byte[] { 0x12, 0x34, 0x56, 0x78 });
        }

        [Test]
        public void TestWriteInt32_negative()
        {
            m_w.Write((int)-0x12345678);
            Check(m_w, new byte[] { 0xED, 0xCB, 0xA9, 0x88 });
        }

        [Test]
        public void TestWriteUInt32()
        {
            m_w.Write((uint)0x89ABCDEF);
            Check(m_w, new byte[] { 0x89, 0xAB, 0xCD, 0xEF });
        }

        [Test]
        public void TestReadInt32()
        {
            Assert.AreEqual(0x12345678, Reader(new byte[] { 0x12, 0x34, 0x56, 0x78 }).ReadInt32());
        }

        [Test]
        public void TestReadUInt32()
        {
            Assert.AreEqual(0x89ABCDEF, Reader(new byte[] { 0x89, 0xAB, 0xCD, 0xEF }).ReadUInt32());
        }


        [Test]
        public void TestWriteInt64_positive()
        {
            m_w.Write((long)0x123456789ABCDEF0);
            Check(m_w, new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 });
        }

        [Test]
        public void TestWriteInt64_negative()
        {
            m_w.Write((long)-0x123456789ABCDEF0);
            Check(m_w, new byte[] { 0xED, 0xCB, 0xA9, 0x87, 0x65, 0x43, 0x21, 0x10 });
        }

        [Test]
        public void TestWriteUInt64()
        {
            m_w.Write((ulong)0x89ABCDEF01234567);
            Check(m_w, new byte[] { 0x89, 0xAB, 0xCD, 0xEF, 0x01, 0x23, 0x45, 0x67 });
        }

        [Test]
        public void TestReadInt64()
        {
            Assert.AreEqual(0x123456789ABCDEF0,
                            Reader(new byte[] { 0x12, 0x34, 0x56, 0x78,
                                                0x9A, 0xBC, 0xDE, 0xF0 }).ReadInt64());
        }

        [Test]
        public void TestReadUInt64()
        {
            Assert.AreEqual(0x89ABCDEF01234567,
                            Reader(new byte[] { 0x89, 0xAB, 0xCD, 0xEF,
                                                0x01, 0x23, 0x45, 0x67 }).ReadUInt64());
        }
    }
}
