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
using System.Linq;

using RabbitMQ.Client.Impl;

using Xunit;

namespace RabbitMQ.Client.Unit
{

    public class TestFieldTableFormattingGeneric : WireFormattingFixture
    {
        [Fact]
        public void TestStandardTypes()
        {
            // Arrange
            IDictionary<string, object> expectedTable = new Dictionary<string, object>
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
                ["ulong"] = 1234UL,
            };
            expectedTable["BinaryTableValue"] = new BinaryTableValue(new byte[] { 1, 2, 3, 4 });
            expectedTable["AmqpTimestamp"] = new AmqpTimestamp(0);
            expectedTable["Dictionary"] = new Hashtable { ["DictionaryKey"] = "DictionaryValue" };
            expectedTable["List"] = new List<object> { "longstring", 1234 };

            // Act
            int expectedTableSizeBytes = WireFormatting.GetTableByteCount(expectedTable);
            byte[] testTableBytes = new byte[expectedTableSizeBytes];
            WireFormatting.WriteTable(ref testTableBytes.GetStart(), expectedTable);
            int actualTableSizeBytes = WireFormatting.ReadDictionary(testTableBytes, out var actualTable);

            // Assert
            Assert.Equal(expectedTableSizeBytes, actualTableSizeBytes);
            Assert.Equal(expectedTable.Count, actualTable.Count);

            Assert.Equal("Hello"u8.ToArray(), actualTable["string"]);
            Assert.Equal(1234, actualTable["int"]);
            Assert.Equal(true, actualTable["bool"]);
            Assert.Equal(new[] {1, 2, 3, 4}, actualTable["byte[]"]);
            Assert.Equal(1234f, actualTable["float"]);
            Assert.Equal(1234D, actualTable["double"]);
            Assert.Equal(1234L, actualTable["long"]);
            Assert.Equal((byte)123, actualTable["byte"]);
            Assert.Equal((sbyte)123, actualTable["sbyte"]);
            Assert.Equal((short)1234, actualTable["short"]);
            Assert.Equal(1234u, actualTable["uint"]);
            Assert.Equal(12.34m, actualTable["decimal"]);
            Assert.Equal((ushort)1234, actualTable["ushort"]);
            Assert.Equal(1234UL, actualTable["ulong"]);

            Assert.IsType<BinaryTableValue>(actualTable["BinaryTableValue"]);
            Assert.True(((BinaryTableValue)actualTable["BinaryTableValue"]).Bytes.SequenceEqual(new byte[] { 1, 2, 3, 4 }));
            Assert.Equal(new AmqpTimestamp(0), actualTable["AmqpTimestamp"]);
            Assert.Equal("DictionaryValue", ((IDictionary)expectedTable["Dictionary"])["DictionaryKey"]);
            Assert.Equal("longstring", ((IList)expectedTable["List"])[0]);
            Assert.Equal(1234, ((IList)expectedTable["List"])[1]);
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
