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
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Impl;
using Xunit;

namespace Test.Unit
{
    public class TestRpcContinuationQueue
    {
        private class TestSimpleAsyncRpcContinuation : SimpleAsyncRpcContinuation
        {
            public TestSimpleAsyncRpcContinuation()
                : base(ProtocolCommandId.BasicGet, TimeSpan.FromSeconds(10), CancellationToken.None)
            {
            }
        }

        [Fact]
        public void TestRpcContinuationQueueEnqueueAndRelease()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            var inputContinuation = new TestSimpleAsyncRpcContinuation();
            queue.Enqueue(inputContinuation);
            IRpcContinuation outputContinuation = queue.Next();
            Assert.Equal(outputContinuation, inputContinuation);
        }

        [Fact]
        public void TestRpcContinuationQueueEnqueueAndRelease2()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            var inputContinuation = new TestSimpleAsyncRpcContinuation();
            queue.Enqueue(inputContinuation);
            IRpcContinuation outputContinuation = queue.Next();
            Assert.Equal(outputContinuation, inputContinuation);
            IRpcContinuation outputContinuation1 = queue.Next();
            Assert.NotEqual(outputContinuation1, inputContinuation);
        }

        [Fact]
        public void TestRpcContinuationQueueEnqueue2()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            var inputContinuation = new TestSimpleAsyncRpcContinuation();
            var inputContinuation1 = new TestSimpleAsyncRpcContinuation();
            queue.Enqueue(inputContinuation);
            Assert.Throws<NotSupportedException>(() =>
            {
                queue.Enqueue(inputContinuation1);
            });
        }

        [Fact]
        public void TestShouldIgnoreCommand_NoTimedOutCommands_ReturnsFalse()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            Assert.False(queue.ShouldIgnoreCommand(ProtocolCommandId.BasicAck));
        }

        [Fact]
        public void TestShouldIgnoreCommand_SingleTimedOutCommand_MatchesFirst()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            queue.RpcCanceled(false, [ProtocolCommandId.QueueDeclareOk]);
            Assert.True(queue.ShouldIgnoreCommand(ProtocolCommandId.QueueDeclareOk));
        }

        [Fact]
        public void TestShouldIgnoreCommand_SingleTimedOutCommand_NoMatch()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            queue.RpcCanceled(false, [ProtocolCommandId.QueueDeclareOk]);
            // Another RPC response, so the mismatch path is what is exercised here
            Assert.False(queue.ShouldIgnoreCommand(ProtocolCommandId.ExchangeDeclareOk));
        }

        [Fact]
        public void TestShouldIgnoreCommand_TwoTimedOutCommands_MatchesFirst()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            queue.RpcCanceled(false, [ProtocolCommandId.BasicGetOk, ProtocolCommandId.BasicGetEmpty]);
            Assert.True(queue.ShouldIgnoreCommand(ProtocolCommandId.BasicGetOk));
        }

        [Fact]
        public void TestShouldIgnoreCommand_TwoTimedOutCommands_MatchesSecond()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            queue.RpcCanceled(false, [ProtocolCommandId.BasicGetOk, ProtocolCommandId.BasicGetEmpty]);
            Assert.True(queue.ShouldIgnoreCommand(ProtocolCommandId.BasicGetEmpty));
        }

        [Fact]
        public void TestShouldIgnoreCommand_TwoTimedOutCommands_NoMatch()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            queue.RpcCanceled(false, [ProtocolCommandId.BasicGetOk, ProtocolCommandId.BasicGetEmpty]);
            // Another RPC response, so the mismatch path is what is exercised here
            Assert.False(queue.ShouldIgnoreCommand(ProtocolCommandId.ExchangeDeclareOk));
        }

        [Fact]
        public void TestShouldIgnoreCommand_ConsumesTimedOutState()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            queue.RpcCanceled(false, [ProtocolCommandId.QueueDeclareOk]);
            Assert.True(queue.ShouldIgnoreCommand(ProtocolCommandId.QueueDeclareOk));
            // Second call should return false — the timed-out state was consumed
            Assert.False(queue.ShouldIgnoreCommand(ProtocolCommandId.QueueDeclareOk));
        }

        [Fact]
        public void TestShouldIgnoreCommand_EmptyAfterConsume()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            queue.RpcCanceled(false, [ProtocolCommandId.QueueDeclareOk]);
            // Consume with a non-matching command ID. It must be another RPC response:
            // a server-originated frame deliberately does not consume an entry (#1964).
            Assert.False(queue.ShouldIgnoreCommand(ProtocolCommandId.ExchangeDeclareOk));
            // Now the timed-out state is consumed, any check returns false
            Assert.False(queue.ShouldIgnoreCommand(ProtocolCommandId.QueueDeclareOk));
        }

        [Fact]
        public void TestRpcCanceled_ResponseReceivedTrue_DoesNotRecord()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            queue.RpcCanceled(true, [ProtocolCommandId.QueueDeclareOk]);
            // Since responseReceived=true, no command IDs should be recorded
            Assert.False(queue.ShouldIgnoreCommand(ProtocolCommandId.QueueDeclareOk));
        }

        // rabbitmq/rabbitmq-dotnet-client#1964
        // Consecutive timeouts each leave a late response in flight. A single-slot
        // record kept only the newest, so the older responses were matched against
        // an unrelated continuation ("Received unexpected command of type ...!").
        [Fact]
        public void TestShouldIgnoreCommand_ConsecutiveTimeouts_IgnoresEveryLateResponse()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();

            queue.RpcCanceled(false, [ProtocolCommandId.ExchangeDeclareOk]);
            queue.RpcCanceled(false, [ProtocolCommandId.QueueDeclareOk]);
            queue.RpcCanceled(false, [ProtocolCommandId.BasicGetOk, ProtocolCommandId.BasicGetEmpty]);

            // Late responses arrive in the order the requests were sent
            Assert.True(queue.ShouldIgnoreCommand(ProtocolCommandId.ExchangeDeclareOk));
            Assert.True(queue.ShouldIgnoreCommand(ProtocolCommandId.QueueDeclareOk));
            Assert.True(queue.ShouldIgnoreCommand(ProtocolCommandId.BasicGetEmpty));

            // All three have now been absorbed
            Assert.False(queue.ShouldIgnoreCommand(ProtocolCommandId.ExchangeDeclareOk));
        }

        [Fact]
        public void TestShouldIgnoreCommand_ConsecutiveTimeouts_SameCommandId()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();

            queue.RpcCanceled(false, [ProtocolCommandId.ExchangeDeclareOk]);
            queue.RpcCanceled(false, [ProtocolCommandId.ExchangeDeclareOk]);

            Assert.True(queue.ShouldIgnoreCommand(ProtocolCommandId.ExchangeDeclareOk));
            Assert.True(queue.ShouldIgnoreCommand(ProtocolCommandId.ExchangeDeclareOk));
            Assert.False(queue.ShouldIgnoreCommand(ProtocolCommandId.ExchangeDeclareOk));
        }

        // rabbitmq/rabbitmq-dotnet-client#1964
        // Server-originated frames interleave freely with the request-response stream.
        // Consuming an entry for one of them discarded the record of a timed-out RPC,
        // so its late response then reached an unrelated continuation.
        [Theory]
        [InlineData(ProtocolCommandId.BasicDeliver)]
        [InlineData(ProtocolCommandId.BasicAck)]
        [InlineData(ProtocolCommandId.BasicNack)]
        [InlineData(ProtocolCommandId.BasicReturn)]
        [InlineData(ProtocolCommandId.BasicCancel)]
        [InlineData(ProtocolCommandId.ChannelFlow)]
        [InlineData(ProtocolCommandId.ConnectionBlocked)]
        [InlineData(ProtocolCommandId.ConnectionUnblocked)]
        // Note: internal because ProtocolCommandId is internal; xUnit discovers it either way
        internal void TestShouldIgnoreCommand_ServerOriginatedFrameDoesNotConsumeEntry(
            ProtocolCommandId serverOriginated)
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            queue.RpcCanceled(false, [ProtocolCommandId.QueueDeclareOk]);

            Assert.False(queue.ShouldIgnoreCommand(serverOriginated));

            // The timed-out RPC's record must survive, otherwise its late response is
            // matched against whichever continuation is outstanding by then
            Assert.True(queue.ShouldIgnoreCommand(ProtocolCommandId.QueueDeclareOk));
        }

        [Fact]
        public void TestShouldIgnoreCommand_ChannelCloseOkStillAbsorbed()
        {
            // ChannelCloseOk is dispatched by Channel, but it is a real RPC response and
            // so may arrive late like any other
            RpcContinuationQueue queue = new RpcContinuationQueue();
            queue.RpcCanceled(false, [ProtocolCommandId.ChannelCloseOk]);
            Assert.True(queue.ShouldIgnoreCommand(ProtocolCommandId.ChannelCloseOk));
        }

        [Fact]
        public void TestShouldIgnoreCommand_ConnectionSecureAndTuneStillAbsorbed()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();
            queue.RpcCanceled(false, [ProtocolCommandId.ConnectionSecure, ProtocolCommandId.ConnectionTune]);
            Assert.True(queue.ShouldIgnoreCommand(ProtocolCommandId.ConnectionTune));
        }

        // rabbitmq/rabbitmq-dotnet-client#1964
        // Entries are only removed by an inbound command or by channel shutdown, so a
        // channel that receives no frames at all would grow this record without limit
        // while RPCs keep timing out. The record is capped, dropping the oldest entries.
        [Fact]
        public void TestRpcCanceled_IsBounded()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();

            const int recorded = 500;
            for (int i = 0; i < recorded; i++)
            {
                queue.RpcCanceled(false, [ProtocolCommandId.ExchangeDeclareOk]);
            }

            // Drain by feeding matching responses until the record is empty. An unbounded
            // record would absorb all 500.
            int absorbed = 0;
            while (queue.ShouldIgnoreCommand(ProtocolCommandId.ExchangeDeclareOk))
            {
                absorbed++;
                Assert.True(absorbed <= recorded,
                    "draining did not terminate, so the record is not bounded");
            }

            Assert.True(absorbed < recorded,
                $"expected the record to be capped, but it absorbed all {absorbed} entries");
        }

        [Fact]
        public void TestRpcCanceled_BoundDropsTheOldestEntries()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();

            // One distinguishable entry first, so it is the oldest and therefore the first
            // to be given up on once the bound is exceeded.
            queue.RpcCanceled(false, [ProtocolCommandId.QueueDeclareOk]);

            for (int i = 0; i < 500; i++)
            {
                queue.RpcCanceled(false, [ProtocolCommandId.ExchangeDeclareOk]);
            }

            // If the oldest entry had survived it would still be at the head, and matching
            // ExchangeDeclareOk against it would consume it and return false. Returning
            // true proves it was dropped rather than something newer.
            Assert.True(queue.ShouldIgnoreCommand(ProtocolCommandId.ExchangeDeclareOk));
        }

        [Fact]
        public void TestHandleChannelShutdown_DiscardsPendingTimedOutCommands()
        {
            RpcContinuationQueue queue = new RpcContinuationQueue();

            queue.RpcCanceled(false, [ProtocolCommandId.ExchangeDeclareOk]);
            queue.RpcCanceled(false, [ProtocolCommandId.QueueDeclareOk]);

            queue.HandleChannelShutdown(new ShutdownEventArgs(ShutdownInitiator.Library,
                Constants.ReplySuccess, "test shutdown"));

            // No further frames arrive on a shut down channel, so the recorded entries can
            // never be consumed and are released instead. Probe the newest entry first:
            // probing the oldest would consume the sole slot of a single-entry
            // implementation and mask a missing drain.
            Assert.False(queue.ShouldIgnoreCommand(ProtocolCommandId.QueueDeclareOk));
            Assert.False(queue.ShouldIgnoreCommand(ProtocolCommandId.ExchangeDeclareOk));
        }
    }
}
