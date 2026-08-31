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

#nullable enable

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Impl;
using Xunit;

using ImplChannel = RabbitMQ.Client.Impl.Channel;

namespace Test.Unit
{
    public class TestConnectionOpen
    {
        [Fact]
        public async Task TestConnectionOpenWaitsForConnectionOpenOkAsync()
        {
            using var session = new TestSession();
            var channel = CreateChannel(session);

            try
            {
                Task openTask = channel.ConnectionOpenAsync("/", CancellationToken.None).AsTask();

                Assert.Equal(ProtocolCommandId.ConnectionOpen,
                    await session.ReadTransmittedCommandAsync());
                Assert.False(openTask.IsCompleted);

                await session.DeliverCommandAsync(ProtocolCommandId.ConnectionOpenOk);
                await openTask.WaitAsync(TimingFixture.TestTimeout);
            }
            finally
            {
                await DisposeChannelAsync(session, channel);
            }
        }

        [Fact]
        public async Task TestConnectionOpenHandlesImmediateConnectionOpenOkAsync()
        {
            using var session = new TestSession(respondToConnectionOpen: true);
            var channel = CreateChannel(session);

            try
            {
                await channel.ConnectionOpenAsync("/", CancellationToken.None).AsTask()
                    .WaitAsync(TimingFixture.TestTimeout);

                Assert.Equal(ProtocolCommandId.ConnectionOpen,
                    await session.ReadTransmittedCommandAsync());
            }
            finally
            {
                await DisposeChannelAsync(session, channel);
            }
        }

        [Fact]
        public async Task TestConnectionOpenPropagatesSessionShutdownAsync()
        {
            using var session = new TestSession();
            var channel = CreateChannel(session);

            try
            {
                Task openTask = channel.ConnectionOpenAsync("/", CancellationToken.None).AsTask();
                Assert.Equal(ProtocolCommandId.ConnectionOpen,
                    await session.ReadTransmittedCommandAsync());

                var reason = new ShutdownEventArgs(ShutdownInitiator.Peer, 530,
                    "connection open rejected", classId: 20, methodId: 10);
                await session.CloseAsync(reason);

                OperationInterruptedException exception =
                    await Assert.ThrowsAsync<OperationInterruptedException>(() => openTask);
                Assert.Same(reason, exception.ShutdownReason);
            }
            finally
            {
                await DisposeChannelAsync(session, channel);
            }
        }

        [Fact]
        public async Task TestConnectionOpenTimeoutDoesNotCorruptNextRpcAsync()
        {
            using var session = new TestSession();
            var channel = CreateChannel(session);
            channel.HandshakeContinuationTimeout = TimingFixture.TimingInterval;

            try
            {
                Task firstOpenTask = channel.ConnectionOpenAsync("/", CancellationToken.None).AsTask();
                Assert.Equal(ProtocolCommandId.ConnectionOpen,
                    await session.ReadTransmittedCommandAsync());
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstOpenTask);

                await session.DeliverCommandAsync(ProtocolCommandId.ConnectionOpenOk);

                Task secondOpenTask = channel.ConnectionOpenAsync("/", CancellationToken.None).AsTask();
                Assert.Equal(ProtocolCommandId.ConnectionOpen,
                    await session.ReadTransmittedCommandAsync());
                Assert.False(secondOpenTask.IsCompleted);

                await session.DeliverCommandAsync(ProtocolCommandId.ConnectionOpenOk);
                await secondOpenTask.WaitAsync(TimingFixture.TestTimeout);
            }
            finally
            {
                await DisposeChannelAsync(session, channel);
            }
        }

