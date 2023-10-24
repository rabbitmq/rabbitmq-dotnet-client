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
using RabbitMQ.Client.Impl;
using Xunit;

namespace RabbitMQ.Client.Unit
{
    [Collection("IntegrationFixture")]
    public class TestChannelAllocation : IDisposable
    {
        public const int CHANNEL_COUNT = 100;

        IConnection _c;

        public int ChannelNumber(IChannel channel)
        {
            return ((AutorecoveringChannel)channel).ChannelNumber;
        }

        public TestChannelAllocation()
        {
            var cf = new ConnectionFactory();
            _c = cf.CreateConnection();
        }

        public void Dispose() => _c.Close();

        [Fact]
        public void AllocateInOrder()
        {
            for (int i = 1; i <= CHANNEL_COUNT; i++)
                Assert.Equal(i, ChannelNumber(_c.CreateChannel()));
        }

        [Fact]
        public void AllocateAfterFreeingLast()
        {
            IChannel ch = _c.CreateChannel();
            Assert.Equal(1, ChannelNumber(ch));
            ch.Close();
            ch = _c.CreateChannel();
            Assert.Equal(1, ChannelNumber(ch));
        }

        [Fact]
        public async Task AllocateAfterFreeingLastAsync()
        {
            IChannel ch = _c.CreateChannel();
            Assert.Equal(1, ChannelNumber(ch));
            await ch.CloseAsync();
            ch = _c.CreateChannel();
            Assert.Equal(1, ChannelNumber(ch));
        }

        public int CompareChannels(IChannel x, IChannel y)
        {
            int i = ChannelNumber(x);
            int j = ChannelNumber(y);
            return (i < j) ? -1 : (i == j) ? 0 : 1;
        }

        [Fact]
        public void AllocateAfterFreeingMany()
        {
            List<IChannel> channels = new List<IChannel>();

            for (int i = 1; i <= CHANNEL_COUNT; i++)
                channels.Add(_c.CreateChannel());

            foreach (IChannel channel in channels)
            {
                channel.Close();
            }

            channels = new List<IChannel>();

            for (int j = 1; j <= CHANNEL_COUNT; j++)
                channels.Add(_c.CreateChannel());

            // In the current implementation the list should actually
            // already be sorted, but we don't want to force that behaviour
            channels.Sort(CompareChannels);

            int k = 1;
            foreach (IChannel channel in channels)
                Assert.Equal(k++, ChannelNumber(channel));
        }
    }
}
