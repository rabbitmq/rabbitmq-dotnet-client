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
using RabbitMQ.Client.Exceptions;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestBasicPublishAsync : IntegrationFixture
    {
        public TestBasicPublishAsync(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestQueuePurgeAsync()
        {
            const int messageCount = 1024;

            var publishSyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            QueueDeclareOk q = await _channel.QueueDeclareAsync(string.Empty, false, true, true);

            var publishTask = Task.Run(async () =>
            {
                byte[] body = GetRandomBody(512);
                for (int i = 0; i < messageCount; i++)
                {
                    await _channel.BasicPublishAsync(string.Empty, q, body);
                }
                publishSyncSource.SetResult(true);
            });

            Assert.True(await publishSyncSource.Task);
            Assert.Equal((uint)messageCount, await _channel.QueuePurgeAsync(q));
        }

        [Fact]
        public async Task TestBasicReturnAsync()
        {
            string routingKey = Guid.NewGuid().ToString();
            try
            {
                await _channel.BasicPublishAsync(exchange: string.Empty, routingKey: routingKey,
                    mandatory: true, body: GetRandomBody());
            }
            catch (PublishReturnException prex)
            {
                Assert.True(prex.IsReturn);
                Assert.NotNull(prex.Exchange);
                Assert.Equal(string.Empty, prex.Exchange);
                Assert.NotNull(prex.RoutingKey);
                Assert.Equal(routingKey, prex.RoutingKey);
                Assert.NotEqual(0, prex.ReplyCode);
                Assert.NotNull(prex.ReplyText);
                Assert.Equal("NO_ROUTE", prex.ReplyText);

            }
        }

        [Fact]
        public async Task TestMemoryOwnerBody()
        {
            const int size = 1024;

            QueueDeclareOk q = await _channel.QueueDeclareAsync(string.Empty, false, true, true);
            var body = new TrackedMemoryOwner(GetRandomBody(size));

            await _channel.BasicPublishAsync(string.Empty, q,
                mandatory: true, body: body.Memory, body);

            Assert.Equal((uint)1, await _channel.QueuePurgeAsync(q));
            await body.AssertDisposedExactlyOnceAsync();
        }

        [Fact]
        public async Task TestMemoryOwnerBodyDisposedWhenChannelAlreadyClosed()
        {
            var body = new TrackedMemoryOwner(GetRandomBody(1024));

            await _channel.CloseAsync();

            await Assert.ThrowsAnyAsync<Exception>(() =>
                _channel.BasicPublishAsync(string.Empty, "queue",
                    mandatory: false, body: body.Memory, body).AsTask());

            await body.AssertDisposedExactlyOnceAsync();
        }

        [Fact]
        public async Task TestMemoryOwnerBodyDisposedOnCancellation()
        {
            var body = new TrackedMemoryOwner(GetRandomBody(1024));
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<Exception>(() =>
                _channel.BasicPublishAsync(string.Empty, "queue",
                    mandatory: false, body: body.Memory, body,
                    cancellationToken: cts.Token).AsTask());

            await body.AssertDisposedExactlyOnceAsync();
        }

        [Fact]
        public async Task TestSequenceBodyRoundTrip()
        {
            const int Size = 1024;
            const int SegmentSize = 128;

            QueueDeclareOk q = await _channel.QueueDeclareAsync(string.Empty, false, true, true);
            var body = new TrackedSequenceOwner(GetRandomBody(Size), SegmentSize);
            Assert.False(body.Sequence.IsSingleSegment);

            await _channel.BasicPublishAsync(string.Empty, q.QueueName,
                mandatory: true, basicProperties: new BasicProperties(), body: body.Sequence, bodyOwner: body);

            BasicGetResult getResult = await _channel.BasicGetAsync(q.QueueName, true);
            Assert.NotNull(getResult);
            Assert.Equal(body.Content, getResult.Body.ToArray());

            await body.AssertDisposedExactlyOnceAsync();
        }

        [Fact]
        public async Task TestSequenceBodyRoundTripWithExtensionOverload()
        {
            const int Size = 1024;
            const int SegmentSize = 100;

            QueueDeclareOk q = await _channel.QueueDeclareAsync(string.Empty, false, true, true);
            var body = new TrackedSequenceOwner(GetRandomBody(Size), SegmentSize);

            // (Extension method) overload: no exchange/properties boilerplate.
            await _channel.BasicPublishAsync(string.Empty, q.QueueName, body.Sequence, body);

            BasicGetResult getResult = await _channel.BasicGetAsync(q.QueueName, true);
            Assert.NotNull(getResult);
            Assert.Equal(body.Content, getResult.Body.ToArray());

            await body.AssertDisposedExactlyOnceAsync();
        }

        [Fact]
        public async Task TestSequenceBodyRoundTripWithCachedString()
        {
            const int Size = 2048;
            const int SegmentSize = 333;

            QueueDeclareOk q = await _channel.QueueDeclareAsync(string.Empty, false, true, true);
            CachedString exchange = new CachedString(string.Empty);
            CachedString routingKey = new CachedString(q.QueueName);
            var body = new TrackedSequenceOwner(GetRandomBody(Size), SegmentSize);

            await _channel.BasicPublishAsync(exchange, routingKey,
                mandatory: true, basicProperties: new BasicProperties(), body: body.Sequence, bodyOwner: body);

            BasicGetResult getResult = await _channel.BasicGetAsync(q.QueueName, true);
            Assert.NotNull(getResult);
            Assert.Equal(body.Content, getResult.Body.ToArray());

            await body.AssertDisposedExactlyOnceAsync();
        }

        [Fact]
        public async Task TestSequenceBodySpanningMultipleFramesAndSegments()
        {
            /*
             * A body larger than the negotiated frame size is split into several body frames, and
             * those frame boundaries do not line up with the segment boundaries of the sequence.
             */
            const int SegmentSize = 4099;
            int size = _conn.FrameMax == 0 ? 512 * 1024 : (int)_conn.FrameMax * 3;

            byte[] content = GetLargeBody(size);
            var body = new TrackedSequenceOwner(content, SegmentSize);
            Assert.False(body.Sequence.IsSingleSegment);
            if (_conn.FrameMax > 0)
            {
                Assert.True(body.Sequence.Length > _conn.FrameMax);
            }

            QueueDeclareOk q = await _channel.QueueDeclareAsync(string.Empty, false, true, true);

            await _channel.BasicPublishAsync(string.Empty, q.QueueName,
                mandatory: true, basicProperties: new BasicProperties(), body: body.Sequence, bodyOwner: body);

            BasicGetResult getResult = await _channel.BasicGetAsync(q.QueueName, true);
            Assert.NotNull(getResult);
            Assert.Equal(body.Content, getResult.Body.ToArray());

            await body.AssertDisposedExactlyOnceAsync();
        }

        [Fact]
        public async Task TestSequenceBodyRoundTripWithoutPublisherConfirmations()
        {
            const int Size = 4096;
            const int SegmentSize = 512;

            var options = new CreateChannelOptions(publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false);
            await using IChannel channel = await _conn.CreateChannelAsync(options);

            QueueDeclareOk q = await channel.QueueDeclareAsync(string.Empty, false, true, true);
            var body = new TrackedSequenceOwner(GetRandomBody(Size), SegmentSize);

            await channel.BasicPublishAsync(string.Empty, q.QueueName,
                mandatory: false, basicProperties: new BasicProperties(), body: body.Sequence, bodyOwner: body);

            // Without publisher confirmations the publish returns before the broker has replied,
            // so wait for the write loop to release the body.
            await body.AssertDisposedExactlyOnceAsync();

            BasicGetResult getResult = await channel.BasicGetAsync(q.QueueName, true);
            Assert.NotNull(getResult);
            Assert.Equal(body.Content, getResult.Body.ToArray());
        }

        [Fact]
        public async Task TestSequenceOwnerBodyDisposedWhenChannelAlreadyClosed()
        {
            var body = new TrackedSequenceOwner(GetRandomBody(1024), 128);

            await _channel.CloseAsync();

            await Assert.ThrowsAnyAsync<Exception>(() =>
                _channel.BasicPublishAsync(string.Empty, "queue",
                    mandatory: false, basicProperties: new BasicProperties(),
                    body: body.Sequence, bodyOwner: body).AsTask());

            await body.AssertDisposedExactlyOnceAsync();
        }

        [Fact]
        public async Task TestSequenceOwnerBodyDisposedOnCancellation()
        {
            var body = new TrackedSequenceOwner(GetRandomBody(1024), 128);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<Exception>(() =>
                _channel.BasicPublishAsync(string.Empty, "queue",
                    mandatory: false, basicProperties: new BasicProperties(),
                    body: body.Sequence, bodyOwner: body,
                    cancellationToken: cts.Token).AsTask());

            await body.AssertDisposedExactlyOnceAsync();
        }

        [Fact]
        public async Task TestSequenceOwnerBodyDisposedOnBasicReturn()
        {
            string routingKey = Guid.NewGuid().ToString();
            var body = new TrackedSequenceOwner(GetRandomBody(1024), 128);

            PublishReturnException prex = await Assert.ThrowsAsync<PublishReturnException>(() =>
                _channel.BasicPublishAsync(exchange: string.Empty, routingKey: routingKey,
                    mandatory: true, basicProperties: new BasicProperties(),
                    body: body.Sequence, bodyOwner: body).AsTask());

            Assert.True(prex.IsReturn);
            Assert.Equal("NO_ROUTE", prex.ReplyText);

            await body.AssertDisposedExactlyOnceAsync();
        }

        [Fact]
        public async Task TestSequenceBodyIsRejectedWhenLongerThanIntMaxValue()
        {
            var body = new TrackedSequenceOwner(ReadOnlySequenceFactory.CreateUnbacked(segmentCount: 3));
            Assert.True(body.Sequence.Length > int.MaxValue);

            ArgumentOutOfRangeException ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                _channel.BasicPublishAsync(string.Empty, "queue",
                    mandatory: false, basicProperties: new BasicProperties(),
                    body: body.Sequence, bodyOwner: body).AsTask());

            Assert.Equal("body", ex.ParamName);
            await body.AssertDisposedExactlyOnceAsync();
        }

        private static byte[] GetLargeBody(int size)
        {
            byte[] body = new byte[size];
            for (int i = 0; i < size; i++)
            {
                body[i] = (byte)(i % 251);
            }
            return body;
        }

        private class TrackedMemoryOwner : IMemoryOwner<byte>
        {
            private readonly DisposalTracker _tracker = new DisposalTracker();

            public TrackedMemoryOwner(byte[] content)
            {
                Memory = content;
            }

            public Memory<byte> Memory { get; }
            public bool Disposed => _tracker.DisposeCount > 0;

            public void Dispose() => _tracker.Dispose();

            public Task AssertDisposedExactlyOnceAsync() => _tracker.AssertDisposedExactlyOnceAsync();
        }

        private class TrackedSequenceOwner : IDisposable
        {
            private readonly DisposalTracker _tracker = new DisposalTracker();

            public TrackedSequenceOwner(byte[] content, int segmentSize)
            {
                Content = content;
                Sequence = ReadOnlySequenceFactory.CreateSegmented(content, segmentSize);
            }

            public TrackedSequenceOwner(ReadOnlySequence<byte> sequence)
            {
                Content = Array.Empty<byte>();
                Sequence = sequence;
            }

            public byte[] Content { get; }
            public ReadOnlySequence<byte> Sequence { get; }

            public void Dispose() => _tracker.Dispose();

            public Task AssertDisposedExactlyOnceAsync() => _tracker.AssertDisposedExactlyOnceAsync();
        }

        /// <summary>
        /// Counts disposals of a message body owner. Disposal can happen asynchronously, on the
        /// socket write loop, so tests wait for it instead of asserting immediately.
        /// </summary>
        private class DisposalTracker
        {
            private static readonly TimeSpan s_disposalTimeout = TimeSpan.FromSeconds(10);
            private static readonly TimeSpan s_extraDisposalWindow = TimeSpan.FromMilliseconds(250);

            private readonly TaskCompletionSource<bool> _disposed =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private int _disposeCount;

            public int DisposeCount => Volatile.Read(ref _disposeCount);

            public void Dispose()
            {
                Interlocked.Increment(ref _disposeCount);
                _disposed.TrySetResult(true);
            }

            public async Task AssertDisposedExactlyOnceAsync()
            {
                Task completed = await Task.WhenAny(_disposed.Task, Task.Delay(s_disposalTimeout));
                Assert.True(ReferenceEquals(completed, _disposed.Task),
                    $"the message body owner was not disposed within {s_disposalTimeout}");

                // Give any erroneous second disposal a chance to show up.
                await Task.Delay(s_extraDisposalWindow);
                Assert.Equal(1, DisposeCount);
            }
        }
    }
}
