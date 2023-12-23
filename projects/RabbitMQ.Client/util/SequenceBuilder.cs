#nullable enable
using System;
using System.Buffers;
using System.Runtime.InteropServices;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Util;

internal ref struct SequenceBuilder<T>
{
    private Segment? _first;
    private Segment? _current;

    public SequenceBuilder()
    {
        _first = _current = null;
    }

    public void Append(ReadOnlySequence<T> sequence)
    {
        SequencePosition pos = sequence.Start;
        while (sequence.TryGet(ref pos, out ReadOnlyMemory<T> mem))
        {
            Append(mem);
        }
    }

    public void Append(ReadOnlyMemory<T> memory)
    {
        if (_current == null)
        {
            _first = _current = new Segment(memory);
        }
        else if (!_current.TryMerge(memory))
        {
            _current = _current.Append(memory);
        }
    }

    public ReadOnlySequence<T> Build()
    {
        if (_first == null || _current == null)
        {
            return default;
        }

        return new ReadOnlySequence<T>(_first, 0, _current, _current.Memory.Length);
    }

    private sealed class Segment : ReadOnlySequenceSegment<T>
    {
        public Segment(ReadOnlyMemory<T> memory)
        {
            Memory = memory;
        }

        /// <summary>
        /// Try to merge the next memory into this chunk.
        /// </summary>
        /// <remarks>
        /// Used in <see cref="Framing.BodySegment.WriteTo"/> that can write the same array when the body is being copied.
        /// </remarks>
        /// <param name="next">The next memory.</param>
        /// <returns><c>true</c> if the memory was merged; otherwise <c>false</c>.</returns>
        public bool TryMerge(ReadOnlyMemory<T> next)
        {
            if (MemoryMarshal.TryGetArray(Memory, out ArraySegment<T> segment) &&
                MemoryMarshal.TryGetArray(next, out ArraySegment<T> nextSegment) &&
                segment.Array == nextSegment.Array &&
                nextSegment.Offset == segment.Offset + segment.Count)
            {
                Memory = segment.Array.AsMemory(segment.Offset, segment.Count + nextSegment.Count);
                return true;
            }

            return false;
        }

        public Segment Append(ReadOnlyMemory<T> memory)
        {
            Segment nextChunk = new(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };

            Next = nextChunk;
            return nextChunk;
        }
    }
}
