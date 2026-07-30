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

namespace Test
{
    /// <summary>
    /// Builds multi-segment <see cref="ReadOnlySequence{T}"/> instances for tests that need
    /// to exercise non-contiguous message bodies.
    /// </summary>
    public static class ReadOnlySequenceFactory
    {
        /// <summary>
        /// Splits <paramref name="data"/> into segments of at most <paramref name="segmentSize"/>
        /// bytes, returning a sequence backed by a linked list of segments.
        /// </summary>
        /// <remarks>
        /// The returned sequence is multi-segment whenever <paramref name="data"/> is longer than
        /// <paramref name="segmentSize"/>. Each segment is a copy, placed in its own array, so that
        /// segments are guaranteed to be discontiguous in memory.
        /// </remarks>
        public static ReadOnlySequence<byte> CreateSegmented(byte[] data, int segmentSize)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (segmentSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(segmentSize), segmentSize,
                    "segmentSize must be greater than zero");
            }

            var segments = new List<byte[]>();
            for (int offset = 0; offset < data.Length; offset += segmentSize)
            {
                int length = Math.Min(segmentSize, data.Length - offset);
                var segment = new byte[length];
                Array.Copy(data, offset, segment, 0, length);
                segments.Add(segment);
            }

            return CreateSegmented(segments.ToArray());
        }

        /// <summary>
        /// Creates a sequence whose segments are exactly the supplied arrays, in order.
        /// Zero-length arrays are preserved so that empty-segment handling can be tested.
        /// </summary>
        public static ReadOnlySequence<byte> CreateSegmented(params byte[][] segments)
        {
            if (segments is null)
            {
                throw new ArgumentNullException(nameof(segments));
            }

            if (segments.Length == 0)
            {
                return ReadOnlySequence<byte>.Empty;
            }

            var first = new Segment(segments[0]);
            Segment last = first;
            for (int i = 1; i < segments.Length; i++)
            {
                last = last.Append(segments[i]);
            }

            return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        }

        private sealed class Segment : ReadOnlySequenceSegment<byte>
        {
            public Segment(ReadOnlyMemory<byte> memory)
            {
                Memory = memory;
                RunningIndex = 0;
            }

            public Segment Append(ReadOnlyMemory<byte> memory)
            {
                var segment = new Segment(memory)
                {
                    RunningIndex = RunningIndex + Memory.Length
                };
                Next = segment;
                return segment;
            }
        }

        /// <summary>
        /// Creates a multi-segment sequence of <paramref name="segmentCount"/> segments of
        /// <c>int.MaxValue / 2</c> bytes each without allocating any memory: the segments are backed
        /// by a <see cref="MemoryManager{T}"/> that never materializes a buffer. Useful to test the
        /// rejection of bodies that are too large to be framed, where only the length is inspected.
        /// </summary>
        public static ReadOnlySequence<byte> CreateUnbacked(int segmentCount)
        {
            if (segmentCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(segmentCount), segmentCount,
                    "segmentCount must be greater than zero");
            }

            const int SegmentLength = int.MaxValue / 2;

            var first = new UnbackedSegment(SegmentLength, runningIndex: 0);
            UnbackedSegment last = first;
            for (int i = 1; i < segmentCount; i++)
            {
                last = last.Append(SegmentLength);
            }

            return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        }

        private sealed class UnbackedSegment : ReadOnlySequenceSegment<byte>
        {
            public UnbackedSegment(int length, long runningIndex)
            {
                Memory = new UnbackedMemoryManager(length).Memory;
                RunningIndex = runningIndex;
            }

            public UnbackedSegment Append(int length)
            {
                var segment = new UnbackedSegment(length, RunningIndex + Memory.Length);
                Next = segment;
                return segment;
            }
        }

        private sealed class UnbackedMemoryManager : MemoryManager<byte>
        {
            private readonly int _length;

            public UnbackedMemoryManager(int length) => _length = length;

            public override Memory<byte> Memory => CreateMemory(_length);

            public override Span<byte> GetSpan() => throw new NotSupportedException();

            public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();

            public override void Unpin() => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
            }
        }
    }
}
