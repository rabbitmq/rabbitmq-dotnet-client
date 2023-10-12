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


using System.Collections;
using System.Collections.Generic;
using System.Text;
using RabbitMQ.Client.Impl;
using Xunit;

namespace RabbitMQ.Client.Unit
{
    public class TestFieldTableFormattingGeneric : WireFormattingFixture
    {
        [Fact]
        public void TestStandardTypes()
        {
            IDictionary<string, object> t = new Dictionary<string, object>
            {
                ["string"] = "Hello",
                ["int"] = 1234,
                ["uint"] = 1234u,
                ["decimal"] = 12.34m,
                ["timestamp"] = new AmqpTimestamp(0)
            };
            IDictionary<string, object> t2 = new Dictionary<string, object>();
            t["fieldtable"] = t2;
            t2["test"] = "test";
            IList array = new List<object>
            {
                "longstring",
                1234
            };
            t["fieldarray"] = array;
            int bytesNeeded = WireFormatting.GetTableByteCount(t);
            byte[] bytes = new byte[bytesNeeded];
            WireFormatting.WriteTable(ref bytes.GetStart(), t);
            int bytesRead = WireFormatting.ReadDictionary(bytes, out var nt);
            Assert.Equal(bytesNeeded, bytesRead);
            Assert.Equal(Encoding.UTF8.GetBytes("Hello"), nt["string"]);
            Assert.Equal(1234, nt["int"]);
            Assert.Equal(1234u, nt["uint"]);
            Assert.Equal(12.34m, nt["decimal"]);
            Assert.Equal(0, ((AmqpTimestamp)nt["timestamp"]).UnixTime);
            IDictionary<string, object> nt2 = (IDictionary<string, object>)nt["fieldtable"];
            Assert.Equal(Encoding.UTF8.GetBytes("test"), nt2["test"]);
            IList<object> narray = (IList<object>)nt["fieldarray"];
            Assert.Equal(Encoding.UTF8.GetBytes("longstring"), narray[0]);
            Assert.Equal(1234, narray[1]);
        }

        [Fact]
        public void TestTableEncoding_S()
        {
            IDictionary<string, object> t = new Dictionary<string, object>
            {
                ["a"] = "bc"
            };
            int bytesNeeded = WireFormatting.GetTableByteCount(t);
            byte[] bytes = new byte[bytesNeeded];
            WireFormatting.WriteTable(ref bytes.GetStart(), t);
            int bytesRead = WireFormatting.ReadDictionary(bytes, out _);
            Assert.Equal(bytesNeeded, bytesRead);
            Check(bytes, new byte[] {
                    0,0,0,9, // table length
                    1,(byte)'a', // key
                    (byte)'S', // type
                    0,0,0,2,(byte)'b',(byte)'c' // value
                });
        }

        [Fact]
        public void TestTableEncoding_x()
        {
            IDictionary<string, object> t = new Dictionary<string, object>
            {
                ["a"] = new BinaryTableValue(new byte[] { 0xaa, 0x55 })
            };
            int bytesNeeded = WireFormatting.GetTableByteCount(t);
            byte[] bytes = new byte[bytesNeeded];
            WireFormatting.WriteTable(ref bytes.GetStart(), t);
            int bytesRead = WireFormatting.ReadDictionary(bytes, out _);
            Assert.Equal(bytesNeeded, bytesRead);
            Check(bytes, new byte[] {
                    0,0,0,9, // table length
                    1,(byte)'a', // key
                    (byte)'x', // type
                    0,0,0,2,0xaa,0x55 // value
                });
        }

        [Fact]
        public void TestQpidJmsTypes()
        {
            IDictionary<string, object> t = new Dictionary<string, object>
            {
                ["B"] = (byte)255,
                ["b"] = (sbyte)-128,
                ["d"] = (double)123,
                ["f"] = (float)123,
                ["l"] = (long)123,
                ["s"] = (short)123,
                ["u"] = (ushort)123,
                ["t"] = true
            };
            byte[] xbytes = new byte[] { 0xaa, 0x55 };
            t["x"] = new BinaryTableValue(xbytes);
            t["V"] = null;
            int bytesNeeded = WireFormatting.GetTableByteCount(t);
            byte[] bytes = new byte[bytesNeeded];
            WireFormatting.WriteTable(ref bytes.GetStart(), t);
            int bytesRead = WireFormatting.ReadDictionary(bytes, out var nt);
            Assert.Equal(bytesNeeded, bytesRead);
            Assert.Equal(typeof(byte), nt["B"].GetType()); Assert.Equal((byte)255, nt["B"]);
            Assert.Equal(typeof(sbyte), nt["b"].GetType()); Assert.Equal((sbyte)-128, nt["b"]);
            Assert.Equal(typeof(double), nt["d"].GetType()); Assert.Equal((double)123, nt["d"]);
            Assert.Equal(typeof(float), nt["f"].GetType()); Assert.Equal((float)123, nt["f"]);
            Assert.Equal(typeof(long), nt["l"].GetType()); Assert.Equal((long)123, nt["l"]);
            Assert.Equal(typeof(short), nt["s"].GetType()); Assert.Equal((short)123, nt["s"]);
            Assert.Equal(typeof(ushort), nt["u"].GetType()); Assert.Equal((ushort)123, nt["u"]);
            Assert.Equal(true, nt["t"]);
            Assert.Equal(xbytes, ((BinaryTableValue)nt["x"]).Bytes);
            Assert.Null(nt["V"]);
        }
    }
}
