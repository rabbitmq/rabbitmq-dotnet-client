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
using System.IO.Pipelines;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Impl;
using Xunit;

namespace Test.Unit
{
    /// <summary>
    /// Verifies <see cref="Framing.SerializeToFrames{TMethod,THeader}(ref TMethod, ref THeader, in System.Buffers.ReadOnlySequence{byte}, IDisposable, ushort, int)"/>:
    /// the copy path (no body owner) and the zero-copy path (ownership transferred) must produce
    /// identical wire bytes, and both must reject bodies that cannot be framed.
    /// </summary>
    public class TestSerializeToFramesWithSequence
    {
        private const ushort Channel = 3;
        private const string Exchange = "exchange";
        private const string RoutingKey = "routing-key";

        [Theory]
        // maxBodyPayloadBytes smaller than, equal to, and larger than the segment size
        [InlineData(64, 16, 4)]
        [InlineData(64, 16, 16)]
        [InlineData(64, 16, 64)]
        [InlineData(64, 16, 1024)]
        [InlineData(70, 16, 7)]
        [InlineData(70, 7, 16)]
        [InlineData(70, 1, 3)]
        [InlineData(1024, 128, 100)]
        public void CopyPathAndZeroCopyPathProduceIdenticalBytes(int bodyLength, int segmentSize, int maxBodyPayloadBytes)
        {
            byte[] body = GetBody(bodyLength);
            ReadOnlySequence<byte> segmented = ReadOnlySequenceFactory.CreateSegmented(body, segmentSize);
            Assert.False(segmented.IsSingleSegment);

            byte[] contiguousBytes = SerializeToArray(new ReadOnlySequence<byte>(body), null, maxBodyPayloadBytes);
            byte[] copyPathBytes = SerializeToArray(segmented, null, maxBodyPayloadBytes);

            var owner = new TrackingDisposable();
            byte[] zeroCopyBytes = SerializeToArray(segmented, owner, maxBodyPayloadBytes);

            Assert.Equal(contiguousBytes, copyPathBytes);
            Assert.Equal(contiguousBytes, zeroCopyBytes);
            Assert.Equal(1, owner.DisposeCount);
        }

        [Theory]
        [InlineData(64, 16, 4)]
        [InlineData(70, 7, 16)]
        public void SizeMatchesTheNumberOfBytesWrittenOnBothPaths(int bodyLength, int segmentSize, int maxBodyPayloadBytes)
        {
            ReadOnlySequence<byte> segmented =
                ReadOnlySequenceFactory.CreateSegmented(GetBody(bodyLength), segmentSize);

            AssertSizeMatchesBytesWritten(segmented, null, maxBodyPayloadBytes);
            AssertSizeMatchesBytesWritten(segmented, new TrackingDisposable(), maxBodyPayloadBytes);
        }

        [Fact]
        public void SingleSegmentSequenceTakesTheContiguousPath()
        {
            byte[] body = GetBody(1024);

            byte[] fromMemory = SerializeToArray(body, null, 100);
            byte[] fromSequence = SerializeToArray(new ReadOnlySequence<byte>(body), null, 100);

            Assert.Equal(fromMemory, fromSequence);
        }

        [Fact]
        public void SingleSegmentSequenceTransfersOwnership()
        {
            byte[] body = GetBody(1024);
            var memoryOwner = new TrackingDisposable();
            var sequenceOwner = new TrackingDisposable();

            byte[] fromMemory = SerializeToArray(body, memoryOwner, 100);
            byte[] fromSequence = SerializeToArray(new ReadOnlySequence<byte>(body), sequenceOwner, 100);

            Assert.Equal(fromMemory, fromSequence);
            Assert.Equal(1, memoryOwner.DisposeCount);
            Assert.Equal(1, sequenceOwner.DisposeCount);
        }

        [Fact]
        public void EmptySegmentsProduceNoBodyFramesOnBothPaths()
        {
            ReadOnlySequence<byte> segmented = ReadOnlySequenceFactory.CreateSegmented(
                Array.Empty<byte>(), Array.Empty<byte>());
            Assert.False(segmented.IsSingleSegment);

            byte[] emptyBodyBytes = SerializeToArray(ReadOnlyMemory<byte>.Empty, null, 16);

            Assert.Equal(emptyBodyBytes, SerializeToArray(segmented, null, 16));
            Assert.Equal(emptyBodyBytes, SerializeToArray(segmented, new TrackingDisposable(), 16));
        }

        [Fact]
        public void EmptyBodyWithUnlimitedFrameSizeProducesNoBodyFrames()
        {
            // maxBodyPayloadBytes == int.MaxValue is the "unlimited frame size" case.
            byte[] withFrameLimit = SerializeToArray(ReadOnlyMemory<byte>.Empty, null, 16);
            byte[] withoutFrameLimit = SerializeToArray(ReadOnlyMemory<byte>.Empty, null, int.MaxValue);

            Assert.Equal(withFrameLimit, withoutFrameLimit);
        }

