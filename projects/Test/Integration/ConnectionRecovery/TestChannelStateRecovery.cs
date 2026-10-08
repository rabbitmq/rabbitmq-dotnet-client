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
    /// <summary>
    /// rabbitmq/rabbitmq-dotnet-client#2031
    ///
    /// Recovery replaces the inner channel, and <c>Channel.TakeOver</c> carried only the event
    /// wrappers. The two settable members of <see cref="IChannel"/> were therefore discarded on every
    /// recovery: <see cref="IChannel.ContinuationTimeout"/> silently reverted to whatever the
    /// connection config said, and <see cref="IChannel.DefaultConsumer"/> to the internal fallback,
    /// which logs an unmatched delivery and drops it.
    /// <para>
    /// <see cref="IChannel.CurrentQueue"/> is a third piece of channel state recovery drops, and is
    /// deliberately not fixed here - see docs/internal/channel-state-across-recovery.md.
    /// </para>
    /// </summary>
    public class TestChannelStateRecovery : TestConnectionRecoveryBase
    {
        public TestChannelStateRecovery(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestContinuationTimeoutSurvivesRecovery_GH2031()
        {
            // Deliberately unlike the connection config's value, so reverting is visible.
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
            /*
             * An identity assertion, not a delivery assertion: producing a genuinely unmatched
             * consumer tag against a live broker is awkward, so this pins that the reference
             * survives rather than that the dispatcher consults it. The stand-in has no
             * ReceivedAsync handler, so it is only a reference, not an example to copy.
             */
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
            // A channel that never touched the property still comes back with the value it was
            // created with, which is the connection config's at the time it was created.
            TimeSpan before = _channel.ContinuationTimeout;

            await CloseAndWaitForRecoveryAsync();

            Assert.True(_channel.IsOpen);
            Assert.Equal(before, _channel.ContinuationTimeout);
        }

        [Fact]
        public async Task TestRecoveryDoesNotAdoptAnotherConnectionsTimeout_GH2031()
        {
            /*
             * This is the test that earns the design. CreateChannelOptions.CreateOrUpdate mutates
             * the caller's own instance in place, on every CreateChannelAsync, so one options object
             * shared across connections carries the last writer's connection config. Replaying only
             * an explicit assignment - the obvious conservative choice - leaves an untouched channel
             * reading that shared object at recovery time and silently adopting a different
             * connection's timeout. Capturing in the constructor is what makes this pass.
             */
            var sharedOptions = new CreateChannelOptions(publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false);

            ConnectionFactory firstFactory = CreateConnectionFactory();
            firstFactory.AutomaticRecoveryEnabled = true;
            firstFactory.ContinuationTimeout = TimeSpan.FromSeconds(31);

            await using IConnection firstConn = await firstFactory.CreateConnectionAsync();
            await using IChannel firstChannel = await firstConn.CreateChannelAsync(sharedOptions);
            TimeSpan expected = firstChannel.ContinuationTimeout;
            Assert.Equal(TimeSpan.FromSeconds(31), expected);

            // A second connection reuses the same options instance, rewriting it in place.
            ConnectionFactory secondFactory = CreateConnectionFactory();
            secondFactory.ContinuationTimeout = TimeSpan.FromSeconds(7);
            await using IConnection secondConn = await secondFactory.CreateConnectionAsync();
            await using IChannel secondChannel = await secondConn.CreateChannelAsync(sharedOptions);
            Assert.Equal(TimeSpan.FromSeconds(7), secondChannel.ContinuationTimeout);

            // The first channel is untouched until it recovers.
            Assert.Equal(expected, firstChannel.ContinuationTimeout);

            await CloseAndWaitForRecoveryAsync((AutorecoveringConnection)firstConn);

            Assert.True(firstChannel.IsOpen);
            Assert.Equal(expected, firstChannel.ContinuationTimeout);
        }
    }
}
