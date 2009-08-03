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

using System;
using System.IO;
using System.Collections;

using RabbitMQ.Util;
using RabbitMQ.Client.Content;

[TestFixture]
public class TestMapMessage: WireFormattingFixture {
    [Test]
    public void TestRoundTrip() {
        NetworkBinaryWriter w = Writer();
        Hashtable t = new Hashtable();
        t["double"] = 1.234;
        t["string"] = "hello";
        MapWireFormatting.WriteMap(w, t);
        IDictionary t2 = MapWireFormatting.ReadMap(Reader(Contents(w)));
        Assert.AreEqual(2, t2.Count);
        Assert.AreEqual(1.234, t2["double"]);
        Assert.AreEqual("hello", t2["string"]);
    }

    [Test]
    public void TestEncoding() {
        NetworkBinaryWriter w = Writer();
        Hashtable t = new Hashtable();
        t["double"] = 1.234;
        t["string"] = "hello";
        MapWireFormatting.WriteMap(w, t);
        Check(w, new byte[] {
            0x00, 0x00, 0x00, 0x02,

            0x64, 0x6F, 0x75, 0x62, 0x6C, 0x65, 0x00,
            0x09,
            0x3F, 0xF3, 0xBE, 0x76, 0xC8, 0xB4, 0x39, 0x58,

            0x73, 0x74, 0x72, 0x69, 0x6E, 0x67, 0x00,
            0x0A,
            0x68, 0x65, 0x6C, 0x6C, 0x6F, 0x00
        });
    }
}