        [Fact]
        public async Task TestConnectionOpenCanceledWhileWaitingForRpcSemaphoreDoesNotSendAsync()
        {
            using var session = new TestSession();
            TestChannel channel = CreateChannel(session);

            try
            {
                await channel.AcquireRpcSemaphoreAsync();
                try
                {
                    using var cts = new CancellationTokenSource();
                    cts.Cancel();

                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                        channel.ConnectionOpenAsync("/", cts.Token).AsTask());
                    Assert.Equal(0, session.TransmittedCommandCount);
                }
                finally
                {
                    // This also proves that the cancelled waiter did not release a permit it
                    // never acquired: doing so would make this Release throw SemaphoreFullException.
                    channel.ReleaseRpcSemaphore();
                }

                Task openTask = channel.ConnectionOpenAsync("/", CancellationToken.None).AsTask();
                Assert.Equal(ProtocolCommandId.ConnectionOpen,
                    await session.ReadTransmittedCommandAsync());
                await session.DeliverCommandAsync(ProtocolCommandId.ConnectionOpenOk);
                await openTask.WaitAsync(TimingFixture.TestTimeout);
            }
            finally
            {
                await DisposeChannelAsync(session, channel);
            }
        }

        private static TestChannel CreateChannel(TestSession session)
            => new TestChannel(session, new CreateChannelOptions(
                publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false));

        private static async Task DisposeChannelAsync(TestSession session, ImplChannel channel)
        {
            await session.CloseAsync(new ShutdownEventArgs(ShutdownInitiator.Library,
                Constants.ReplySuccess, "test teardown"));
            await channel.DisposeAsync();
        }

        private sealed class TestChannel : ImplChannel
        {
            public TestChannel(ISession session, CreateChannelOptions createChannelOptions)
                : base(session, createChannelOptions)
            {
            }

            public Task AcquireRpcSemaphoreAsync() => _rpcSemaphore.WaitAsync();

            public void ReleaseRpcSemaphore() => _rpcSemaphore.Release();
        }

        private sealed class TestSession : ISession, IDisposable
        {
            private readonly bool _respondToConnectionOpen;
            private readonly ConcurrentQueue<ProtocolCommandId> _transmittedCommands =
                new ConcurrentQueue<ProtocolCommandId>();
            private readonly SemaphoreSlim _transmittedCommandSignal = new SemaphoreSlim(0);
            private AsyncEventHandler<ShutdownEventArgs>? _sessionShutdownAsync;

            public TestSession(bool respondToConnectionOpen = false)
            {
                _respondToConnectionOpen = respondToConnectionOpen;
            }

            public ushort ChannelNumber => 0;

            public ShutdownEventArgs? CloseReason { get; private set; }

            public CommandReceivedAction? CommandReceived { get; set; }

            public Connection Connection => throw new NotSupportedException();

            public bool IsOpen => CloseReason is null;

            public int TransmittedCommandCount => _transmittedCommands.Count;

            public event AsyncEventHandler<ShutdownEventArgs> SessionShutdownAsync
            {
                add => _sessionShutdownAsync += value;
                remove => _sessionShutdownAsync -= value;
            }

            public Task CloseAsync(ShutdownEventArgs reason, bool notify = true)
            {
                if (CloseReason is not null)
                {
                    return Task.CompletedTask;
                }

                CloseReason = reason;
                return notify ? NotifySessionShutdownAsync(reason) : Task.CompletedTask;
            }

            public Task HandleFrameAsync(InboundFrame frame, CancellationToken cancellationToken)
                => throw new NotSupportedException();

            public Task NotifyAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return CloseReason is null
                    ? throw new InvalidOperationException("The session is still open.")
                    : NotifySessionShutdownAsync(CloseReason);
            }

            public ValueTask TransmitAsync<T>(in T cmd, CancellationToken cancellationToken)
                where T : struct, IOutgoingAmqpMethod
            {
                cancellationToken.ThrowIfCancellationRequested();
                _transmittedCommands.Enqueue(cmd.ProtocolCommandId);
                _transmittedCommandSignal.Release();

                if (_respondToConnectionOpen &&
                    cmd.ProtocolCommandId == ProtocolCommandId.ConnectionOpen)
                {
                    return new ValueTask(DeliverCommandAsync(ProtocolCommandId.ConnectionOpenOk));
                }

                return default;
            }

            public ValueTask TransmitAsync<TMethod, THeader>(in TMethod cmd, in THeader header,
                ReadOnlyMemory<byte> body, IDisposable? bodyOwner, CancellationToken cancellationToken)
                where TMethod : struct, IOutgoingAmqpMethod
                where THeader : IAmqpHeader
            {
                bodyOwner?.Dispose();
                throw new NotSupportedException();
            }

            public ValueTask TransmitAsync<TMethod, THeader>(in TMethod cmd, in THeader header,
                ReadOnlySequence<byte> body, IDisposable? bodyOwner, CancellationToken cancellationToken)
                where TMethod : struct, IOutgoingAmqpMethod
                where THeader : IAmqpHeader
            {
                bodyOwner?.Dispose();
                throw new NotSupportedException();
            }

            public async Task<ProtocolCommandId> ReadTransmittedCommandAsync()
            {
                Assert.True(await _transmittedCommandSignal.WaitAsync(TimingFixture.TestTimeout));
                Assert.True(_transmittedCommands.TryDequeue(out ProtocolCommandId commandId));
                return commandId;
            }

            public Task DeliverCommandAsync(ProtocolCommandId commandId)
            {
                CommandReceivedAction commandReceived = CommandReceived ??
                    throw new InvalidOperationException("No command receiver is registered.");
                return commandReceived(new IncomingCommand { CommandId = commandId },
                    CancellationToken.None);
            }

            public void Dispose()
            {
                _transmittedCommandSignal.Dispose();
            }

            private async Task NotifySessionShutdownAsync(ShutdownEventArgs reason)
            {
                AsyncEventHandler<ShutdownEventArgs>? handlers = _sessionShutdownAsync;
                if (handlers is null)
                {
                    return;
                }

                foreach (AsyncEventHandler<ShutdownEventArgs> handler in handlers.GetInvocationList())
                {
                    await handler(this, reason).ConfigureAwait(false);
                }
            }
        }
    }
}
