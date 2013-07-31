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
using System.IO;

using RabbitMQ.Util;
using RabbitMQ.Client.Content;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestStreamWireFormatting : WireFormattingFixture
    {
        [Test]
        public void TestSingleDecoding1()
        {
            Assert.AreEqual(1.234f,
                            StreamWireFormatting.ReadSingle(Reader
                                                            (new byte[] { 8, 63, 157, 243, 182 })));
        }

        [Test]
        public void TestSingleDecoding2()
        {
            Assert.AreEqual("1.234",
                            StreamWireFormatting.ReadString(Reader
                                                            (new byte[] { 8, 63, 157, 243, 182 })));
        }


        [Test]
        public void TestRoundTrip()
        {
            NetworkBinaryWriter w = Writer();
            StreamWireFormatting.WriteBool(w, true);
            StreamWireFormatting.WriteInt32(w, 1234);
            StreamWireFormatting.WriteInt16(w, 1234);
            StreamWireFormatting.WriteByte(w, 123);
            StreamWireFormatting.WriteChar(w, 'x');
            StreamWireFormatting.WriteInt64(w, 1234);
            StreamWireFormatting.WriteSingle(w, 1.234f);
            StreamWireFormatting.WriteDouble(w, 1.234);
            StreamWireFormatting.WriteBytes(w, new byte[] { 1, 2, 3, 4 });
            StreamWireFormatting.WriteString(w, "hello");
            StreamWireFormatting.WriteObject(w, "world");
            NetworkBinaryReader r = Reader(Contents(w));
            Assert.AreEqual(true, StreamWireFormatting.ReadBool(r));
            Assert.AreEqual(1234, StreamWireFormatting.ReadInt32(r));
            Assert.AreEqual(1234, StreamWireFormatting.ReadInt16(r));
            Assert.AreEqual(123, StreamWireFormatting.ReadByte(r));
            Assert.AreEqual('x', StreamWireFormatting.ReadChar(r));
            Assert.AreEqual(1234, StreamWireFormatting.ReadInt64(r));
            Assert.AreEqual(1.234f, StreamWireFormatting.ReadSingle(r));
            Assert.AreEqual(1.234, StreamWireFormatting.ReadDouble(r));
            Assert.AreEqual(new byte[] { 1, 2, 3, 4 }, StreamWireFormatting.ReadBytes(r));
            Assert.AreEqual("hello", StreamWireFormatting.ReadString(r));
            Assert.AreEqual("world", StreamWireFormatting.ReadObject(r));
        }
    }
}
