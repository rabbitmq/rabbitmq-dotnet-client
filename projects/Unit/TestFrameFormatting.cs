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
using System.Buffers;
using System.Runtime.InteropServices;
using NUnit.Framework;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    class TestFrameFormatting : WireFormattingFixture
    {
        [Test]
        public void HeartbeatFrame()
        {
            var memory = Impl.Framing.Heartbeat.GetHeartbeatFrame(ArrayPool<byte>.Shared);
            var frameSpan = memory.Span;

            try
            {
                Assert.AreEqual(8, frameSpan.Length);
                Assert.AreEqual(Constants.FrameHeartbeat, frameSpan[0]);
                Assert.AreEqual(0, frameSpan[1]); // channel
                Assert.AreEqual(0, frameSpan[2]); // channel
                Assert.AreEqual(0, frameSpan[3]); // payload size
                Assert.AreEqual(0, frameSpan[4]); // payload size
                Assert.AreEqual(0, frameSpan[5]); // payload size
                Assert.AreEqual(0, frameSpan[6]); // payload size
                Assert.AreEqual(Constants.FrameEnd, frameSpan[7]);
            }
            finally
            {
                if (MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment))
                {
                    ArrayPool<byte>.Shared.Return(segment.Array);
                }
            }
        }

        [Test]
        public void HeaderFrame()
        {
            const int Channel = 3;
            const int BodyLength = 10;

            var basicProperties = new Framing.BasicProperties { AppId = "A" };
            int payloadSize = basicProperties.GetRequiredPayloadBufferSize();
            var frameBytes = new byte[Impl.Framing.Header.FrameSize + BodyLength + payloadSize];
            Impl.Framing.Header.WriteTo(frameBytes, Channel, basicProperties, BodyLength);

            Assert.AreEqual(20, Impl.Framing.Header.FrameSize);
            Assert.AreEqual(Constants.FrameHeader, frameBytes[0]);
            Assert.AreEqual(0, frameBytes[1]);       // channel
            Assert.AreEqual(Channel, frameBytes[2]); // channel
            Assert.AreEqual(0, frameBytes[3]);                // payload size
            Assert.AreEqual(0, frameBytes[4]);                // payload size
            Assert.AreEqual(0, frameBytes[5]);                // payload size
            Assert.AreEqual(12 + payloadSize, frameBytes[6]); // payload size
            Assert.AreEqual(0, frameBytes[7]);                    // ProtocolClassId
            Assert.AreEqual(ClassConstants.Basic, frameBytes[8]); // ProtocolClassId
            Assert.AreEqual(0, frameBytes[9]);  // Weight
            Assert.AreEqual(0, frameBytes[10]); // Weight
            Assert.AreEqual(0, frameBytes[11]);          // BodyLength
            Assert.AreEqual(0, frameBytes[12]);          // BodyLength
            Assert.AreEqual(0, frameBytes[13]);          // BodyLength
            Assert.AreEqual(0, frameBytes[14]);          // BodyLength
            Assert.AreEqual(0, frameBytes[15]);          // BodyLength
            Assert.AreEqual(0, frameBytes[16]);          // BodyLength
            Assert.AreEqual(0, frameBytes[17]);          // BodyLength
            Assert.AreEqual(BodyLength, frameBytes[18]); // BodyLength
            Assert.AreEqual(0b0000_0000, frameBytes[19]); // Presence
            Assert.AreEqual(0b0000_1000, frameBytes[20]); // Presence
            Assert.AreEqual(1, frameBytes[21]); // AppId Length
            Assert.AreEqual((byte)'A', frameBytes[22]); // AppId payload
            Assert.AreEqual(Constants.FrameEnd, frameBytes[23]);
        }

        [Test]
        public void MethodFrame()
        {
            const int Channel = 3;

            var method = new BasicPublish(0, "E", "R", true, true);
            int payloadSize = method.GetRequiredBufferSize();
            var frameBytes = new byte[Impl.Framing.Method.FrameSize + payloadSize];
            Impl.Framing.Method.WriteTo(frameBytes, Channel, method);

            Assert.AreEqual(12, Impl.Framing.Method.FrameSize);
            Assert.AreEqual(Constants.FrameMethod, frameBytes[0]);
            Assert.AreEqual(0, frameBytes[1]);       // channel
            Assert.AreEqual(Channel, frameBytes[2]); // channel
            Assert.AreEqual(0, frameBytes[3]);               // payload size
            Assert.AreEqual(0, frameBytes[4]);               // payload size
            Assert.AreEqual(0, frameBytes[5]);               // payload size
            Assert.AreEqual(4 + payloadSize, frameBytes[6]); // payload size
            Assert.AreEqual(0, frameBytes[7]);                    // ProtocolClassId
            Assert.AreEqual(ClassConstants.Basic, frameBytes[8]); // ProtocolClassId
            Assert.AreEqual(0, frameBytes[9]);                             // ProtocolMethodId
            Assert.AreEqual(BasicMethodConstants.Publish, frameBytes[10]); // ProtocolMethodId
            Assert.AreEqual(0, frameBytes[11]); // reserved1
            Assert.AreEqual(0, frameBytes[12]); // reserved1
            Assert.AreEqual(1, frameBytes[13]);         // Exchange length
            Assert.AreEqual((byte)'E', frameBytes[14]); // Exchange payload
            Assert.AreEqual(1, frameBytes[15]);         // RoutingKey length
            Assert.AreEqual((byte)'R', frameBytes[16]); // RoutingKey payload
            Assert.AreEqual(0b0000_0011, frameBytes[17]); // Mandatory & Immediate flags
            Assert.AreEqual(Constants.FrameEnd, frameBytes[18]);
        }

        [Test]
        public void BodySegmentFrame()
        {
            const int Channel = 3;

            var payload = new byte[4];
            var frameBytes = new byte[Impl.Framing.BodySegment.FrameSize + payload.Length];
            Impl.Framing.BodySegment.WriteTo(frameBytes, Channel, payload);

            Assert.AreEqual(8, Impl.Framing.BodySegment.FrameSize);
            Assert.AreEqual(Constants.FrameBody, frameBytes[0]);
            Assert.AreEqual(0, frameBytes[1]);       // channel
            Assert.AreEqual(Channel, frameBytes[2]); // channel
            Assert.AreEqual(0, frameBytes[3]);              // payload size
            Assert.AreEqual(0, frameBytes[4]);              // payload size
            Assert.AreEqual(0, frameBytes[5]);              // payload size
            Assert.AreEqual(payload.Length, frameBytes[6]); // payload size
            Assert.AreEqual(0, frameBytes[7]);  // payload
            Assert.AreEqual(0, frameBytes[8]);  // payload
            Assert.AreEqual(0, frameBytes[9]);  // payload
            Assert.AreEqual(0, frameBytes[10]); // payload
            Assert.AreEqual(Constants.FrameEnd, frameBytes[11]);
        }
    }
}
