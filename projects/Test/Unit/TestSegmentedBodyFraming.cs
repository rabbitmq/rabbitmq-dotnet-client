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
using System.IO.Pipelines;
using RabbitMQ.Client;
using Xunit;

namespace Test.Unit
{
    /// <summary>
    /// Verifies that <see cref="OutgoingFrame"/> frames a non-contiguous
    /// (multi-segment) body exactly like the contiguous equivalent.
    /// </summary>
    public class TestSegmentedBodyFraming
    {
        private const ushort Channel = 3;
        private static readonly byte[] s_methodAndHeader = { 0x01, 0x02, 0x03, 0x04, 0x05 };

        [Theory]
        // maxBodyPayloadBytes smaller than, equal to, and larger than the segment size
        [InlineData(64, 16, 4)]
        [InlineData(64, 16, 16)]
        [InlineData(64, 16, 64)]
        [InlineData(64, 16, 1000)]
        [InlineData(64, 16, int.MaxValue)]
        // body length that is not a multiple of either the segment or the payload size
        [InlineData(70, 16, 7)]
        [InlineData(70, 7, 16)]
        [InlineData(70, 1, 3)]
        [InlineData(1, 1, 1)]
        public void SegmentedBodyIsByteIdenticalToContiguousBody(int bodyLength, int segmentSize, int maxBodyPayloadBytes)
        {
            byte[] body = GetBody(bodyLength);
            ReadOnlySequence<byte> segmented = ReadOnlySequenceFactory.CreateSegmented(body, segmentSize);

            byte[] contiguousBytes = WriteToArray(CreateFrame(new ReadOnlySequence<byte>(body), maxBodyPayloadBytes));
            byte[] segmentedBytes = WriteToArray(CreateFrame(segmented, maxBodyPayloadBytes));

            Assert.Equal(contiguousBytes, segmentedBytes);
        }

        [Theory]
        [InlineData(64, 16)]
        [InlineData(70, 7)]
        [InlineData(70, 1)]
        public void SegmentedBodyIsMultiSegment(int bodyLength, int segmentSize)
        {
            // Guards the tests above: a single-segment sequence would silently exercise the fast path.
            ReadOnlySequence<byte> segmented = ReadOnlySequenceFactory.CreateSegmented(GetBody(bodyLength), segmentSize);
            Assert.False(segmented.IsSingleSegment);
            Assert.Equal(bodyLength, segmented.Length);
        }

        [Theory]
        [InlineData(64, 16, 4, 16)]  // payload smaller than segment
        [InlineData(64, 16, 16, 4)]  // payload equal to segment
        [InlineData(64, 16, 64, 1)]  // payload covers the whole body
        [InlineData(70, 16, 7, 10)]  // ragged last frame
        [InlineData(70, 16, int.MaxValue, 1)]
        public void SegmentedBodyProducesExpectedBodyFrames(int bodyLength, int segmentSize,
            int maxBodyPayloadBytes, int expectedBodyFrameCount)
        {
            byte[] body = GetBody(bodyLength);
            ReadOnlySequence<byte> segmented = ReadOnlySequenceFactory.CreateSegmented(body, segmentSize);

            byte[] bytes = WriteToArray(CreateFrame(segmented, maxBodyPayloadBytes));

            Assert.Equal(s_methodAndHeader, bytes.AsSpan(0, s_methodAndHeader.Length).ToArray());

            List<byte[]> bodyFrames = ParseBodyFrames(bytes.AsSpan(s_methodAndHeader.Length).ToArray());
            Assert.Equal(expectedBodyFrameCount, bodyFrames.Count);

            // Payloads concatenate back to the original body, and no frame exceeds the payload limit.
            var reassembled = new List<byte>();
            foreach (byte[] payload in bodyFrames)
            {
                Assert.InRange(payload.Length, 1, maxBodyPayloadBytes);
                reassembled.AddRange(payload);
            }
            Assert.Equal(body, reassembled.ToArray());
        }

        [Fact]
        public void SizeMatchesTheNumberOfBytesWritten()
        {
            const int BodyLength = 70;
            const int SegmentSize = 16;
            const int MaxBodyPayloadBytes = 7;

            ReadOnlySequence<byte> segmented =
                ReadOnlySequenceFactory.CreateSegmented(GetBody(BodyLength), SegmentSize);

            OutgoingFrame frame = CreateFrame(segmented, MaxBodyPayloadBytes);
            int size = frame.Size;

            Assert.Equal(size, WriteToArray(frame).Length);
        }

