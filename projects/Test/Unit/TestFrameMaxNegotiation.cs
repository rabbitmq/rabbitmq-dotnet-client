// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------


using System;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Impl;
using Xunit;

namespace Test.Unit
{
    public class TestFrameMaxNegotiation
    {
        [Theory]
        [InlineData(0u, 0u, 0u)]
        [InlineData(0u, 0xFFFFFFFFu, 0xFFFFFFFFu)]
        [InlineData(0u, 131072u, 131072u)]
        [InlineData(131072u, 0u, 131072u)]
        [InlineData(8192u, 131072u, 8192u)]
        [InlineData(0u, 4096u, 4096u)]
        public void AFrameMaxOfZeroOrAtLeastTheMinimumIsAccepted_GHSA_qqh4(uint client, uint server, uint expected)
        {
            Assert.Equal(expected, Connection.NegotiateFrameMax(client, server));
        }

        [Theory]
        [InlineData(0u, 7u)]
        [InlineData(0u, 8u)]
        [InlineData(0u, 4095u)]
        [InlineData(8u, 0u)]
        [InlineData(131072u, 1u)]
        public void AFrameMaxBelowTheMinimumIsRejected_GHSA_qqh4(uint client, uint server)
        {
            Assert.Throws<ProtocolViolationException>(() => Connection.NegotiateFrameMax(client, server));
        }

        [Theory]
        [InlineData(0u, int.MaxValue)]
        [InlineData(4096u, 4088)]
        [InlineData(131072u, 131064)]
        [InlineData(0x80000007u, int.MaxValue)]
        [InlineData(0x80000008u, int.MaxValue)]
        [InlineData(0xFFFFFFFFu, int.MaxValue)]
        public void ThePayloadSizeIsPositiveForEveryAcceptedFrameMax_GHSA_qqh4(uint frameMax, int expected)
        {
            Assert.Equal(expected, Connection.MaxPayloadSizeFor(frameMax));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ANonPositiveBodyPayloadIsRejectedBeforeAnythingIsWritten_GHSA_qqh4(int maxBodyPayloadBytes)
        {
            // An empty body: without the guard it frames normally, so no regression run writes past a buffer.
            var method = new BasicPublish("exchange", "routing-key", false, false);
            var header = new BasicProperties();

            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                RabbitMQ.Client.Impl.Framing.SerializeToFrames(ref method, ref header, ReadOnlyMemory<byte>.Empty, null, 1,
                    maxBodyPayloadBytes));
            Assert.Equal("maxBodyPayloadBytes", ex.ParamName);
        }
    }
}
