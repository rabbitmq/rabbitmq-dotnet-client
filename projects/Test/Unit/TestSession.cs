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

namespace Test.Unit
{
    internal sealed class TestSessionOptions
    {
        public ushort ChannelNumber { get; set; }

        public bool ServerAcceptsConsumerCancelOk { get; set; }

        // When the channel transmits a key, the session delivers the value back to it.
        public IDictionary<ProtocolCommandId, ProtocolCommandId> Replies { get; } =
            new Dictionary<ProtocolCommandId, ProtocolCommandId>();
    }

    internal readonly struct TransmittedCommand
    {
        public TransmittedCommand(ProtocolCommandId commandId, byte[] method)
        {
            CommandId = commandId;
            Method = method;
        }

        public ProtocolCommandId CommandId { get; }

        public byte[] Method { get; }
    }

    // A fake ISession that records what the channel transmits and delivers commands to it,
    // so that a real Channel can be tested without a connection or a broker.
    internal sealed class TestSession : ISession, IDisposable
    {
        private readonly TestSessionOptions _options;
        private readonly ConcurrentQueue<TransmittedCommand> _transmittedCommands =
            new ConcurrentQueue<TransmittedCommand>();
        private readonly ConcurrentQueue<ProtocolCommandId> _unreadCommandIds =
            new ConcurrentQueue<ProtocolCommandId>();
        private readonly SemaphoreSlim _transmittedCommandSignal = new SemaphoreSlim(0);
        private AsyncEventHandler<ShutdownEventArgs>? _sessionShutdownAsync;

        public TestSession(TestSessionOptions options)
        {
            _options = options;
        }

        public TestSession(bool respondToConnectionOpen = false)
            : this(CreateOptions(respondToConnectionOpen))
        {
        }

        public ushort ChannelNumber => _options.ChannelNumber;

        public ShutdownEventArgs? CloseReason { get; private set; }

        public CommandReceivedAction? CommandReceived { get; set; }

        public Connection Connection => throw new NotSupportedException();

        public bool ServerAcceptsConsumerCancelOk => _options.ServerAcceptsConsumerCancelOk;

        public bool IsOpen => CloseReason is null;

        public int TransmittedCommandCount => _transmittedCommands.Count;

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
            _unreadCommandIds.Enqueue(cmd.ProtocolCommandId);
            _transmittedCommandSignal.Release();

            if (_options.Replies.TryGetValue(cmd.ProtocolCommandId, out ProtocolCommandId reply))
            {
                return new ValueTask(DeliverCommandAsync(reply));
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

        // Waits for the next transmitted command that has not been read yet.
        public async Task<ProtocolCommandId> ReadTransmittedCommandAsync()
        {
            Assert.True(await _transmittedCommandSignal.WaitAsync(TimingFixture.TestTimeout));
            Assert.True(_unreadCommandIds.TryDequeue(out ProtocolCommandId commandId));
            return commandId;
        }

        // Delivers a command without arguments, such as connection.open-ok.
        public Task DeliverCommandAsync(ProtocolCommandId commandId)
        {
            return GetCommandReceived()(new IncomingCommand { CommandId = commandId },
                CancellationToken.None);
        }

        // Delivers a command with its arguments encoded, as the broker would send it.
        public Task DeliverMethodAsync<T>(in T method) where T : struct, IOutgoingAmqpMethod
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(method.GetRequiredBufferSize());
            int written = method.WriteTo(rented);

            return GetCommandReceived()(new IncomingCommand
            {
                CommandId = method.ProtocolCommandId,
                Method = new RentedMemory(new ReadOnlyMemory<byte>(rented, 0, written), rented)
            }, CancellationToken.None);
        }

        public Task DeliverBasicCancelAsync(string consumerTag)
            => DeliverMethodAsync(new BasicCancel(consumerTag, Nowait: true));

        public void Dispose()
        {
            _transmittedCommandSignal.Dispose();
        }

        private static TestSessionOptions CreateOptions(bool respondToConnectionOpen)
        {
            var options = new TestSessionOptions();

            if (respondToConnectionOpen)
            {
                options.Replies[ProtocolCommandId.ConnectionOpen] = ProtocolCommandId.ConnectionOpenOk;
            }

            return options;
        }

        private CommandReceivedAction GetCommandReceived()
        {
            return CommandReceived ??
                throw new InvalidOperationException("No command receiver is registered.");
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
