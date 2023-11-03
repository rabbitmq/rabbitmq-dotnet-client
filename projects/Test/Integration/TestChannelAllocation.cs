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
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using Xunit;

namespace Test.Integration
{
    public class TestChannelAllocation : IDisposable
    {
        public const int CHANNEL_COUNT = 100;

        IConnection _c;

        public TestChannelAllocation()
        {
            var cf = new ConnectionFactory
            {
                ContinuationTimeout = IntegrationFixture.WaitSpan,
                HandshakeContinuationTimeout = IntegrationFixture.WaitSpan,
                ClientProvidedName = nameof(TestChannelAllocation)
            };
            _c = cf.CreateConnection();
        }

        public void Dispose() => _c.Close();

        [Fact]
        public void AllocateInOrder()
        {
            var channels = new List<IChannel>();
            for (int i = 1; i <= CHANNEL_COUNT; i++)
            {
                IChannel channel = _c.CreateChannel();
                channels.Add(channel);
                Assert.Equal(i, ChannelNumber(channel));
            }

            foreach (IChannel channel in channels)
            {
                channel.Dispose();
            }
        }

        [Fact]
        public void AllocateAfterFreeingLast()
        {
            using IChannel ch0 = _c.CreateChannel();
            Assert.Equal(1, ChannelNumber(ch0));
            ch0.Close();

            using IChannel ch1 = _c.CreateChannel();
            Assert.Equal(1, ChannelNumber(ch1));
        }

        [Fact]
        public async Task AllocateAfterFreeingLastAsync()
        {
            using IChannel ch0 = _c.CreateChannel();
            Assert.Equal(1, ChannelNumber(ch0));
            await ch0.CloseAsync();

            using IChannel ch1 = _c.CreateChannel();
            Assert.Equal(1, ChannelNumber(ch1));
        }

        [Fact]
        public void AllocateAfterFreeingMany()
        {
            var channels = new List<IChannel>();

            for (int i = 1; i <= CHANNEL_COUNT; i++)
            {
                channels.Add(_c.CreateChannel());
            }

            foreach (IChannel channel in channels)
            {
                channel.Close();
            }

            channels.Clear();

            for (int j = 1; j <= CHANNEL_COUNT; j++)
            {
                channels.Add(_c.CreateChannel());
            }

            // In the current implementation the list should actually
            // already be sorted, but we don't want to force that behaviour
            channels.Sort(CompareChannels);

            int k = 1;
            foreach (IChannel channel in channels)
            {
                Assert.Equal(k++, ChannelNumber(channel));
                channel.Close();
            }
        }

        public int ChannelNumber(IChannel channel)
        {
            return ((AutorecoveringChannel)channel).ChannelNumber;
        }

        public int CompareChannels(IChannel x, IChannel y)
        {
            int i = ChannelNumber(x);
            int j = ChannelNumber(y);
            return (i < j) ? -1 : (i == j) ? 0 : 1;
        }
    }
}