        [Fact]
        public void BodyRequiringMoreThanIntMaxValueBytesOfFramesIsRejected()
        {
            /*
             * A body just under int.MaxValue cannot be framed: the per-frame overhead pushes the
             * total frame set past int.MaxValue. The sequence below reports its length without
             * allocating any memory, since validation happens before the body is read.
             */
            const int MaxBodyPayloadBytes = 1024;
            ReadOnlySequence<byte> hugeBody = ReadOnlySequenceFactory.CreateUnbacked(segmentCount: 2);
            Assert.InRange(hugeBody.Length, 1, int.MaxValue);

            var owner = new TrackingDisposable();
            ArgumentOutOfRangeException ex = AssertSerializeThrows(hugeBody, owner, MaxBodyPayloadBytes);

            Assert.Equal("body", ex.ParamName);
            Assert.Contains("exceeds the maximum", ex.Message);
            Assert.Contains(MaxBodyPayloadBytes.ToString(), ex.Message);

            // The frame was never created, so the caller (SessionBase.TransmitAsync) owns disposal.
            Assert.Equal(0, owner.DisposeCount);
        }

        [Fact]
        public void BodyLongerThanIntMaxValueIsRejected()
        {
            ReadOnlySequence<byte> hugeBody = ReadOnlySequenceFactory.CreateUnbacked(segmentCount: 3);
            Assert.True(hugeBody.Length > int.MaxValue);

            var owner = new TrackingDisposable();
            ArgumentOutOfRangeException ex = AssertSerializeThrows(hugeBody, owner, 1024);

            Assert.Equal("body", ex.ParamName);
            Assert.Equal(0, owner.DisposeCount);
        }

        [Fact]
        public void RejectedBodyIsAlsoRejectedOnTheCopyPath()
        {
            ReadOnlySequence<byte> hugeBody = ReadOnlySequenceFactory.CreateUnbacked(segmentCount: 3);

            ArgumentOutOfRangeException ex = AssertSerializeThrows(hugeBody, null, 1024);

            Assert.Equal("body", ex.ParamName);
        }

        private static ArgumentOutOfRangeException AssertSerializeThrows(ReadOnlySequence<byte> body,
            IDisposable bodyOwner, int maxBodyPayloadBytes)
        {
            return Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var method = new BasicPublish(Exchange, RoutingKey, false, false);
                var header = new BasicProperties();
                using OutgoingFrame frame = Framing.SerializeToFrames(ref method, ref header, body,
                    bodyOwner, Channel, maxBodyPayloadBytes);
            });
        }

        private static void AssertSizeMatchesBytesWritten(in ReadOnlySequence<byte> body,
            IDisposable bodyOwner, int maxBodyPayloadBytes)
        {
            var method = new BasicPublish(Exchange, RoutingKey, false, false);
            var header = new BasicProperties();

            OutgoingFrame frame = Framing.SerializeToFrames(ref method, ref header, body, bodyOwner,
                Channel, maxBodyPayloadBytes);
            int size = frame.Size;

            Assert.Equal(size, WriteToArray(frame).Length);
        }

        private static byte[] SerializeToArray(in ReadOnlySequence<byte> body, IDisposable bodyOwner,
            int maxBodyPayloadBytes)
        {
            var method = new BasicPublish(Exchange, RoutingKey, false, false);
            var header = new BasicProperties();

            return WriteToArray(Framing.SerializeToFrames(ref method, ref header, body, bodyOwner,
                Channel, maxBodyPayloadBytes));
        }

        private static byte[] SerializeToArray(ReadOnlyMemory<byte> body, IDisposable bodyOwner,
            int maxBodyPayloadBytes)
        {
            var method = new BasicPublish(Exchange, RoutingKey, false, false);
            var header = new BasicProperties();

            return WriteToArray(Framing.SerializeToFrames(ref method, ref header, body, bodyOwner,
                Channel, maxBodyPayloadBytes));
        }

        private static byte[] WriteToArray(OutgoingFrame frame)
        {
            try
            {
                var pipe = new Pipe();
                frame.WriteTo(pipe.Writer);
                pipe.Writer.Complete();

                Assert.True(pipe.Reader.TryRead(out ReadResult result));
                byte[] bytes = result.Buffer.ToArray();
                pipe.Reader.AdvanceTo(result.Buffer.End);
                pipe.Reader.Complete();
                return bytes;
            }
            finally
            {
                frame.Dispose();
            }
        }

        private static byte[] GetBody(int length)
        {
            byte[] body = new byte[length];
            for (int i = 0; i < length; i++)
            {
                body[i] = (byte)(i % 251);
            }
            return body;
        }

        private sealed class TrackingDisposable : IDisposable
        {
            public int DisposeCount { get; private set; }

            public void Dispose() => DisposeCount++;
        }
    }
}
