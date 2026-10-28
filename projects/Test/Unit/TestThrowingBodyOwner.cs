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
using System.Diagnostics.Tracing;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Logging;
using Xunit;

namespace Test.Unit
{
    public class TestThrowingBodyOwner
    {
        [Fact]
        public async Task ABodyOwnerThatThrowsFromDisposeDoesNotStopTheWriteLoop_GH2100()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var endpoint = new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)listener.LocalEndpoint).Port);
                Task<TcpClient> accept = listener.AcceptTcpClientAsync();
                SocketFrameHandler frameHandler = await SocketFrameHandler.CreateAsync(endpoint,
                    ConnectionFactoryBase.DefaultSocketFactory, TimeSpan.FromSeconds(5), CancellationToken.None);
                using TcpClient peer = await accept;
                try
                {
                    OutgoingFrame first = Frame(new ThrowingOwner());
                    OutgoingFrame second = Frame(null);
                    int expected = first.Size + second.Size;

                    await frameHandler.WriteAsync(first, CancellationToken.None);
                    await frameHandler.WriteAsync(second, CancellationToken.None);

                    // Both frames reach the peer only if the write loop survived the first owner.
                    var buffer = new byte[expected];
                    int received = 0;
                    // Closing the peer is what ends a stuck read on .NET Framework, which ignores the token.
                    using var readTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    using CancellationTokenRegistration closePeer = readTimeout.Token.Register(() => peer.Close());
                    NetworkStream stream = peer.GetStream();
                    while (received < expected)
                    {
                        int n = await stream.ReadAsync(buffer, received, expected - received, readTimeout.Token);
                        Assert.True(n > 0, "the peer's connection closed early");
                        received += n;
                    }
                }
                finally
                {
                    await frameHandler.CloseAsync(CancellationToken.None);
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public void DisposingAFrameWhoseOwnerThrowsReturns_GH2100()
        {
            OutgoingFrame frame = Frame(new ThrowingOwner());

            frame.Dispose();
        }

        [Fact]
        public async Task AWriteOnAClosedFrameHandlerWithAThrowingOwnerReturns_GH2100()
        {
            // The closed path disposes the frame synchronously; a throw reached the caller, which
            // disposed its own copy of the frame again.
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var endpoint = new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)listener.LocalEndpoint).Port);
                Task<TcpClient> accept = listener.AcceptTcpClientAsync();
                SocketFrameHandler frameHandler = await SocketFrameHandler.CreateAsync(endpoint,
                    ConnectionFactoryBase.DefaultSocketFactory, TimeSpan.FromSeconds(5), CancellationToken.None);
                using TcpClient peer = await accept;
                await frameHandler.CloseAsync(CancellationToken.None);

                await frameHandler.WriteAsync(Frame(new ThrowingOwner()), CancellationToken.None);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public async Task FailedWriteClosesSocketReleasesBlockedWritersAndDrainsOwners_GH2108()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var memory = new FailingMemory();
            var owners = new List<CountingOwner>();
            var failingOwner = new CountingOwner();
            owners.Add(failingOwner);
            try
            {
                var endpoint = new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)listener.LocalEndpoint).Port);
                Task<TcpClient> accept = listener.AcceptTcpClientAsync();
                SocketFrameHandler handler = await SocketFrameHandler.CreateAsync(endpoint,
                    ConnectionFactoryBase.DefaultSocketFactory, TimeSpan.FromSeconds(5), CancellationToken.None);
                using TcpClient peer = await accept;
                try
                {
                    var method = new BasicPublish("", "q", false, false);
                    var properties = new BasicProperties();
                    OutgoingFrame failing = RabbitMQ.Client.Impl.Framing.SerializeToFrames(ref method, ref properties,
                        memory.Memory, failingOwner, 1, int.MaxValue);
                    await handler.WriteAsync(failing, CancellationToken.None);
                    await memory.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    for (int i = 0; i < 128; i++)
                    {
                        var queuedOwner = new CountingOwner();
                        owners.Add(queuedOwner);
                        await handler.WriteAsync(Frame(queuedOwner), CancellationToken.None);
                    }
                    var blockedOwner = new CountingOwner();
                    owners.Add(blockedOwner);
                    Task blocked = handler.WriteAsync(Frame(blockedOwner), CancellationToken.None).AsTask();
                    Assert.False(blocked.IsCompleted);

                    memory.Release.Set();
                    ChannelClosedException blockedError = await Assert.ThrowsAsync<ChannelClosedException>(async () =>
                        await blocked.WaitAsync(TimeSpan.FromSeconds(10)));
                    Assert.Same(memory.Failure, blockedError.InnerException);

                    var rejectedOwner = new CountingOwner();
                    owners.Add(rejectedOwner);
                    ChannelClosedException rejectedError = await Assert.ThrowsAsync<ChannelClosedException>(() =>
                        handler.WriteAsync(Frame(rejectedOwner), CancellationToken.None).AsTask());
                    Assert.Same(memory.Failure, rejectedError.InnerException);

                    // A healthy peer must see EOF even though no receive loop detected a failure.
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    using CancellationTokenRegistration closePeer = timeout.Token.Register(() => peer.Close());
                    var buffer = new byte[1];
                    Assert.Equal(0, await peer.GetStream().ReadAsync(buffer, 0, 1, timeout.Token));
                    await handler.CloseAsync(CancellationToken.None);
                    Assert.All(owners, owner => Assert.Equal(1, owner.DisposeCount));
                    var writer = (PipeWriter)typeof(SocketFrameHandler)
                        .GetField("_pipeWriter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler)!;
                    var reader = (PipeReader)typeof(SocketFrameHandler)
                        .GetField("_pipeReader", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler)!;
                    Assert.Throws<InvalidOperationException>(() => writer.GetMemory(1));
                    await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadAsync().AsTask());
                }
                finally
                {
                    memory.Release.Set();
                    peer.Close();
                    await handler.CloseAsync(CancellationToken.None);
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        // Tracing reads the remote endpoint for every publish, and the write loop can close the socket first.
        [Fact]
        public async Task EndpointsRemainReadableAfterTheSocketIsClosed_GH2108()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var endpoint = new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)listener.LocalEndpoint).Port);
                Task<TcpClient> accept = listener.AcceptTcpClientAsync();
                SocketFrameHandler handler = await SocketFrameHandler.CreateAsync(endpoint,
                    ConnectionFactoryBase.DefaultSocketFactory, TimeSpan.FromSeconds(5), CancellationToken.None);
                using TcpClient peer = await accept;
                int localPort = handler.LocalPort;
                int remotePort = handler.RemotePort;

                handler.CloseSocket();

                Assert.Equal(localPort, handler.LocalPort);
                Assert.Equal(remotePort, handler.RemotePort);
                Assert.Equal(AddressFamily.InterNetwork, handler.RemoteEndPoint.AddressFamily);
                await handler.CloseAsync(CancellationToken.None);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public void OwnerDisposalReturnsWhenLoggingItsExceptionThrows_GH2108()
        {
            using var listener = new ErrorListener();
            var failure = new ThrowingMessageException();
            var owner = new ThrowingOwner(failure);

            BodyOwner.Dispose(owner);

            Assert.True(failure.MessageRead);
            Assert.Equal(1, owner.DisposeCount);
        }

        private sealed class ErrorListener : EventListener
        {
            public ErrorListener() => EnableEvents(RabbitMqClientEventSource.Log, EventLevel.Error);
        }

        private sealed class ThrowingMessageException : Exception
        {
            public bool MessageRead { get; private set; }
            public override string Message
            {
                get
                {
                    MessageRead = true;
                    throw new InvalidOperationException("exception formatting bug");
                }
            }
        }

        private sealed class CountingOwner : IDisposable
        {
            private int _disposeCount;
            public int DisposeCount => Volatile.Read(ref _disposeCount);
            public void Dispose() => Interlocked.Increment(ref _disposeCount);
        }

        private sealed class FailingMemory : MemoryManager<byte>
        {
            public readonly TaskCompletionSource<bool> Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly ManualResetEventSlim Release = new();
            public readonly ObjectDisposedException Failure = new(nameof(FailingMemory));
            public override Memory<byte> Memory => CreateMemory(8);
            public override Span<byte> GetSpan()
            {
                Entered.TrySetResult(true);
                Assert.True(Release.Wait(TimeSpan.FromSeconds(10)));
                throw Failure;
            }
            public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
            public override void Unpin() { }
            protected override void Dispose(bool disposing) => Release.Dispose();
        }

        private static OutgoingFrame Frame(IDisposable owner)
        {
            var method = new BasicPublish("exchange", "routing-key", false, false);
            var header = new BasicProperties();
            ReadOnlyMemory<byte> body = new byte[8];
            return RabbitMQ.Client.Impl.Framing.SerializeToFrames(ref method, ref header, body, owner, 1, int.MaxValue);
        }

        private sealed class ThrowingOwner : IDisposable
        {
            private readonly Exception _failure;
            public int DisposeCount { get; private set; }
            public ThrowingOwner(Exception failure = null) => _failure = failure ?? new InvalidOperationException("owner bug");
            public void Dispose()
            {
                DisposeCount++;
                throw _failure;
            }
        }
    }
}
