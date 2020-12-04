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
using NUnit.Framework;
using RabbitMQ.Client.client.impl.Channel;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestIChannelAllocation
    {
        public const int CHANNEL_COUNT = 100;

        private IConnection _c;

        [SetUp]
        public void Connect()
        {
            _c = new ConnectionFactory().CreateConnection();
        }

        [TearDown]
        public void Disconnect()
        {
            _c.Close();
        }

        [Test]
        public async Task AllocateInOrder()
        {
            for (int i = 1; i <= CHANNEL_COUNT; i++)
            {
                Assert.AreEqual(i, ChannelNumber(await _c.CreateChannelAsync().ConfigureAwait(false)));
            }
        }

        [Test]
        public async Task AllocateAfterFreeingLast()
        {
            IChannel ch = await _c.CreateChannelAsync().ConfigureAwait(false);
            Assert.AreEqual(1, ChannelNumber(ch));
            await ch.CloseAsync().ConfigureAwait(false);
            ch = await _c.CreateChannelAsync().ConfigureAwait(false);
            Assert.AreEqual(1, ChannelNumber(ch));
        }

        public int CompareChannels(IChannel x, IChannel y)
        {
            int i = ChannelNumber(x);
            int j = ChannelNumber(y);
            return (i < j) ? -1 : (i == j) ? 0 : 1;
        }

        [Test]
        public async Task AllocateAfterFreeingMany()
        {
            List<IChannel> channels = new List<IChannel>();

            for (int i = 1; i <= CHANNEL_COUNT; i++)
            {
                channels.Add(await _c.CreateChannelAsync().ConfigureAwait(false));
            }

            foreach (IChannel channel in channels)
            {
                await channel.CloseAsync().ConfigureAwait(false);
            }

            channels.Clear();

            for (int j = 1; j <= CHANNEL_COUNT; j++)
            {
                channels.Add(await _c.CreateChannelAsync().ConfigureAwait(false));
            }

            // In the current implementation the list should actually
            // already be sorted, but we don't want to force that behaviour
            channels.Sort(CompareChannels);

            int k = 1;
            foreach (IChannel channel in channels)
            {
                Assert.AreEqual(k++, ChannelNumber(channel));
            }
        }

        private static int ChannelNumber(IChannel channel)
        {
            return channel.ChannelNumber;
        }
    }
}
