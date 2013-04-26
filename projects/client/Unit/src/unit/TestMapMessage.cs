// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2013 VMware, Inc.
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
//  Copyright (c) 2007-2013 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

using RabbitMQ.Util;
using RabbitMQ.Client.Content;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestMapMessage : WireFormattingFixture
    {
        [Test]
        public void TestRoundTrip()
        {
            NetworkBinaryWriter w = Writer();
            Dictionary<string, object> t = new Dictionary<string, object>();
            t["double"] = 1.234;
            t["string"] = "hello";
            MapWireFormatting.WriteMap(w, t);
            IDictionary t2 = MapWireFormatting.ReadMap(Reader(Contents(w)));
            Assert.AreEqual(2, t2.Count);
            Assert.AreEqual(1.234, t2["double"]);
            Assert.AreEqual("hello", t2["string"]);
        }

        [Test]
        public void TestEncoding()
        {
            NetworkBinaryWriter w = Writer();
            Dictionary<string, object> t = new Dictionary<string, object>();
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
}
