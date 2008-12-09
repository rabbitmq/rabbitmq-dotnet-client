// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2009 LShift Ltd., Cohesive Financial
//   Technologies LLC., and Rabbit Technologies Ltd.
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
//   The contents of this file are subject to the Mozilla Public License
//   Version 1.1 (the "License"); you may not use this file except in
//   compliance with the License. You may obtain a copy of the License at
//   http://www.rabbitmq.com/mpl.html
//
//   Software distributed under the License is distributed on an "AS IS"
//   basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
//   License for the specific language governing rights and limitations
//   under the License.
//
//   The Original Code is The RabbitMQ .NET Client.
//
//   The Initial Developers of the Original Code are LShift Ltd,
//   Cohesive Financial Technologies LLC, and Rabbit Technologies Ltd.
//
//   Portions created before 22-Nov-2008 00:00:00 GMT by LShift Ltd,
//   Cohesive Financial Technologies LLC, or Rabbit Technologies Ltd
//   are Copyright (C) 2007-2008 LShift Ltd, Cohesive Financial
//   Technologies LLC, and Rabbit Technologies Ltd.
//
//   Portions created by LShift Ltd are Copyright (C) 2007-2009 LShift
//   Ltd. Portions created by Cohesive Financial Technologies LLC are
//   Copyright (C) 2007-2009 Cohesive Financial Technologies
//   LLC. Portions created by Rabbit Technologies Ltd are Copyright
//   (C) 2007-2009 Rabbit Technologies Ltd.
//
//   All Rights Reserved.
//
//   Contributor(s): ______________________________________.
//
//---------------------------------------------------------------------------
using NUnit.Framework;

using System.IO;

using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using RabbitMQ.Util;

[TestFixture]
public class TestNetworkBinaryCodec {
    public static NetworkBinaryWriter Writer() {
        return new NetworkBinaryWriter(new MemoryStream());
    }

    public byte[] Contents(NetworkBinaryWriter w) {
        return ((MemoryStream) w.BaseStream).ToArray();
    }

    public NetworkBinaryReader Reader(byte[] bytes) {
        return new NetworkBinaryReader(new MemoryStream(bytes));
    }

    public void Check(NetworkBinaryWriter w, byte[] bytes) {
        Assert.AreEqual(bytes, Contents(w));
    }

    public NetworkBinaryWriter w;

    [SetUp]
    public void SetUp() {
        w = Writer();
    }

    [Test]
    public void TestWriteInt16_positive() {
        w.Write((short) 0x1234);
        Check(w, new byte[] { 0x12, 0x34 });
    }

    [Test]
    public void TestWriteInt16_negative() {
        w.Write((short) -0x1234);
        Check(w, new byte[] { 0xED, 0xCC });
    }

    [Test]
    public void TestWriteUInt16() {
        w.Write((ushort) 0x89AB);
        Check(w, new byte[] { 0x89, 0xAB });
    }

    [Test]
    public void TestReadInt16() {
        Assert.AreEqual(0x1234, Reader(new byte[] { 0x12, 0x34 }).ReadInt16());
    }

    [Test]
    public void TestReadUInt16() {
        Assert.AreEqual(0x89AB, Reader(new byte[] { 0x89, 0xAB }).ReadUInt16());
    }


    [Test]
    public void TestWriteInt32_positive() {
        w.Write((int) 0x12345678);
        Check(w, new byte[] { 0x12, 0x34, 0x56, 0x78 });
    }

    [Test]
    public void TestWriteInt32_negative() {
        w.Write((int) -0x12345678);
        Check(w, new byte[] { 0xED, 0xCB, 0xA9, 0x88 });
    }

    [Test]
    public void TestWriteUInt32() {
        NetworkBinaryWriter w = Writer();
        w.Write((uint) 0x89ABCDEF);
        Check(w, new byte[] { 0x89, 0xAB, 0xCD, 0xEF });
    }

    [Test]
    public void TestReadInt32() {
        Assert.AreEqual(0x12345678, Reader(new byte[] { 0x12, 0x34, 0x56, 0x78 }).ReadInt32());
    }

    [Test]
    public void TestReadUInt32() {
        Assert.AreEqual(0x89ABCDEF, Reader(new byte[] { 0x89, 0xAB, 0xCD, 0xEF }).ReadUInt32());
    }


    [Test]
    public void TestWriteInt64_positive() {
        w.Write((long) 0x123456789ABCDEF0);
        Check(w, new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 });
    }

    [Test]
    public void TestWriteInt64_negative() {
        w.Write((long) -0x123456789ABCDEF0);
        Check(w, new byte[] { 0xED, 0xCB, 0xA9, 0x87, 0x65, 0x43, 0x21, 0x10 });
    }

    [Test]
    public void TestWriteUInt64() {
        NetworkBinaryWriter w = Writer();
        w.Write((ulong) 0x89ABCDEF01234567);
        Check(w, new byte[] { 0x89, 0xAB, 0xCD, 0xEF, 0x01, 0x23, 0x45, 0x67 });
    }

    [Test]
    public void TestReadInt64() {
        Assert.AreEqual(0x123456789ABCDEF0,
                        Reader(new byte[] { 0x12, 0x34, 0x56, 0x78,
                                            0x9A, 0xBC, 0xDE, 0xF0 }).ReadInt64());
    }

    [Test]
    public void TestReadUInt64() {
        Assert.AreEqual(0x89ABCDEF01234567,
                        Reader(new byte[] { 0x89, 0xAB, 0xCD, 0xEF,
                                            0x01, 0x23, 0x45, 0x67 }).ReadUInt64());
    }
}