        [Fact]
        public void EmptySequenceProducesNoBodyFrames()
        {
            byte[] bytes = WriteToArray(CreateFrame(ReadOnlySequence<byte>.Empty, 16));
            Assert.Equal(s_methodAndHeader, bytes);
        }

        [Fact]
        public void SequenceOfEmptySegmentsProducesNoBodyFrames()
        {
            ReadOnlySequence<byte> segmented = ReadOnlySequenceFactory.CreateSegmented(
                Array.Empty<byte>(), Array.Empty<byte>(), Array.Empty<byte>());

            Assert.Equal(0, segmented.Length);

            byte[] bytes = WriteToArray(CreateFrame(segmented, 16));
            Assert.Equal(s_methodAndHeader, bytes);
        }

        [Fact]
        public void EmptySegmentsInterleavedWithDataAreSkipped()
        {
            ReadOnlySequence<byte> segmented = ReadOnlySequenceFactory.CreateSegmented(
                new byte[] { 1, 2, 3 },
                Array.Empty<byte>(),
                new byte[] { 4, 5 },
                Array.Empty<byte>(),
                new byte[] { 6 });

            byte[] expectedBody = { 1, 2, 3, 4, 5, 6 };
            Assert.Equal(expectedBody.Length, segmented.Length);

            byte[] contiguousBytes = WriteToArray(CreateFrame(new ReadOnlySequence<byte>(expectedBody), 4));
            byte[] segmentedBytes = WriteToArray(CreateFrame(segmented, 4));

            Assert.Equal(contiguousBytes, segmentedBytes);
        }

        [Fact]
        public void DisposeDisposesTheBodyOwnerAndReturnsThePooledArray()
        {
            var owner = new TrackingDisposable();
            ReadOnlySequence<byte> segmented = ReadOnlySequenceFactory.CreateSegmented(GetBody(64), 16);

            OutgoingFrame frame = CreateFrame(segmented, 16, owner);
            Assert.Equal(0, owner.DisposeCount);

            frame.Dispose();
            Assert.Equal(1, owner.DisposeCount);

            // Disposing twice must not dispose the owner again.
            frame.Dispose();
            Assert.Equal(1, owner.DisposeCount);
        }

        private static OutgoingFrame CreateFrame(in ReadOnlySequence<byte> body, int maxBodyPayloadBytes,
            IDisposable bodyOwner = null)
        {
            // OutgoingFrame.Dispose returns this array to the shared pool, so it must be rented.
            byte[] methodAndHeader = ArrayPool<byte>.Shared.Rent(s_methodAndHeader.Length);
            s_methodAndHeader.CopyTo(methodAndHeader, 0);

            long bodyLength = body.Length;
            long frameCount = maxBodyPayloadBytes == int.MaxValue
                ? (bodyLength == 0 ? 0 : 1)
                : (bodyLength + maxBodyPayloadBytes - 1) / maxBodyPayloadBytes;
            int totalSize = (int)(s_methodAndHeader.Length + bodyLength + (8 * frameCount));

            return new OutgoingFrame(methodAndHeader, s_methodAndHeader.Length, body, bodyOwner,
                Channel, maxBodyPayloadBytes, totalSize);
        }

        private static byte[] WriteToArray(OutgoingFrame frame)
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

        private static List<byte[]> ParseBodyFrames(byte[] bytes)
        {
            var frames = new List<byte[]>();
            int offset = 0;
            while (offset < bytes.Length)
            {
                Assert.Equal(Constants.FrameBody, bytes[offset]);
                Assert.Equal(0, bytes[offset + 1]);
                Assert.Equal(Channel, bytes[offset + 2]);

                int payloadLength = (bytes[offset + 3] << 24) | (bytes[offset + 4] << 16) |
                                    (bytes[offset + 5] << 8) | bytes[offset + 6];

                byte[] payload = new byte[payloadLength];
                Array.Copy(bytes, offset + 7, payload, 0, payloadLength);
                frames.Add(payload);

                Assert.Equal(Constants.FrameEnd, bytes[offset + 7 + payloadLength]);
                offset += 8 + payloadLength;
            }

            Assert.Equal(bytes.Length, offset);
            return frames;
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
