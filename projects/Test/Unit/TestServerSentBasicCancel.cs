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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Impl;
using Xunit;

using ImplChannel = RabbitMQ.Client.Impl.Channel;

namespace Test.Unit
{
    public class TestServerSentBasicCancel
    {
        private const string ConsumerTag = "ctag-1";

        [Fact]
        public void TestCapabilitySetToTrueIsPresent()
        {
            Assert.True(Connection.ServerHasCapability(
                ServerProperties(Connection.AcceptConsumerCancelOkCapability, true),
                Connection.AcceptConsumerCancelOkCapability));
        }

        [Fact]
        public void TestCapabilitySetToFalseIsAbsent()
        {
            Assert.False(Connection.ServerHasCapability(
                ServerProperties(Connection.AcceptConsumerCancelOkCapability, false),
                Connection.AcceptConsumerCancelOkCapability));
        }

        [Fact]
        public void TestMissingCapabilityIsAbsent()
        {
            Assert.False(Connection.ServerHasCapability(
                ServerProperties("consumer_cancel_notify", true),
                Connection.AcceptConsumerCancelOkCapability));
        }

        [Fact]
        public void TestCapabilityIsAbsentWithoutCapabilitiesTable()
        {
            Assert.False(Connection.ServerHasCapability(
                new Dictionary<string, object?> { ["product"] = "RabbitMQ" },
                Connection.AcceptConsumerCancelOkCapability));
            Assert.False(Connection.ServerHasCapability(null, Connection.AcceptConsumerCancelOkCapability));
        }

        [Theory]
        [InlineData("ctag-1")]
        [InlineData("")]
        [InlineData("amq.ctag-ünïcødé")]
        public void TestBasicCancelOkRoundTrips(string consumerTag)
        {
            var method = new BasicCancelOk(consumerTag);
            byte[] buffer = new byte[method.GetRequiredBufferSize()];

            int written = method.WriteTo(buffer);

            Assert.Equal(buffer.Length, written);
            Assert.Equal(consumerTag, new BasicCancelOk(buffer)._consumerTag);
        }

        [Fact]
        public async Task TestServerSentBasicCancelIsAnsweredWhenServerAcceptsCancelOkAsync()
        {
            using var session = new TestSession(serverAcceptsConsumerCancelOk: true);
            var channel = CreateChannel(session);

            try
            {
                var consumer = new CancelRecordingConsumer(channel);
                await channel.ConsumerDispatcher.HandleBasicConsumeOkAsync(consumer, ConsumerTag,
                    CancellationToken.None);

                await session.DeliverBasicCancelAsync(ConsumerTag);

                await consumer.CancelledConsumerTag.WaitAsync(TimingFixture.TestTimeout);
                Assert.Equal(ConsumerTag, await consumer.CancelledConsumerTag);
                TransmittedCommand reply = Assert.Single(session.TransmittedCommands);
                Assert.Equal(ProtocolCommandId.BasicCancelOk, reply.CommandId);
                Assert.Equal(ConsumerTag, new BasicCancelOk(reply.Method)._consumerTag);
            }
            finally
            {
                await DisposeChannelAsync(session, channel);
            }
        }

        [Fact]
        public async Task TestServerSentBasicCancelIsNotAnsweredWhenServerDoesNotAcceptCancelOkAsync()
        {
            using var session = new TestSession(serverAcceptsConsumerCancelOk: false);
            var channel = CreateChannel(session);

            try
            {
                var consumer = new CancelRecordingConsumer(channel);
                await channel.ConsumerDispatcher.HandleBasicConsumeOkAsync(consumer, ConsumerTag,
                    CancellationToken.None);

                await session.DeliverBasicCancelAsync(ConsumerTag);

                await consumer.CancelledConsumerTag.WaitAsync(TimingFixture.TestTimeout);
                Assert.Equal(ConsumerTag, await consumer.CancelledConsumerTag);
                Assert.Empty(session.TransmittedCommands);
            }
            finally
            {
                await DisposeChannelAsync(session, channel);
            }
        }

