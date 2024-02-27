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

using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using Xunit;

namespace Test.Integration
{
    public class TestChannelAllocation : IAsyncLifetime
    {
        private IConnection _c;
        private const int CHANNEL_COUNT = 100;

        public async Task InitializeAsync()
        {
            var cf = new ConnectionFactory
            {
                ContinuationTimeout = IntegrationFixture.WaitSpan,
                HandshakeContinuationTimeout = IntegrationFixture.WaitSpan,
                ClientProvidedName = nameof(TestChannelAllocation)
            };

            _c = await cf.CreateConnectionAsync();
        }

        public async Task DisposeAsync()
        {
            await _c.CloseAsync();
        }

        [Fact]
        public async Task AllocateInOrder()
        {
            var channels = new List<IChannel>();
            for (int i = 1; i <= CHANNEL_COUNT; i++)
            {
                IChannel channel = await _c.CreateChannelAsync();
                channels.Add(channel);
                Assert.Equal(i, ChannelNumber(channel));
            }

            foreach (IChannel channel in channels)
            {
                await channel.CloseAsync();
                channel.Dispose();
            }
        }

        [Fact]
        public async Task AllocateInOrderOnlyUsingDispose()
        {
            var channels = new List<IChannel>();
            for (int i = 1; i <= CHANNEL_COUNT; i++)
            {
                IChannel channel = await _c.CreateChannelAsync();
                channels.Add(channel);
                Assert.Equal(i, ChannelNumber(channel));
            }

            foreach (IChannel channel in channels)
            {
                channel.Dispose();
            }
        }

        [Fact]
        public async Task AllocateAfterFreeingLast()
        {
            IChannel ch0 = await _c.CreateChannelAsync();
            Assert.Equal(1, ChannelNumber(ch0));
            await ch0.CloseAsync();
            ch0.Dispose();

            IChannel ch1 = await _c.CreateChannelAsync();
            Assert.Equal(1, ChannelNumber(ch1));
            await ch1.CloseAsync();
            ch1.Dispose();
        }

        [Fact]
        public async Task AllocateAfterFreeingMany()
        {
            var channels = new List<IChannel>();

            for (int i = 1; i <= CHANNEL_COUNT; i++)
            {
                channels.Add(await _c.CreateChannelAsync());
            }

            foreach (IChannel channel in channels)
            {
                await channel.CloseAsync();
            }

            channels.Clear();

            for (int j = 1; j <= CHANNEL_COUNT; j++)
            {
                channels.Add(await _c.CreateChannelAsync());
            }

            // In the current implementation the list should actually
            // already be sorted, but we don't want to force that behaviour
            channels.Sort(CompareChannels);

            int k = 1;
            foreach (IChannel channel in channels)
            {
                Assert.Equal(k++, ChannelNumber(channel));
                await channel.CloseAsync();
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
