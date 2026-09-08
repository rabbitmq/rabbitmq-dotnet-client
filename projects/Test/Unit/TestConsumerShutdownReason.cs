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

        [Fact]
        public async Task TheTokenTheDispatcherHandsAConsumerIsCancelledByQuiesce_GH2006()
        {
            /*
             * The guard above is load-bearing only because of wiring none of those tests touch:
             * the token in a consume-ok work item is the dispatcher's shutdown source, and Quiesce()
             * cancels that source. Each of those is one line in ConsumerDispatcherChannelBase, and
             * with both mutated away - hand CreateConsumeOk a fresh source, or drop the Cancel from
             * Quiesce - the guard becomes dead code in production while every other test here stays
             * green, because they all supply a cancelled token of their own making.
             *
             * Capture the token the dispatcher really passes, then cancel via Quiesce and observe
             * the captured copy. A CancellationToken is a struct referring to its source, so the
             * copy the consumer received reports the cancellation - which is exactly the mechanism
             * the guard depends on at dispatch time. No broker: a consume-ok work item touches only
             * the consumer, so a null channel is fine.
             */
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