        [Fact]
        public async Task TestServerSentBasicCancelIsNotAnsweredWhenChannelIsClosingAsync()
        {
            using var session = new TestSession(serverAcceptsConsumerCancelOk: true);
            var channel = CreateChannel(session);

            try
            {
                channel.SetCloseReason(new ShutdownEventArgs(ShutdownInitiator.Application,
                    Constants.ReplySuccess, "closing"));

                await session.DeliverBasicCancelAsync(ConsumerTag);

                Assert.Empty(session.TransmittedCommands);
            }
            finally
            {
                await DisposeChannelAsync(session, channel);
            }
        }

        private static Dictionary<string, object?> ServerProperties(string capability, object value)
            => new Dictionary<string, object?>
            {
                ["capabilities"] = new Dictionary<string, object?> { [capability] = value }
            };

        private static ImplChannel CreateChannel(TestSession session)
            => new ImplChannel(session, new CreateChannelOptions(
                publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false));

        private static async Task DisposeChannelAsync(TestSession session, ImplChannel channel)
        {
            await session.CloseAsync(new ShutdownEventArgs(ShutdownInitiator.Library,
                Constants.ReplySuccess, "test teardown"));
            await channel.DisposeAsync();
        }

        private sealed class CancelRecordingConsumer : AsyncDefaultBasicConsumer
        {
            private readonly TaskCompletionSource<string> _cancelled =
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            public CancelRecordingConsumer(IChannel channel) : base(channel)
            {
            }

            public Task<string> CancelledConsumerTag => _cancelled.Task;

            protected override Task OnCancelAsync(string[] consumerTags, CancellationToken cancellationToken = default)
            {
                _cancelled.TrySetResult(Assert.Single(consumerTags));
                return Task.CompletedTask;
            }
        }

        private readonly struct TransmittedCommand
        {
            public TransmittedCommand(ProtocolCommandId commandId, byte[] method)
            {
                CommandId = commandId;
                Method = method;
            }

            public ProtocolCommandId CommandId { get; }

            public byte[] Method { get; }
        }

        private sealed class TestSession : ISession, IDisposable
        {
            private readonly ConcurrentQueue<TransmittedCommand> _transmittedCommands =
                new ConcurrentQueue<TransmittedCommand>();
            private AsyncEventHandler<ShutdownEventArgs>? _sessionShutdownAsync;

            public TestSession(bool serverAcceptsConsumerCancelOk)
            {
                ServerAcceptsConsumerCancelOk = serverAcceptsConsumerCancelOk;
            }

            public ushort ChannelNumber => 1;

            public ShutdownEventArgs? CloseReason { get; private set; }

            public CommandReceivedAction? CommandReceived { get; set; }

            public Connection Connection => throw new NotSupportedException();

            public bool ServerAcceptsConsumerCancelOk { get; }

            public bool IsOpen => CloseReason is null;

            public IReadOnlyCollection<TransmittedCommand> TransmittedCommands => _transmittedCommands.ToArray();

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
                byte[] method = new byte[cmd.GetRequiredBufferSize()];
                int written = cmd.WriteTo(method);
                Array.Resize(ref method, written);
                _transmittedCommands.Enqueue(new TransmittedCommand(cmd.ProtocolCommandId, method));
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

            public Task DeliverBasicCancelAsync(string consumerTag)
            {
                var cancel = new BasicCancel(consumerTag, Nowait: true);
                byte[] rented = ArrayPool<byte>.Shared.Rent(cancel.GetRequiredBufferSize());
                int written = cancel.WriteTo(rented);

                CommandReceivedAction commandReceived = CommandReceived ??
                    throw new InvalidOperationException("No command receiver is registered.");
                return commandReceived(new IncomingCommand
                {
                    CommandId = ProtocolCommandId.BasicCancel,
                    Method = new RentedMemory(new ReadOnlyMemory<byte>(rented, 0, written), rented)
                }, CancellationToken.None);
            }

            public void Dispose()
            {
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
