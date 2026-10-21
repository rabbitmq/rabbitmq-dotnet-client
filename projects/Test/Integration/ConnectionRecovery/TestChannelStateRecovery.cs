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
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration.ConnectionRecovery
{
    // Recovery replaces the inner channel, which used to discard both settable members of
    // IChannel. What survives recovery and what still does not is in
    // docs/internal/channel-state-across-recovery.md. Issue #2031.
    public class TestChannelStateRecovery : TestConnectionRecoveryBase
    {
        public TestChannelStateRecovery(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestContinuationTimeoutSurvivesRecovery_GH2031()
        {
            TimeSpan assigned = TimeSpan.FromSeconds(77);
            Assert.NotEqual(assigned, _channel.ContinuationTimeout);

            _channel.ContinuationTimeout = assigned;
            Assert.Equal(assigned, _channel.ContinuationTimeout);

            await CloseAndWaitForRecoveryAsync();

            Assert.True(_channel.IsOpen);
            Assert.Equal(assigned, _channel.ContinuationTimeout);
        }

        [Fact]
        public async Task TestDefaultConsumerSurvivesRecovery_GH2031()
        {
            // Pins that the reference survives, not that the dispatcher consults it: an
            // unmatched consumer tag is awkward to produce against a live broker.
            var mine = new AsyncEventingBasicConsumer(_channel);
            _channel.DefaultConsumer = mine;
            Assert.Same(mine, _channel.DefaultConsumer);

            await CloseAndWaitForRecoveryAsync();

            Assert.True(_channel.IsOpen);
            Assert.Same(mine, _channel.DefaultConsumer);
        }

        [Fact]
        public async Task TestAnUnsetContinuationTimeoutIsUnchangedByRecovery_GH2031()
        {
            // An untouched channel comes back with the value it was created with.
            TimeSpan before = _channel.ContinuationTimeout;

            await CloseAndWaitForRecoveryAsync();

            Assert.True(_channel.IsOpen);
            Assert.Equal(before, _channel.ContinuationTimeout);
        }

        [Fact]
        public async Task TestRecoveryDoesNotAdoptAnotherConnectionsTimeout_GH2031()
        {
            // One CreateChannelOptions instance reused across two connections. The unconditional
            // replay alone satisfies this, so it does not discriminate #2034's fix; see the
            // test section of docs/internal/channel-state-across-recovery.md before trimming it.
            TimeSpan firstConnectionTimeout = TimeSpan.FromSeconds(31);
            TimeSpan secondConnectionTimeout = TimeSpan.FromSeconds(7);

            var sharedOptions = new CreateChannelOptions(publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false);

            ConnectionFactory firstFactory = CreateConnectionFactory();
            firstFactory.AutomaticRecoveryEnabled = true;
            firstFactory.ContinuationTimeout = firstConnectionTimeout;

            await using IConnection firstConn = await firstFactory.CreateConnectionAsync();
            await using IChannel firstChannel = await firstConn.CreateChannelAsync(sharedOptions);
            Assert.Equal(firstConnectionTimeout, firstChannel.ContinuationTimeout);

            ConnectionFactory secondFactory = CreateConnectionFactory();
            secondFactory.ContinuationTimeout = secondConnectionTimeout;
            await using IConnection secondConn = await secondFactory.CreateConnectionAsync();
            await using IChannel secondChannel = await secondConn.CreateChannelAsync(sharedOptions);
            Assert.Equal(secondConnectionTimeout, secondChannel.ContinuationTimeout);
            Assert.Equal(firstConnectionTimeout, firstChannel.ContinuationTimeout);

            await CloseAndWaitForRecoveryAsync((AutorecoveringConnection)firstConn);

            Assert.True(firstChannel.IsOpen);
            Assert.Equal(firstConnectionTimeout, firstChannel.ContinuationTimeout);
            Assert.NotEqual(secondConnectionTimeout, firstChannel.ContinuationTimeout);
        }

        [Fact]
        public async Task TestRecoveryDoesNotAdoptAnotherConnectionsConcurrency_GH2034()
        {
            // Dispatch concurrency is the deterministic form of #2034: nothing replays it, so a
            // value another connection wrote reached the rebuilt channel. #2034.
            // Neither value is Constants.DefaultConsumerDispatchConcurrency, the fixture's own
            // default, or the dispatcher's floor for zero, so losing the connection's value fails
            // this rather than landing back on 1.
            const ushort firstConcurrency = 3;
            const ushort secondConcurrency = 5;

            // null, not omitted: the constructor's own default of 1 would short-circuit the
            // resolution and the connection's value would never be consulted. #2027.
            var sharedOptions = new CreateChannelOptions(publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false, consumerDispatchConcurrency: null);

            ConnectionFactory firstFactory = CreateConnectionFactory();
            firstFactory.AutomaticRecoveryEnabled = true;
            firstFactory.ConsumerDispatchConcurrency = firstConcurrency;

            await using IConnection firstConn = await firstFactory.CreateConnectionAsync();
            await using IChannel firstChannel = await firstConn.CreateChannelAsync(sharedOptions);
            Assert.Equal(firstConcurrency, DispatchConcurrencyOf(firstChannel));

            ConnectionFactory secondFactory = CreateConnectionFactory();
            secondFactory.AutomaticRecoveryEnabled = true;
            secondFactory.ConsumerDispatchConcurrency = secondConcurrency;
            await using IConnection secondConn = await secondFactory.CreateConnectionAsync();
            await using IChannel secondChannel = await secondConn.CreateChannelAsync(sharedOptions);
            Assert.Equal(secondConcurrency, DispatchConcurrencyOf(secondChannel));

            await CloseAndWaitForRecoveryAsync((AutorecoveringConnection)firstConn);

            Assert.True(firstChannel.IsOpen);
            // The recovered inner channel, not the one asserted above: the wrapper forwards.
            Assert.Equal(firstConcurrency, DispatchConcurrencyOf(firstChannel));
        }

        private static ushort DispatchConcurrencyOf(IChannel channel)
        {
            // Asserted, not cast blind: without automatic recovery this is a plain Channel and the
            // cast would fail as if the guard had broken rather than as if it were not enforced.
            var recovering = Assert.IsType<AutorecoveringChannel>(channel);
            return recovering.ConsumerDispatcher.Concurrency;
        }
    }
}
