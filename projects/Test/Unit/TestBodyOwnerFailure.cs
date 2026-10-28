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
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Impl;
using Xunit;

namespace Test.Unit
{
    public class TestBodyOwnerFailure
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ClosedSessionPreservesItsExceptionWhenOwnerThrows_GH2108(bool sequence)
        {
            // Session 0 needs no connection subscription, and this guard needs no shutdown notification.
            var session = new Session(null!, 0, uint.MaxValue);
            await session.CloseAsync(new ShutdownEventArgs(ShutdownInitiator.Application, 200, "test"), notify: false);
            var owner = new ThrowingOwner();
            var method = new BasicPublish("", "q", false, false);
            var properties = new BasicProperties();
            var body = new ReadOnlyMemory<byte>(new byte[8]);

            Assert.Throws<AlreadyClosedException>(() =>
            {
                if (sequence)
                {
                    session.TransmitAsync(method, properties, new ReadOnlySequence<byte>(body), owner);
                }
                else
                {
                    session.TransmitAsync(method, properties, body, owner);
                }
            });
            Assert.Equal(1, owner.DisposeCount);
        }

        [Fact]
        public async Task OversizedBodyPreservesValidationExceptionWhenOwnerThrows_GH2108()
        {
            using var session = new TestSession();
            var channel = new RecoveryAwareChannel(session, new CreateChannelOptions(false, false));
            var owner = new ThrowingOwner();
            try
            {
                ReadOnlySequence<byte> body = ReadOnlySequenceFactory.CreateUnbacked(3);
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    channel.BasicPublishAsync("", "q", false, new BasicProperties(), body, owner));
                Assert.Equal(1, owner.DisposeCount);
            }
            finally
            {
                await session.CloseAsync(new ShutdownEventArgs(ShutdownInitiator.Application, 200, "test"));
                await channel.DisposeAsync();
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CancelledPublishPreservesCancellationWhenOwnerThrows_GH2108(bool sequence)
        {
            var options = new TestSessionOptions();
            options.Replies[ProtocolCommandId.ChannelOpen] = ProtocolCommandId.ChannelOpenOk;
            options.Replies[ProtocolCommandId.ConfirmSelect] = ProtocolCommandId.ConfirmSelectOk;
            using var session = new TestSession(options);
            var channelOptions = new CreateChannelOptions(true, true);
            var channel = new RecoveryAwareChannel(session, channelOptions) { ContinuationTimeout = TimeSpan.FromSeconds(10) };
            var owner = new ThrowingOwner();
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            try
            {
                await channel.OpenAsync(channelOptions, CancellationToken.None);
                var body = new ReadOnlyMemory<byte>(new byte[8]);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => (sequence
                    ? channel.BasicPublishAsync("", "q", false, new BasicProperties(), new ReadOnlySequence<byte>(body), owner, cts.Token)
                    : channel.BasicPublishAsync("", "q", false, new BasicProperties(), body, owner, cts.Token)).AsTask());
                Assert.Equal(1, owner.DisposeCount);
            }
            finally
            {
                await session.CloseAsync(new ShutdownEventArgs(ShutdownInitiator.Application, 200, "test"));
                await channel.DisposeAsync();
            }
        }

        private sealed class ThrowingOwner : IDisposable
        {
            public int DisposeCount { get; private set; }

            public void Dispose()
            {
                DisposeCount++;
                throw new InvalidOperationException("owner bug");
            }
        }
    }
}
