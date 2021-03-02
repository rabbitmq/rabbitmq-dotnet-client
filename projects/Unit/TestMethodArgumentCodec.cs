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
    public class TestMethodArgumentCodec
    {
        public void Check(byte[] actual, byte[] expected)
        {
            try
            {
                Assert.AreEqual(expected, actual);
            }
            catch
            {
                Console.WriteLine();
                Console.WriteLine("EXPECTED ==================================================");
                DebugUtil.Dump(expected, Console.Out);
                Console.WriteLine("ACTUAL ====================================================");
                DebugUtil.Dump(actual, Console.Out);
                Console.WriteLine("===========================================================");
                throw;
            }
        }

        [Test]
        public void TestTableLengthWrite()
        {
            var t = new Hashtable
            {
                ["abc"] = "def"
            };

            int bytesNeeded = WireFormatting.GetTableByteCount(t);
            byte[] memory = new byte[bytesNeeded];
            int offset = WireFormatting.WriteTable(memory.AsSpan(), 0, t);
            Assert.AreEqual(bytesNeeded, offset);
            Check(memory, new byte[] { 0x00, 0x00, 0x00, 0x0C,
                                   0x03, 0x61, 0x62, 0x63,
                                   0x53, 0x00, 0x00, 0x00,
                                   0x03, 0x64, 0x65, 0x66 });
        }

        [Test]
        public void TestDictionaryLengthRead()
        {
            WireFormatting.ReadDictionary(new byte[] {
                0x00, 0x00, 0x00, 0x0C,
                0x03, 0x61, 0x62, 0x63,
                0x53, 0x00, 0x00, 0x00,
                0x03, 0x64, 0x65, 0x66 }, 0, out var t);
            Assert.AreEqual(Encoding.UTF8.GetBytes("def"), t["abc"]);
            Assert.AreEqual(1, t.Count);
        }

        [Test]
        public void TestNestedTableWrite()
        {
            Hashtable t = new Hashtable();
            Hashtable x = new Hashtable
            {
                ["y"] = 0x12345678
            };
            t["x"] = x;
            int bytesNeeded = WireFormatting.GetTableByteCount(t);
            byte[] memory = new byte[bytesNeeded];
            int offset = WireFormatting.WriteTable(memory.AsSpan(), 0, t);
            Assert.AreEqual(bytesNeeded, offset);
            Check(memory, new byte[] { 0x00, 0x00, 0x00, 0x0E,
                                   0x01, 0x78, 0x46, 0x00,
                                   0x00, 0x00, 0x07, 0x01,
                                   0x79, 0x49, 0x12, 0x34,
                                   0x56, 0x78 });
        }
    }
}
