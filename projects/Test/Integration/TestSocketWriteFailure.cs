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
using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestSocketWriteFailure : IntegrationFixture
    {
        public TestSocketWriteFailure(ITestOutputHelper output) : base(output) { }

        [Theory]
        [InlineData(false, false, 0)]
        [InlineData(false, false, 1)]
        [InlineData(false, false, 2)]
        [InlineData(false, true, 0)]
        [InlineData(false, true, 1)]
        [InlineData(false, true, 2)]
        [InlineData(true, false, 0)]
        [InlineData(true, false, 1)]
        [InlineData(true, false, 2)]
        [InlineData(true, true, 0)]
        [InlineData(true, true, 1)]
        [InlineData(true, true, 2)]
        public async Task InvalidBodyMemoryTriggersShutdownAndRecovery_GH2108(bool recovery, bool confirms, int bodyKind)
        {
            ConnectionFactory factory = CreateConnectionFactory();
            factory.AutomaticRecoveryEnabled = recovery;
            factory.NetworkRecoveryInterval = TimeSpan.FromMilliseconds(100);
            await using IConnection connection = await factory.CreateConnectionAsync();
            await using IChannel channel = await connection.CreateChannelAsync(new CreateChannelOptions(confirms, confirms));
            var shutdown = new TaskCompletionSource<(ShutdownInitiator Initiator, Exception Cause, bool ChannelOpen)>(TaskCreationOptions.RunContinuationsAsynchronously);
            var ownerDisposed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var recovered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.ConnectionShutdownAsync += (_, reason) =>
            {
                shutdown.TrySetResult((reason.Initiator, reason.Exception, channel.IsOpen));
                return Task.CompletedTask;
            };
            connection.RecoverySucceededAsync += (_, _) =>
            {
                recovered.TrySetResult(true);
                return Task.CompletedTask;
            };

            using var manager = new InvalidBodyMemory(ownerDisposed);
            ReadOnlyMemory<byte> body = manager.Memory;
            manager.Invalidate();
            Task publish = bodyKind switch
            {
                0 => channel.BasicPublishAsync("", "q", false, new BasicProperties(), body, manager).AsTask(),
                1 => channel.BasicPublishAsync("", "q", false, new BasicProperties(), new ReadOnlySequence<byte>(body), manager).AsTask(),
                _ => channel.BasicPublishAsync("", "q", false, new BasicProperties(), SegmentedBody(body), manager).AsTask()
            };
            try
            {
                await publish.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (AlreadyClosedException) { }
            catch (OperationInterruptedException) { }

            await shutdown.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var atShutdown = await shutdown.Task;
            Assert.Equal(ShutdownInitiator.Library, atShutdown.Initiator);
            Assert.Equal(nameof(InvalidBodyMemory), Assert.IsType<ObjectDisposedException>(atShutdown.Cause).ObjectName);
            Assert.False(atShutdown.ChannelOpen);
            await ownerDisposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(1, manager.DisposeCount);
            if (recovery)
            {
                await recovered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.True(connection.IsOpen);
                Assert.True(channel.IsOpen);
                QueueDeclareOk queue = await channel.QueueDeclareAsync(exclusive: true);
                byte[] validBody = { 1, 2, 3 };
                await channel.BasicPublishAsync("", queue.QueueName, validBody);
                BasicGetResult result = await channel.BasicGetAsync(queue.QueueName, true);
                Assert.NotNull(result);
                Assert.Equal(validBody, result.Body.ToArray());
            }
            else
            {
                Assert.False(connection.IsOpen);
                await Assert.ThrowsAsync<AlreadyClosedException>(() => channel.QueueDeclareAsync(exclusive: true));
            }
        }

        [Fact]
        public async Task SerializationFailurePreservesValidationExceptionWhenOwnerThrows_GH2108()
        {
            ConnectionFactory factory = CreateConnectionFactory();
            factory.AutomaticRecoveryEnabled = false;
            await using IConnection connection = await factory.CreateConnectionAsync();
            await using IChannel channel = await connection.CreateChannelAsync(new CreateChannelOptions(false, false));
            var owner = new ThrowingOwner();
            ReadOnlySequence<byte> body = ReadOnlySequenceFactory.CreateUnbacked(2);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                channel.BasicPublishAsync("", "q", false, new BasicProperties(), body, owner).AsTask());

            Assert.Equal(1, owner.DisposeCount);
            Assert.True(connection.IsOpen);
            await channel.QueueDeclareAsync(exclusive: true);
        }

        [Fact]
        public async Task SerializationFailureOfAMemoryBodyPreservesTheExceptionWhenOwnerThrows_GH2108()
        {
            ConnectionFactory factory = CreateConnectionFactory();
            factory.AutomaticRecoveryEnabled = false;
            await using IConnection connection = await factory.CreateConnectionAsync();
            await using IChannel channel = await connection.CreateChannelAsync(new CreateChannelOptions(false, false));
            var owner = new ThrowingOwner();
            var properties = new BasicProperties { Headers = new Dictionary<string, object> { ["unsupported"] = DateTime.UtcNow } };
            ReadOnlyMemory<byte> body = new byte[8];

            await Assert.ThrowsAsync<WireFormattingException>(() =>
                channel.BasicPublishAsync("", "q", false, properties, body, owner).AsTask());

            Assert.Equal(1, owner.DisposeCount);
            Assert.True(connection.IsOpen);
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

        private static ReadOnlySequence<byte> SegmentedBody(ReadOnlyMemory<byte> invalidMemory)
        {
            var first = new BodySegment(new byte[] { 1 });
            var last = new BodySegment(invalidMemory);
            first.Append(last);
            return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        }

        private sealed class BodySegment : ReadOnlySequenceSegment<byte>
        {
            public BodySegment(ReadOnlyMemory<byte> memory) => Memory = memory;
            public void Append(BodySegment next)
            {
                next.RunningIndex = RunningIndex + Memory.Length;
                Next = next;
            }
        }

        private sealed class InvalidBodyMemory : MemoryManager<byte>
        {
            private bool _invalid;
            private readonly TaskCompletionSource<bool> _disposed;
            public InvalidBodyMemory(TaskCompletionSource<bool> disposed) => _disposed = disposed;
            public int DisposeCount { get; private set; }
            public void Invalidate() => _invalid = true;
            public override Memory<byte> Memory => CreateMemory(8);
            public override Span<byte> GetSpan() => _invalid
                ? throw new ObjectDisposedException(nameof(InvalidBodyMemory)) : new byte[8];
            public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
            public override void Unpin() { }
            protected override void Dispose(bool disposing)
            {
                DisposeCount++;
                _invalid = true;
                _disposed.TrySetResult(true);
            }
        }
    }
}
