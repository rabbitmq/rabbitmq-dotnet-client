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
using System.Buffers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.ConsumerDispatching;
using Xunit;
using Xunit.Abstractions;

namespace Test.SequentialIntegration
{
    /// <summary>
    /// These assert on ArrayPool&lt;byte&gt;.Shared, which is process-wide, so they live here rather
    /// than in Integration: a test running in parallel - or even another test in this class that churns
    /// the pool - could rent from the same bucket and make the
    /// instance check meaningless. See issue #2039.
    /// </summary>
    public class TestDroppedDeliveryReturnsItsBody : SequentialIntegrationFixture
    {
        public TestDroppedDeliveryReturnsItsBody(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task QuiescedDispatcherReturnsTheBodyToThePool_GH2039()
        {
            await AssertBodyIsReturned(dispatcher => dispatcher.Quiesce());
        }

        [Fact]
        public async Task CancelledTokenReturnsTheBodyToThePool_GH2039()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => AssertBodyIsReturned(_ => { }, cts.Token));
        }

        [Fact]
        public async Task FailedWriteReturnsTheBodyExactlyOnce_GH2039()
        {
            /*
             * The remaining exit: the guard is passed and the write itself fails. The race cannot be
             * scheduled, but its state can - complete the writer directly, leaving _disposed false.
             * Asserting the array does not come back twice is what catches a double return, which is
             * worse than not returning it: RentedMemory's Dispose clears only the copy it is called
             * on, so disposing both the parameter and the work item's copy would return one array to
             * two owners. The call must also not throw: ChannelClosedException here would unwind
             * into the frame-receive loop and tear down the whole connection (#1988).
             */
            const int Size = 8192;
            using var dispatcher = new AsyncConsumerDispatcher(null, 1);

            object writer = typeof(ConsumerDispatcherChannelBase)
                .GetField("_writer", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(dispatcher);
            Assert.True((bool)writer.GetType().GetMethod("TryComplete").Invoke(writer, new object[] { null }),
                "could not complete the work channel, so this test never reaches the state it is about");
            Assert.False((bool)typeof(ConsumerDispatcherChannelBase)
                .GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(dispatcher),
                "the dispatcher reports disposed, so the write under test is never reached");

            byte[] rented = ArrayPool<byte>.Shared.Rent(Size);
            Assert.NotSame(rented, ArrayPool<byte>.Shared.Rent(Size));

            await dispatcher.HandleBasicDeliverAsync("ctag", 1, false, "exchange", "key",
                new BasicProperties(), new RentedMemory(rented), CancellationToken.None);

            byte[] first = ArrayPool<byte>.Shared.Rent(Size);
            byte[] second = ArrayPool<byte>.Shared.Rent(Size);
            Assert.Same(rented, first);
            Assert.NotSame(rented, second);
            ArrayPool<byte>.Shared.Return(first);
            ArrayPool<byte>.Shared.Return(second);
        }

        /// <summary>
        /// Rents a distinctive buffer, drives a delivery that the dispatcher must drop, and checks the
        /// same array instance comes back out of the pool. The pre-check makes the test non-vacuous:
        /// if the array were already back in the pool before the delivery, a broken implementation
        /// would pass.
        /// </summary>
        private async Task AssertBodyIsReturned(Action<IConsumerDispatcher> arrange,
            CancellationToken cancellationToken = default)
        {
            const int Size = 8192;
            using var dispatcher = new AsyncConsumerDispatcher(null, 1);
            arrange(dispatcher);

            byte[] rented = ArrayPool<byte>.Shared.Rent(Size);
            Assert.NotSame(rented, ArrayPool<byte>.Shared.Rent(Size));

            try
            {
                await dispatcher.HandleBasicDeliverAsync("ctag", 1, false, "exchange", "key",
                    new BasicProperties(), new RentedMemory(rented), cancellationToken);
            }
            finally
            {
                byte[] again = ArrayPool<byte>.Shared.Rent(Size);
                Assert.Same(rented, again);
                ArrayPool<byte>.Shared.Return(again);
            }
        }
    }
}
