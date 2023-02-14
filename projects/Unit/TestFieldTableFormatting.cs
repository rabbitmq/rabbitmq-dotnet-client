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
using System.IO;
using System.Text;

using RabbitMQ.Client.Impl;

using Xunit;

namespace RabbitMQ.Client.Unit
{
    public class TestFieldTableFormatting : WireFormattingFixture
    {
        [Fact]
        public void TestStandardTypes()
        {
            // Arrange
            var expectedTable = new Hashtable
            {
                ["string"] = "Hello",
                ["int"] = 1234,
                ["bool"] = true,
                ["byte[]"] = new[] {1, 2, 3, 4},
                ["float"] = 1234f,
                ["double"] = 1234D,
                ["long"] = 1234L,
                ["byte"] = (byte)123,
                ["sbyte"] = (sbyte)123,
                ["short"] = (short)1234,
                ["uint"] = 1234u,
                ["decimal"] = 12.34m,
                ["ushort"] = (ushort)1234,
            };
            expectedTable["AmqpTimestamp"] = new AmqpTimestamp(0);
            var expectedDic = new Hashtable { ["HashtableKey"] = "HashtableValue" };
            expectedTable["Hashtable"] = expectedDic;
            var expectedList = new ArrayList { "longstring", 1234 };
            expectedTable["ArrayList"] = expectedList;

            // Act
            int expectedTableSizeBytes = WireFormatting.GetTableByteCount(expectedTable);
            byte[] testTableBytes = new byte[expectedTableSizeBytes];
            WireFormatting.WriteTable(ref testTableBytes.GetStart(), expectedTable);
            int actualTableSizeBytes = WireFormatting.ReadDictionary(testTableBytes, out var actualTable);

            // Assert
            Assert.Equal(expectedTableSizeBytes, actualTableSizeBytes);
            Assert.Equal(expectedTable.Count, actualTable.Count);

            Assert.Equal(actualTable["string"], "Hello"u8.ToArray());
            Assert.Equal(actualTable["int"], 1234);
            Assert.Equal(actualTable["bool"], true);
            Assert.Equal(actualTable["byte[]"], new[] {1, 2, 3, 4});
            Assert.Equal(actualTable["float"], 1234f);
            Assert.Equal(actualTable["double"], 1234D);
            Assert.Equal(actualTable["long"], 1234L);
            Assert.Equal(actualTable["byte"], (byte)123);
            Assert.Equal(actualTable["sbyte"], (sbyte)123);
            Assert.Equal(actualTable["short"], (short)1234);
            Assert.Equal(actualTable["uint"], 1234u);
            Assert.Equal(actualTable["decimal"], 12.34m);
            Assert.Equal(actualTable["ushort"], (ushort)1234);

            Assert.Equal(actualTable["AmqpTimestamp"], new AmqpTimestamp(0));
            Assert.Equal(((IDictionary)expectedTable["Hashtable"])["HashtableKey"], "HashtableValue");
            Assert.Equal(((IList)expectedTable["ArrayList"])[0], "longstring");
            Assert.Equal(((IList)expectedTable["ArrayList"])[1], 1234);
        }

        [Fact]
        public void TestTableEncoding_S()
        {
            Hashtable t = new Hashtable
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
            Hashtable t = new Hashtable
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
        public void TestTableEncoding_LongKey()
        {
            const int TooLarge = 256;
            Hashtable t = new Hashtable
            {
                [new string('A', TooLarge)] = null
            };
            int bytesNeeded = WireFormatting.GetTableByteCount(t);
            byte[] bytes = new byte[bytesNeeded];

            Assert.Throws<ArgumentException>(() => WireFormatting.WriteTable(ref bytes.GetStart(), t));
        }

        [Fact]
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
                ["u"] = (ushort)123,
                ["t"] = true // 2+2
            };
            byte[] xbytes = { 0xaa, 0x55 };
            t["x"] = new BinaryTableValue(xbytes); // 2+5+2
            t["V"] = null; // 2+1
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
