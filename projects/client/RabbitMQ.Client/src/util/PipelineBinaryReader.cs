using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Util
{
    public class PipelineBinaryReader
    {
        private readonly PipeReader _reader;
        private SequencePosition _examined;
        private SequencePosition _consumed;
        private ReadOnlySequence<byte> _buffer;
        private bool _isCanceled;
        private bool _isCompleted;
        private bool _hasMessage;

        public PipelineBinaryReader(PipeReader reader)
        {
            _reader = reader;
        }

        public ValueTask ReadAsync(FrameContentReader reader, CancellationToken cancellationToken = default)
        {
            if (_hasMessage)
            {
                ThrowHelper.MissedAdvance();
            }

            // If this is the very first read, then make it go async since we have no data
            if (_consumed.GetObject() == null)
            {
                return DoAsyncRead(reader, cancellationToken);
            }

            // We have a buffer, test to see if there's any message left in the buffer
            if (TryParseMessage(reader, _buffer))
            {
                _hasMessage = true;
                return default;
            }
            else
            {
                // We couldn't parse the message so advance the input so we can read
                _reader.AdvanceTo(_consumed, _examined);
            }

            if (_isCompleted)
            {
                _consumed = default;
                _examined = default;

                // If we're complete then short-circuit
                if (!_buffer.IsEmpty)
                {
                    ThrowHelper.ConnectionTerminated();
                }

                return default;
            }

            return DoAsyncRead(reader, cancellationToken);
        }

        private ValueTask DoAsyncRead(FrameContentReader reader, CancellationToken cancellationToken)
        {
            while (true)
            {
                ValueTask<ReadResult> readTask = _reader.ReadAsync(cancellationToken);
                ReadResult result;
                if (readTask.IsCompletedSuccessfully)
                {
                    result = readTask.Result;
                }
                else
                {
                    return ContinueDoAsyncRead(readTask, reader, cancellationToken);
                }

                (bool shouldContinue, bool hasMessage) = TrySetMessage(result, reader);
                if (hasMessage)
                {
                    return default;
                }
                else if (!shouldContinue)
                {
                    break;
                }
            }

            return default;
        }

        private async ValueTask ContinueDoAsyncRead(ValueTask<ReadResult> readTask, FrameContentReader reader, CancellationToken cancellationToken)
        {
            while (true)
            {
                ReadResult result = await readTask;

                (bool shouldContinue, bool hasMessage) = TrySetMessage(result, reader);
                if (hasMessage)
                {
                    return;
                }
                else if (!shouldContinue)
                {
                    break;
                }

                readTask = _reader.ReadAsync(cancellationToken);
            }

            return;
        }

        private (bool ShouldContinue, bool HasMessage) TrySetMessage(ReadResult result, FrameContentReader reader)
        {
            _buffer = result.Buffer;
            _isCanceled = result.IsCanceled;
            _isCompleted = result.IsCompleted;
            _consumed = _buffer.Start;
            _examined = _buffer.End;

            if (_isCanceled)
            {
                return (false, false);
            }

            if (TryParseMessage(reader, _buffer))
            {
                _hasMessage = true;
                return (false, true);
            }
            else
            {
                _reader.AdvanceTo(_consumed, _examined);
            }

            if (_isCompleted)
            {
                _consumed = default;
                _examined = default;

                if (!_buffer.IsEmpty)
                {
                    ThrowHelper.ConnectionTerminated();
                }

                return (false, false);
            }

            return (true, false);
        }

        private bool TryParseMessage(FrameContentReader reader, in ReadOnlySequence<byte> buffer)
        {
            return reader.TryParseMessage(buffer, ref _consumed, ref _examined);
        }

        public void Advance()
        {
            _isCanceled = false;

            if (!_hasMessage)
            {
                return;
            }

            _buffer = _buffer.Slice(_consumed);

            _hasMessage = false;
        }
    }
}
