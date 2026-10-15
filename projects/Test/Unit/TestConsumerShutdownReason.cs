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
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.ConsumerDispatching;
using RabbitMQ.Client.Events;
using Xunit;

namespace Test.Unit
{
    // The shutdown-reason state transitions, with no broker. #2006,
    // docs/internal/connection-shutdown-and-cancellation.md. The broker-backed counterpart is
    // TestConsumerRecovery.TestConsumerShutdownReasonIsClearedAfterRecovery_GH2006.
    public class TestConsumerShutdownReason
    {
        // The constructor only assigns Channel, which none of these tests read.
        private static ShutdownEventArgs Reason() =>
            new ShutdownEventArgs(ShutdownInitiator.Peer, Constants.ConnectionForced, "test");

        [Fact]
        public async Task RegistrationClearsAShutdownReason_GH2006()
        {
            var consumer = new AsyncDefaultBasicConsumer(channel: null);

            await consumer.HandleBasicConsumeOkAsync("tag", CancellationToken.None);
            Assert.True(consumer.IsRunning);

            ShutdownEventArgs reason = Reason();
            await consumer.HandleChannelShutdownAsync(this, reason);
            Assert.Same(reason, consumer.ShutdownReason);
            Assert.False(consumer.IsRunning);

            await consumer.HandleBasicConsumeOkAsync("tag", CancellationToken.None);

            Assert.Null(consumer.ShutdownReason);
            Assert.True(consumer.IsRunning);
            Assert.Equal(new[] { "tag" }, consumer.ConsumerTags);
        }

        [Fact]
        public async Task RegistrationDuringShutdownDoesNotClearTheReason_GH2006()
        {
            // An already-cancelled token proves only that the guard is present; the reachable
            // interleaving it is meant to cover is #2016.
            var consumer = new AsyncDefaultBasicConsumer(channel: null);
            using var quiesced = new CancellationTokenSource();
            quiesced.Cancel();

            await consumer.HandleBasicConsumeOkAsync("tag", CancellationToken.None);
            ShutdownEventArgs reason = Reason();
            await consumer.HandleChannelShutdownAsync(this, reason);

            await consumer.HandleBasicConsumeOkAsync("tag", quiesced.Token);

            Assert.Same(reason, consumer.ShutdownReason);
            Assert.False(consumer.IsRunning,
                "a registration confirmed after the channel began shutting down must not report the " +
                "consumer as running; it never delivers.");
            Assert.Empty(consumer.ConsumerTags);
        }

        [Fact]
        public async Task ReasonSurvivesWhenNoRegistrationFollows_GH2006()
        {
            // A consumer that shut down and had no registration afterwards keeps the reason, which is
            // the signal that recovery did not restore it.
            var consumer = new AsyncDefaultBasicConsumer(channel: null);

            await consumer.HandleBasicConsumeOkAsync("tag", CancellationToken.None);
            ShutdownEventArgs reason = Reason();
            await consumer.HandleChannelShutdownAsync(this, reason);

            Assert.Same(reason, consumer.ShutdownReason);
            Assert.False(consumer.IsRunning);
        }

        [Fact]
        public async Task CancellationLeavesNoShutdownReason_GH2006()
        {
            // Cancellation is not shutdown and records no reason, so a health check reading only
            // ShutdownReason cannot see it. Keeps the XML remarks honest.
            var consumer = new AsyncDefaultBasicConsumer(channel: null);

            await consumer.HandleBasicConsumeOkAsync("tag", CancellationToken.None);
            await consumer.HandleBasicCancelAsync("tag");

            Assert.Null(consumer.ShutdownReason);
            Assert.False(consumer.IsRunning);
        }

        [Fact]
        public async Task TheTokenTheDispatcherHandsAConsumerIsCancelledByQuiesce_GH2006()
        {
            // The only test here that uses the token the dispatcher really passes rather than a
            // cancelled one of its own, so it is the only one that fails if the consume-ok work
            // item stops carrying the shutdown source or Quiesce() stops cancelling it. #2006.
            var consumer = new TokenCapturingConsumer();
            var dispatcher = new AsyncConsumerDispatcher(null, 1);
            try
            {
                await dispatcher.HandleBasicConsumeOkAsync(consumer, "tag", CancellationToken.None);

                Task dispatched = await Task.WhenAny(consumer.ConsumeOkReceived,
                    Task.Delay(TimeSpan.FromSeconds(10)));
                Assert.True(ReferenceEquals(dispatched, consumer.ConsumeOkReceived),
                    "the dispatcher never dispatched the consume-ok, so no token was captured and " +
                    "this test is vacuous");

                Assert.False(consumer.CapturedToken.IsCancellationRequested,
                    "the dispatcher handed out an already-cancelled token while the channel was " +
                    "healthy, so the guard would skip a legitimate reset");
                Assert.True(consumer.CapturedToken.CanBeCanceled,
                    "the dispatcher handed out CancellationToken.None, so the guard can never fire " +
                    "and a consumer registered during shutdown would clear its reason");

                dispatcher.Quiesce();

                Assert.True(consumer.CapturedToken.IsCancellationRequested,
                    "Quiesce() did not cancel the token the dispatcher hands to consumers, so the " +
                    "guard in HandleBasicConsumeOkAsync can never observe a shutdown");
            }
            finally
            {
                await dispatcher.ShutdownAsync(new ShutdownEventArgs(ShutdownInitiator.Library, 0, "test over"));
                dispatcher.Dispose();
            }
        }

        private sealed class TokenCapturingConsumer : IAsyncBasicConsumer
        {
            private readonly TaskCompletionSource<bool> _consumeOk =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task ConsumeOkReceived => _consumeOk.Task;

            public CancellationToken CapturedToken { get; private set; }

            public IChannel Channel => null;

            public Task HandleBasicCancelAsync(string consumerTag, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task HandleBasicCancelOkAsync(string consumerTag, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task HandleBasicConsumeOkAsync(string consumerTag, CancellationToken cancellationToken = default)
            {
                CapturedToken = cancellationToken;
                _consumeOk.TrySetResult(true);
                return Task.CompletedTask;
            }

            public Task HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered,
                string exchange, string routingKey, IReadOnlyBasicProperties properties,
                ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason)
                => Task.CompletedTask;
        }
    }
}
