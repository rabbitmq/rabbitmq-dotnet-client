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
using System.Collections.Generic;
using NUnit.Framework;

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
        public void TestSimpleProperties()
        {
            Framing.BasicProperties prop = new Framing.BasicProperties
                {
                    ContentType = "text/plain"
                };
            int bytesNeeded = prop.GetRequiredPayloadBufferSize();
            byte[] bytes = new byte[bytesNeeded];
            int offset = prop.WritePropertiesTo(bytes);
            Check(bytes.AsMemory().Slice(0, offset), new byte[] {
                     0x80, 0x00, // props flags
                     0x0A, // shortstr len
                     0x74, 0x65, 0x78, 0x74, 0x2F, 0x70, 0x6C, 0x61, 0x69, 0x6E // text/plain
            });
        }

        [Test]
        public void TestFullProperties()
        {
            Framing.BasicProperties prop = new Framing.BasicProperties
                {
                    AppId = "A",
                    ContentType = "B",
                    ContentEncoding = "C",
                    ClusterId = "D",
                    CorrelationId = "E",
                    DeliveryMode = 1,
                    Expiration = "F",
                    MessageId = "G",
                    Priority = 2,
                    Timestamp = new AmqpTimestamp(3),
                    Type = "H",
                    ReplyTo = "I",
                    UserId = "J",
                    Headers = new Dictionary<string, object>(0)
                };
            int bytesNeeded = prop.GetRequiredPayloadBufferSize();
            byte[] bytes = new byte[bytesNeeded];
            int offset = prop.WritePropertiesTo(bytes);
            Check(bytes.AsMemory().Slice(0, offset), new byte[] {
                     0b1111_1111, 0b1111_1100, // props flags (all set)
                     0x01, 0x42, // ContentType
                     0x01, 0x43, // ContentEncoding
                     0x00, 0x00, 0x00, 0x00, // Headers (length 0)
                     0x01,       // DeliveryMode
                     0x02,       // Priority
                     0x01, 0x45, // CorrelationId
                     0x01, 0x49, // ReplyTo
                     0x01, 0x46, // Expiration
                     0x01, 0x47, // MessageId
                     0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, // Timestamp
                     0x01, 0x48, // Type
                     0x01, 0x4A, // UserId
                     0x01, 0x41, // AppId
                     0x01, 0x44, // ClusterId
            });
        }
    }
}
