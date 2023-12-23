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
using RabbitMQ.Client;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;
using RabbitMQ.Util;
using Xunit;

namespace Test.Unit
{
    public class TestFrameFormatting : WireFormattingFixture
    {
        [Fact]
        public void HeartbeatFrame()
        {
            RentedOutgoingMemory sfc = Framing.Heartbeat.GetHeartbeatFrame();

            try
            {
                Assert.Equal(8, sfc.Size);

                Span<byte> frameSpan = stackalloc byte[8];
                sfc.Data.CopyTo(frameSpan);

                Assert.Equal(Constants.FrameHeartbeat, frameSpan[0]);
                Assert.Equal(0, frameSpan[1]); // channel
                Assert.Equal(0, frameSpan[2]); // channel
                Assert.Equal(0, frameSpan[3]); // payload size
                Assert.Equal(0, frameSpan[4]); // payload size
                Assert.Equal(0, frameSpan[5]); // payload size
                Assert.Equal(0, frameSpan[6]); // payload size
                Assert.Equal(Constants.FrameEnd, frameSpan[7]);
            }
            finally
            {
                sfc.Dispose();
            }
        }

        [Fact]
        public void HeaderFrame()
        {
            const int Channel = 3;
            const int BodyLength = 10;

            var basicProperties = new BasicProperties { AppId = "A" };
            int payloadSize = ((IAmqpWriteable)basicProperties).GetRequiredBufferSize();
            byte[] frameBytes = new byte[RabbitMQ.Client.Impl.Framing.Header.FrameSize + BodyLength + payloadSize];
            RabbitMQ.Client.Impl.Framing.Header.WriteTo(frameBytes, Channel, ref basicProperties, BodyLength);

            Assert.Equal(20, RabbitMQ.Client.Impl.Framing.Header.FrameSize);
            Assert.Equal(Constants.FrameHeader, frameBytes[0]);
            Assert.Equal(0, frameBytes[1]);       // channel
            Assert.Equal(Channel, frameBytes[2]); // channel
            Assert.Equal(0, frameBytes[3]);                // payload size
            Assert.Equal(0, frameBytes[4]);                // payload size
            Assert.Equal(0, frameBytes[5]);                // payload size
            Assert.Equal(12 + payloadSize, frameBytes[6]); // payload size
            Assert.Equal(0, frameBytes[7]);                    // ProtocolClassId
            Assert.Equal(ClassConstants.Basic, frameBytes[8]); // ProtocolClassId
            Assert.Equal(0, frameBytes[9]);  // Weight
            Assert.Equal(0, frameBytes[10]); // Weight
            Assert.Equal(0, frameBytes[11]);          // BodyLength
            Assert.Equal(0, frameBytes[12]);          // BodyLength
            Assert.Equal(0, frameBytes[13]);          // BodyLength
            Assert.Equal(0, frameBytes[14]);          // BodyLength
            Assert.Equal(0, frameBytes[15]);          // BodyLength
            Assert.Equal(0, frameBytes[16]);          // BodyLength
            Assert.Equal(0, frameBytes[17]);          // BodyLength
            Assert.Equal(BodyLength, frameBytes[18]); // BodyLength
            Assert.Equal(0b0000_0000, frameBytes[19]); // Presence
            Assert.Equal(0b0000_1000, frameBytes[20]); // Presence
            Assert.Equal(1, frameBytes[21]); // AppId Length
            Assert.Equal((byte)'A', frameBytes[22]); // AppId payload
            Assert.Equal(Constants.FrameEnd, frameBytes[23]);
        }

        [Fact]
        public void MethodFrame()
        {
            const int Channel = 3;

            var method = new BasicPublish("E", "R", true, true);
            int payloadSize = method.GetRequiredBufferSize();
            byte[] frameBytes = new byte[RabbitMQ.Client.Impl.Framing.Method.FrameSize + payloadSize];
            RabbitMQ.Client.Impl.Framing.Method.WriteTo(frameBytes, Channel, ref method);

            Assert.Equal(12, RabbitMQ.Client.Impl.Framing.Method.FrameSize);
            Assert.Equal(Constants.FrameMethod, frameBytes[0]);
            Assert.Equal(0, frameBytes[1]);       // channel
            Assert.Equal(Channel, frameBytes[2]); // channel
            Assert.Equal(0, frameBytes[3]);               // payload size
            Assert.Equal(0, frameBytes[4]);               // payload size
            Assert.Equal(0, frameBytes[5]);               // payload size
            Assert.Equal(4 + payloadSize, frameBytes[6]); // payload size
            Assert.Equal(0, frameBytes[7]);                    // ProtocolClassId
            Assert.Equal(ClassConstants.Basic, frameBytes[8]); // ProtocolClassId
            Assert.Equal(0, frameBytes[9]);                             // ProtocolMethodId
            Assert.Equal(BasicMethodConstants.Publish, frameBytes[10]); // ProtocolMethodId
            Assert.Equal(0, frameBytes[11]); // reserved1
            Assert.Equal(0, frameBytes[12]); // reserved1
            Assert.Equal(1, frameBytes[13]);         // Exchange length
            Assert.Equal((byte)'E', frameBytes[14]); // Exchange payload
            Assert.Equal(1, frameBytes[15]);         // RoutingKey length
            Assert.Equal((byte)'R', frameBytes[16]); // RoutingKey payload
            Assert.Equal(0b0000_0011, frameBytes[17]); // Mandatory & Immediate flags
            Assert.Equal(Constants.FrameEnd, frameBytes[18]);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void BodySegmentFrame(bool copyBody)
        {
            const int Channel = 3;

            byte[] payload = new byte[4];
            byte[] buffer = new byte[RabbitMQ.Client.Impl.Framing.BodySegment.FrameSize + (copyBody ? payload.Length : 0)];

            SequenceBuilder<byte> builder = new SequenceBuilder<byte>();

            RabbitMQ.Client.Impl.Framing.BodySegment.WriteTo(ref builder, buffer, Channel, new ReadOnlySequence<byte>(payload), copyBody);

            var sequence = builder.Build();

            if (copyBody)
            {
                // When copying the body, the memory is sequential
                Assert.True(sequence.IsSingleSegment);
            }

            byte[] frameBytes = sequence.ToArray();

            Assert.Equal(8, RabbitMQ.Client.Impl.Framing.BodySegment.FrameSize);
            Assert.Equal(Constants.FrameBody, frameBytes[0]);
            Assert.Equal(0, frameBytes[1]);       // channel
            Assert.Equal(Channel, frameBytes[2]); // channel
            Assert.Equal(0, frameBytes[3]);              // payload size
            Assert.Equal(0, frameBytes[4]);              // payload size
            Assert.Equal(0, frameBytes[5]);              // payload size
            Assert.Equal(payload.Length, frameBytes[6]); // payload size
            Assert.Equal(0, frameBytes[7]);  // payload
            Assert.Equal(0, frameBytes[8]);  // payload
            Assert.Equal(0, frameBytes[9]);  // payload
            Assert.Equal(0, frameBytes[10]); // payload
            Assert.Equal(Constants.FrameEnd, frameBytes[11]);
        }
    }
}
