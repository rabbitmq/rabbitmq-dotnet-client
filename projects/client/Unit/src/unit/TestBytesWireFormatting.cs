// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2016 Pivotal Software, Inc.
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
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;

using System;
using System.IO;

using RabbitMQ.Util;
using RabbitMQ.Client.Content;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestBytesWireFormatting : WireFormattingFixture
    {
        [Test]
        public void TestSingleDecoding()
        {
            Assert.AreEqual(1.234f,
                            BytesWireFormatting.ReadSingle(Reader
                                                           (new byte[] { 63, 157, 243, 182 })));
        }

        [Test]
        public void TestSingleEncoding()
        {
            NetworkBinaryWriter w = Writer();
            BytesWireFormatting.WriteSingle(w, 1.234f);
            Check(w, new byte[] { 63, 157, 243, 182 });
        }

        [Test]
        public void TestDoubleDecoding()
        {
            Assert.AreEqual(1.234,
                            BytesWireFormatting.ReadDouble(Reader
                                                           (new byte[] { 63, 243, 190, 118,
                                                                         200, 180, 57, 88 })));
        }

        [Test]
        public void TestDoubleEncoding()
        {
            NetworkBinaryWriter w = Writer();
            BytesWireFormatting.WriteDouble(w, 1.234);
            Check(w, new byte[] { 63, 243, 190, 118, 200, 180, 57, 88 });
        }
    }
}
