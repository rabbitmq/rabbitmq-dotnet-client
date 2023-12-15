using System;
using System.Buffers;

namespace RabbitMQ.Client;

internal ref struct ChunkedSequence<T>
{
    private ReadOnlyChunk _first;
    private ReadOnlyChunk _current;

    private bool _changed;
    private ReadOnlySequence<T>? _sequence;

    public ChunkedSequence()
    {
        _first = _current = null;
        _sequence = null;
        _changed = false;
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
            _first = _current = new ReadOnlyChunk(memory);
        }
        else
        {
            _current = _current.Append(memory);
        }

        _changed = true;
    }

    internal ReadOnlySequence<T> GetSequence()
    {
        if (_changed)
        {
            _sequence = new ReadOnlySequence<T>(_first, 0, _current, _current.Memory.Length);
        }
        else
        {
            _sequence ??= new ReadOnlySequence<T>();
        }

        return _sequence.Value;
    }

    private sealed class ReadOnlyChunk : ReadOnlySequenceSegment<T>
    {
        public ReadOnlyChunk(ReadOnlyMemory<T> memory)
        {
            Memory = memory;
        }

        public ReadOnlyChunk Append(ReadOnlyMemory<T> memory)
        {
            ReadOnlyChunk nextChunk = new(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };

            Next = nextChunk;
            return nextChunk;
        }
    }
}
