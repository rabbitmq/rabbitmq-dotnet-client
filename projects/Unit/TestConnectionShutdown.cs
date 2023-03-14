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
using System.Threading;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;
using Xunit;
using Xunit.Abstractions;

namespace RabbitMQ.Client.Unit
{

    public class TestConnectionShutdown : IntegrationFixture
    {
        public TestConnectionShutdown(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void TestCleanClosureWithSocketClosedOutOfBand()
        {
            _conn = CreateAutorecoveringConnection();
            _channel = _conn.CreateChannel();

            var latch = new ManualResetEventSlim(false);
            _channel.ChannelShutdown += (channel, args) =>
            {
                latch.Set();
            };

            var c = (AutorecoveringConnection)_conn;
            c.FrameHandler.Close();

            _conn.Close(TimeSpan.FromSeconds(4));
            Wait(latch, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void TestAbortWithSocketClosedOutOfBand()
        {
            _conn = CreateAutorecoveringConnection();
            _channel = _conn.CreateChannel();

            var latch = new ManualResetEventSlim(false);
            _channel.ChannelShutdown += (channel, args) =>
            {
                latch.Set();
            };

            var c = (AutorecoveringConnection)_conn;
            c.FrameHandler.Close();

            _conn.Abort();
            // default Connection.Abort() timeout and then some
            Wait(latch, TimeSpan.FromSeconds(6));
        }

        [Fact]
        public void TestDisposedWithSocketClosedOutOfBand()
        {
            _conn = CreateAutorecoveringConnection();
            _channel = _conn.CreateChannel();

            var latch = new ManualResetEventSlim(false);
            _channel.ChannelShutdown += (channel, args) =>
            {
                latch.Set();
            };

            var c = (AutorecoveringConnection)_conn;
            c.FrameHandler.Close();

            _conn.Dispose();
            Wait(latch, TimeSpan.FromSeconds(3));
        }

        [Fact]
        public void TestShutdownSignalPropagationToChannels()
        {
            var latch = new ManualResetEventSlim(false);

            _channel.ChannelShutdown += (channel, args) =>
            {
                latch.Set();
            };
            _conn.Close();

            Wait(latch, TimeSpan.FromSeconds(3));
        }

        [Fact]
        public void TestConsumerDispatcherShutdown()
        {
            var m = (AutorecoveringChannel)_channel;
            var latch = new ManualResetEventSlim(false);

            _channel.ChannelShutdown += (channel, args) =>
            {
                latch.Set();
            };
            Assert.False(m.ConsumerDispatcher.IsShutdown, "dispatcher should NOT be shut down before Close");
            _conn.Close();
            Wait(latch, TimeSpan.FromSeconds(3));
            Assert.True(m.ConsumerDispatcher.IsShutdown, "dispatcher should be shut down after Close");
        }
    }
}
