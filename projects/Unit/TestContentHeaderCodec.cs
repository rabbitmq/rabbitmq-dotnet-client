// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
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
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at https://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;

using NUnit.Framework;

using RabbitMQ.Client.Impl;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    class TestContentHeaderCodec
    {
        public void Check(ReadOnlyMemory<byte> actual, ReadOnlyMemory<byte> expected)
        {
            try
            {
                Assert.AreEqual(expected.ToArray(), actual.ToArray());
            }
            catch
            {
                Console.WriteLine();
                Console.WriteLine("EXPECTED ==================================================");
                DebugUtil.Dump(expected.ToArray(), Console.Out);
                Console.WriteLine("ACTUAL ====================================================");
                DebugUtil.Dump(actual.ToArray(), Console.Out);
                Console.WriteLine("===========================================================");
                throw;
            }
        }

        [Test]
        public void TestPresence()
        {
            var memory = new byte[1024];
            var m_w = new ContentHeaderPropertyWriter(memory);
            m_w.WritePresence(false);
            m_w.WritePresence(true);
            m_w.WritePresence(false);
            m_w.WritePresence(true);
            m_w.FinishPresence();
            Check(memory.AsMemory().Slice(0, m_w.Offset), new byte[] { 0x50, 0x00 });
        }

        [Test]
        public void TestLongPresence()
        {
            var memory = new byte[1024];
            var m_w = new ContentHeaderPropertyWriter(memory);

            m_w.WritePresence(false);
            m_w.WritePresence(true);
            m_w.WritePresence(false);
            m_w.WritePresence(true);
            for (int i = 0; i < 20; i++)
            {
                m_w.WritePresence(false);
            }
            m_w.WritePresence(true);
            m_w.FinishPresence();
            Check(memory.AsMemory().Slice(0, m_w.Offset), new byte[] { 0x50, 0x01, 0x00, 0x40 });
        }

        [Test]
        public void TestNoPresence()
        {
            var memory = new byte[1024];
            var m_w = new ContentHeaderPropertyWriter(memory);
            m_w.FinishPresence();
            Check(memory.AsMemory().Slice(0, m_w.Offset), new byte[] { 0x00, 0x00 });
        }

        [Test]
        public void TestBodyLength()
        {
            RabbitMQ.Client.Framing.BasicProperties prop =
                new RabbitMQ.Client.Framing.BasicProperties();
            int bytesNeeded = prop.GetRequiredBufferSize();
            byte[] bytes = new byte[bytesNeeded];
            int bytesWritten = prop.WriteTo(bytes, 0x123456789ABCDEF0UL);
            prop.WriteTo(bytes, 0x123456789ABCDEF0UL);
            Check(bytes.AsMemory().Slice(0, bytesWritten), new byte[] { 0x00, 0x00, // weight
			          0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0, // body len
			          0x00, 0x00}); // props flags
        }

        [Test]
        public void TestSimpleProperties()
        {
            RabbitMQ.Client.Framing.BasicProperties prop =
                new RabbitMQ.Client.Framing.BasicProperties
                {
                    ContentType = "text/plain"
                };
            int bytesNeeded = prop.GetRequiredBufferSize();
            byte[] bytes = new byte[bytesNeeded];
            int bytesWritten = prop.WriteTo(bytes, 0x123456789ABCDEF0UL);
            Check(bytes.AsMemory().Slice(0, bytesWritten), new byte[] { 0x00, 0x00, // weight
			          0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0, // body len
			          0x80, 0x00, // props flags
			          0x0A, // shortstr len
			          0x74, 0x65, 0x78, 0x74,
                      0x2F, 0x70, 0x6C, 0x61,
                      0x69, 0x6E });
        }
    }
}
