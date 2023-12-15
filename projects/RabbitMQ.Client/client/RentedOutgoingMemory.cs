#nullable enable

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace RabbitMQ.Client
{
    internal class RentedOutgoingMemory : IDisposable
    {
        private bool _disposedValue;
        private byte[]? _rentedArray;
        private TaskCompletionSource<bool>? _sendCompletionSource;
        private ReadOnlySequence<byte> _data;

        public RentedOutgoingMemory(ReadOnlyMemory<byte> data, byte[]? rentedArray = null, bool waitSend = false)
            : this(new ReadOnlySequence<byte>(data), rentedArray, waitSend)
        {
        }

        public RentedOutgoingMemory(ReadOnlySequence<byte> data, byte[]? rentedArray = null, bool waitSend = false)
        {
            _data = data;
            _rentedArray = rentedArray;

            if (waitSend)
            {
                _sendCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        internal int Size => (int)Data.Length;

        public int RentedArraySize => _rentedArray?.Length ?? 0;

        internal ReadOnlySequence<byte> Data
        {
            get
            {
                if (_disposedValue)
                {
                    throw new ObjectDisposedException(nameof(RentedOutgoingMemory));
                }

                return _data;
            }
        }

        /// <summary>
        /// Mark the data as sent.
        /// </summary>
        public void DidSend()
        {
            if (_sendCompletionSource is null)
            {
                Dispose();
            }
            else
            {
                _sendCompletionSource.SetResult(true);
            }
        }

        /// <summary>
        /// Wait for the data to be sent.
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> that completes when the data is sent.</returns>
        public ValueTask WaitForDataSendAsync()
        {
            return _sendCompletionSource is null ? default : WaitForFinishCore();

            async ValueTask WaitForFinishCore()
            {
                await _sendCompletionSource.Task.ConfigureAwait(false);
                Dispose();
            }
        }

        public void WriteTo(PipeWriter pipeWriter)
        {
            foreach (ReadOnlyMemory<byte> memory in Data)
            {
                pipeWriter.Write(memory.Span);
            }
        }

        private void Dispose(bool disposing)
        {
            if (_disposedValue)
            {
                return;
            }

            _disposedValue = true;

            if (disposing)
            {
                _data = default;

                if (_rentedArray != null)
                {
                    ClientArrayPool.Return(_rentedArray);
                    _rentedArray = null;
                }
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
