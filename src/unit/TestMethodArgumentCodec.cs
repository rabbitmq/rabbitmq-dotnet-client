// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007, 2008 LShift Ltd., Cohesive Financial
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
//   The Initial Developers of the Original Code are LShift Ltd.,
//   Cohesive Financial Technologies LLC., and Rabbit Technologies Ltd.
//
//   Portions created by LShift Ltd., Cohesive Financial Technologies
//   LLC., and Rabbit Technologies Ltd. are Copyright (C) 2007, 2008
//   LShift Ltd., Cohesive Financial Technologies LLC., and Rabbit
//   Technologies Ltd.;
//
//   All Rights Reserved.
//
//   Contributor(s): ______________________________________.
//
//---------------------------------------------------------------------------
using NUnit.Framework;

using System;
using System.IO;
using System.Text;
using System.Collections;

using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using RabbitMQ.Util;

[TestFixture]
public class TestMethodArgumentCodec {
    public static MethodArgumentWriter Writer() {
        return new MethodArgumentWriter(new NetworkBinaryWriter(new MemoryStream()));
    }

    public static MethodArgumentReader Reader(byte[] bytes) {
        return new MethodArgumentReader(new NetworkBinaryReader(new MemoryStream(bytes)));
    }

    public byte[] Contents(MethodArgumentWriter w) {
        return ((MemoryStream) w.BaseWriter.BaseStream).ToArray();
    }

    public void Check(MethodArgumentWriter w, byte[] expected) {
        byte[] actual = Contents(w);
        try {
            Assert.AreEqual(expected, actual);
        } catch {
            Console.WriteLine();
            Console.WriteLine("EXPECTED ==================================================");
            DebugUtil.Dump(expected);
            Console.WriteLine("ACTUAL ====================================================");
            DebugUtil.Dump(actual);
            Console.WriteLine("===========================================================");
            throw;
        }
    }

    public MethodArgumentWriter w;

    [SetUp]
    public void SetUp() {
        w = Writer();
    }

    [Test]
    public void TestTableLengthWrite() {
        Hashtable t = new Hashtable();
        t["abc"] = "def";
        w.WriteTable(t);
        Check(w, new byte [] { 0x00, 0x00, 0x00, 0x0C,
                               0x03, 0x61, 0x62, 0x63,
                               0x53, 0x00, 0x00, 0x00,
                               0x03, 0x64, 0x65, 0x66 });
    }

    [Test]
    public void TestTableLengthRead() {
        IDictionary t = Reader(new byte [] { 0x00, 0x00, 0x00, 0x0C,
                                             0x03, 0x61, 0x62, 0x63,
                                             0x53, 0x00, 0x00, 0x00,
                                             0x03, 0x64, 0x65, 0x66 }).ReadTable();
        Assert.AreEqual(Encoding.UTF8.GetBytes("def"), t["abc"]);
        Assert.AreEqual(1, t.Count);
    }

    [Test]
    public void TestNestedTableWrite() {
        Hashtable t = new Hashtable();
        Hashtable x = new Hashtable();
        x["y"] = 0x12345678;
        t["x"] = x;
        w.WriteTable(t);
        Check(w, new byte [] { 0x00, 0x00, 0x00, 0x0E,
                               0x01, 0x78, 0x46, 0x00,
                               0x00, 0x00, 0x07, 0x01,
                               0x79, 0x49, 0x12, 0x34,
                               0x56, 0x78 });
    }

    private static byte[] THE_ACCESS_REQUEST = new byte[] { 0x05, 0x2F, 0x64, 0x61,
                                                            0x74, 0x61, 0x1E };

    [Test]
    public void TestAccessRequestWrite() {
        RabbitMQ.Client.Framing.Impl.v0_8.AccessRequest req = new RabbitMQ.Client.Framing.Impl.v0_8.AccessRequest();
        req.m_realm = "/data";
        req.m_exclusive = false;
        req.m_passive = true;
        req.m_active = true;
        req.m_write = true;
        req.m_read = true;
        req.WriteArgumentsTo(w);
        w.Flush();
        Check(w, THE_ACCESS_REQUEST);
    }

    [Test]
    public void TestAccessRequestRead() {
        RabbitMQ.Client.Framing.Impl.v0_8.AccessRequest result = new RabbitMQ.Client.Framing.Impl.v0_8.AccessRequest();
        result.ReadArgumentsFrom(Reader(THE_ACCESS_REQUEST));
        RabbitMQ.Client.Framing.v0_8.IAccessRequest req =
            (RabbitMQ.Client.Framing.v0_8.IAccessRequest) result;
        Assert.AreEqual("/data", req.Realm);
        Assert.IsFalse(req.Exclusive);
        Assert.IsTrue(req.Passive);
        Assert.IsTrue(req.Active);
        Assert.IsTrue(req.Write);
        Assert.IsTrue(req.Read);
    }
}
