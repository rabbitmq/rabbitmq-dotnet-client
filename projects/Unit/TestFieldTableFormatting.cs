// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
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
// The MPL v2.0:
//
//---------------------------------------------------------------------------
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections;
using System.Text;

using NUnit.Framework;

using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    class TestFieldTableFormatting : WireFormattingFixture
    {
        [Test]
        public void TestStandardTypes()
        {
            Hashtable t = new Hashtable
            {
                ["string"] = "Hello",
                ["int"] = 1234,
                ["uint"] = 1234u,
                ["decimal"] = 12.34m,
                ["timestamp"] = new AmqpTimestamp(0)
            };
            Hashtable t2 = new Hashtable();
            t["fieldtable"] = t2;
            t2["test"] = "test";
            IList array = new ArrayList
            {
                "longstring",
                1234
            };
            t["fieldarray"] = array;
            int bytesNeeded = WireFormatting.GetTableByteCount(t);
            byte[] bytes = new byte[bytesNeeded];
            WireFormatting.WriteTable(bytes, 0, t);
            int bytesRead = WireFormatting.ReadDictionary(bytes, 0, out var nt);
            Assert.AreEqual(bytesNeeded, bytesRead);
            Assert.AreEqual(Encoding.UTF8.GetBytes("Hello"), nt["string"]);
            Assert.AreEqual(1234, nt["int"]);
            Assert.AreEqual(1234u, nt["uint"]);
            Assert.AreEqual(12.34m, nt["decimal"]);
            Assert.AreEqual(0, ((AmqpTimestamp)nt["timestamp"]).UnixTime);
            IDictionary nt2 = (IDictionary)nt["fieldtable"];
            Assert.AreEqual(Encoding.UTF8.GetBytes("test"), nt2["test"]);
            IList narray = (IList)nt["fieldarray"];
            Assert.AreEqual(Encoding.UTF8.GetBytes("longstring"), narray[0]);
            Assert.AreEqual(1234, narray[1]);
        }

        [Test]
        public void TestTableEncoding_S()
        {
            Hashtable t = new Hashtable
            {
                ["a"] = "bc"
            };
            int bytesNeeded = WireFormatting.GetTableByteCount(t);
            byte[] bytes = new byte[bytesNeeded];
            WireFormatting.WriteTable(bytes, 0, t);
            int bytesRead = WireFormatting.ReadDictionary(bytes, 0, out _);
            Assert.AreEqual(bytesNeeded, bytesRead);
            Check(bytes, new byte[] {
                    0,0,0,9, // table length
                    1,(byte)'a', // key
                    (byte)'S', // type
                    0,0,0,2,(byte)'b',(byte)'c' // value
                });
        }

        [Test]
        public void TestTableEncoding_x()
        {
            Hashtable t = new Hashtable
            {
                ["a"] = new BinaryTableValue(new byte[] { 0xaa, 0x55 })
            };
            int bytesNeeded = WireFormatting.GetTableByteCount(t);
            byte[] bytes = new byte[bytesNeeded];
            WireFormatting.WriteTable(bytes, 0, t);
            int bytesRead = WireFormatting.ReadDictionary(bytes, 0, out _);
            Assert.AreEqual(bytesNeeded, bytesRead);
            Check(bytes, new byte[] {
                    0,0,0,9, // table length
                    1,(byte)'a', // key
                    (byte)'x', // type
                    0,0,0,2,0xaa,0x55 // value
                });
        }

        [Test]
        public void TestTableEncoding_LongKey()
        {
            const int TooLarge = 256;
            Hashtable t = new Hashtable
            {
                [new string('A', TooLarge)] = null
            };
            int bytesNeeded = WireFormatting.GetTableByteCount(t);
            byte[] bytes = new byte[bytesNeeded];

            Assert.Throws<ArgumentOutOfRangeException>(() => WireFormatting.WriteTable(bytes, 0, t));
        }

        [Test]
        public void TestQpidJmsTypes()
        {
            Hashtable t = new Hashtable // 4
            {
                ["B"] = (byte)255, // 2+2
                ["b"] = (sbyte)-128, // 2+2
                ["d"] = (double)123, // 2+9
                ["f"] = (float)123,  // 2+5
                ["l"] = (long)123, // 2+9
                ["s"] = (short)123, // 2+2
                ["t"] = true // 2+2
            };
            byte[] xbytes = { 0xaa, 0x55 };
            t["x"] = new BinaryTableValue(xbytes); // 2+5+2
            t["V"] = null; // 2+1
            int bytesNeeded = WireFormatting.GetTableByteCount(t);
            byte[] bytes = new byte[bytesNeeded];
            WireFormatting.WriteTable(bytes, 0, t);
            int bytesRead = WireFormatting.ReadDictionary(bytes, 0, out var nt);
            Assert.AreEqual(bytesNeeded, bytesRead);
            Assert.AreEqual(typeof(byte), nt["B"].GetType()); Assert.AreEqual((byte)255, nt["B"]);
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
