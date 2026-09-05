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

using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

namespace Test.Unit
{
    /// <summary>
    /// rabbitmq/rabbitmq-dotnet-client#2006
    ///
    /// <see cref="AsyncDefaultBasicConsumer.ShutdownReason"/> documents itself as null unless the
    /// channel has shut down, but nothing cleared it, so after a recovered connection drop a
    /// consumer served deliveries again while still reporting the shutdown that triggered the
    /// recovery. The registration callback clears it.
    ///
    /// These are the state transitions themselves, which need no broker and no dispatcher: the
    /// constructor only stores the channel. The broker-backed counterpart, which proves recovery
    /// actually drives this path, is
    /// <c>TestConsumerRecovery.TestConsumerShutdownReasonIsClearedAfterRecovery_GH2006</c>.
    /// </summary>
    public class TestConsumerShutdownReason
    {
        // The constructor only assigns Channel, which none of these tests read.
        private static ShutdownEventArgs Reason() =>
            new ShutdownEventArgs(ShutdownInitiator.Peer, Constants.ConnectionForced, "test");

        [Fact]
        public async Task RegistrationClearsAShutdownReason_GH2006()
        {
            var consumer = new AsyncDefaultBasicConsumer(channel: null);

            await consumer.HandleChannelShutdownAsync(this, Reason());
            Assert.NotNull(consumer.ShutdownReason);
            Assert.False(consumer.IsRunning);

            await consumer.HandleBasicConsumeOkAsync("tag", CancellationToken.None);

            Assert.Null(consumer.ShutdownReason);
            Assert.True(consumer.IsRunning);
        }

        [Fact]
        public async Task RegistrationDuringShutdownDoesNotClearTheReason_GH2006()
        {
            /*
             * The token is the dispatcher's shutdown token, cancelled by Quiesce(). A consume-ok
             * processed after the shutdown work item must not clear the reason: the channel is
             * permanently dead, and a null reason together with IsRunning true reads as fully
             * healthy, which is worse than the stale reason this fix set out to remove. Before the
             * guard the reset was unconditional and this case reported healthy.
             */
            var consumer = new AsyncDefaultBasicConsumer(channel: null);
            using var quiesced = new CancellationTokenSource();
            quiesced.Cancel();

            await consumer.HandleChannelShutdownAsync(this, Reason());
            ShutdownEventArgs reason = consumer.ShutdownReason;
            Assert.NotNull(reason);

            await consumer.HandleBasicConsumeOkAsync("tag", quiesced.Token);

            Assert.Same(reason, consumer.ShutdownReason);
        }

        [Fact]
        public async Task ReasonSurvivesWhenNoRegistrationFollows_GH2006()
        {
            // A consumer that recovery never re-registered, for whatever reason, keeps the reason.
            // That is the signal that this consumer was not restored.
            var consumer = new AsyncDefaultBasicConsumer(channel: null);

            await consumer.HandleBasicConsumeOkAsync("tag", CancellationToken.None);
            await consumer.HandleChannelShutdownAsync(this, Reason());

            Assert.NotNull(consumer.ShutdownReason);
            Assert.False(consumer.IsRunning);
        }
    }
}
