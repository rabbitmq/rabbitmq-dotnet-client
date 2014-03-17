// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2013 GoPivotal, Inc.
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
//  Copyright (c) 2007-2013 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------


using NUnit.Framework;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

using RabbitMQ.Util;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestFieldTableFormattingGeneric : WireFormattingFixture
    {
        [Test]
        public void TestStandardTypes()
        {
            NetworkBinaryWriter w = Writer();
            IDictionary<string, object> t = new Dictionary<string, object>();
            t["string"] = "Hello";
            t["int"] = 1234;
            t["decimal"] = 12.34m;
            t["timestamp"] = new AmqpTimestamp(0);
            IDictionary<string, object> t2 = new Dictionary<string, object>();
            t["fieldtable"] = t2;
            t2["test"] = "test";
            IList array = new List<object>();
            array.Add("longstring");
            array.Add(1234);
            t["fieldarray"] = array;
            WireFormatting.WriteTable(w, t);
            IDictionary<string, object> nt = WireFormatting.ReadTable(Reader(Contents(w)));
            Assert.AreEqual(Encoding.UTF8.GetBytes("Hello"), nt["string"]);
            Assert.AreEqual(1234, nt["int"]);
            Assert.AreEqual(12.34m, nt["decimal"]);
            Assert.AreEqual(0, ((AmqpTimestamp)nt["timestamp"]).UnixTime);
            IDictionary<string, object> nt2 = (IDictionary<string, object>)nt["fieldtable"];
            Assert.AreEqual(Encoding.UTF8.GetBytes("test"), nt2["test"]);
            IList<object> narray = (IList<object>)nt["fieldarray"];
            Assert.AreEqual(Encoding.UTF8.GetBytes("longstring"), narray[0]);
            Assert.AreEqual(1234, narray[1]);
        }

        [Test]
        public void TestTableEncoding_S()
        {
            NetworkBinaryWriter w = Writer();
            IDictionary<string, object> t = new Dictionary<string, object>();
            t["a"] = "bc";
            WireFormatting.WriteTable(w, t);
            Check(w, new byte[] {
                    0,0,0,9, // table length
                    1,(byte)'a', // key
                    (byte)'S', // type
                    0,0,0,2,(byte)'b',(byte)'c' // value
                });
        }

        [Test]
        public void TestTableEncoding_x()
        {
            NetworkBinaryWriter w = Writer();
            IDictionary<string, object> t = new Dictionary<string, object>();
            t["a"] = new BinaryTableValue(new byte[] { 0xaa, 0x55 });
            WireFormatting.WriteTable(w, t);
            Check(w, new byte[] {
                    0,0,0,9, // table length
                    1,(byte)'a', // key
                    (byte)'x', // type
                    0,0,0,2,0xaa,0x55 // value
                });
        }

        [Test]
        public void TestQpidJmsTypes()
        {
            NetworkBinaryWriter w = Writer();
            IDictionary<string, object> t = new Dictionary<string, object>();
            t["b"] = (sbyte)-128;
            t["d"] = (double)123;
            t["f"] = (float)123;
            t["l"] = (long)123;
            t["s"] = (short)123;
            t["t"] = true;
            byte[] xbytes = new byte[] { 0xaa, 0x55 };
            t["x"] = new BinaryTableValue(xbytes);
            t["V"] = null;
            WireFormatting.WriteTable(w, t);
            IDictionary nt = (IDictionary)WireFormatting.ReadTable(Reader(Contents(w)));
            Assert.AreEqual(typeof(sbyte), nt["b"].GetType()); Assert.AreEqual((sbyte)-128, nt["b"]);
            Assert.AreEqual(typeof(double), nt["d"].GetType()); Assert.AreEqual((double)123, nt["d"]);
            Assert.AreEqual(typeof(float), nt["f"].GetType()); Assert.AreEqual((float)123, nt["f"]);
            Assert.AreEqual(typeof(long), nt["l"].GetType()); Assert.AreEqual((long)123, nt["l"]);
            Assert.AreEqual(typeof(short), nt["s"].GetType()); Assert.AreEqual((short)123, nt["s"]);
            Assert.AreEqual(true, nt["t"]);
            Assert.AreEqual(xbytes, ((BinaryTableValue)nt["x"]).Bytes);
            Assert.AreEqual(null, nt["V"]);
        }
    }
}
