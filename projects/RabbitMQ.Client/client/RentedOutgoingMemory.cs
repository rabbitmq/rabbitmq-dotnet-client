#nullable enable

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading.Tasks;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client
{
    internal sealed class RentedOutgoingMemory : IDisposable
    {
        private readonly TaskCompletionSource<bool>? _sendCompletionSource;
        private bool _disposedValue;
        private byte[]? _rentedArray;
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
        /// <returns><c>true</c> if the object can be disposed, <c>false</c> if the <see cref="SocketFrameHandler"/> is waiting for the data to be sent.</returns>
        public bool DidSend()
        {
            if (_sendCompletionSource is null)
            {
                return true;
            }

            _sendCompletionSource.SetResult(true);
            return false;
        }

        /// <summary>
        /// Wait for the data to be sent.
        /// </summary>
        /// <returns><c>true</c> if the data was sent and the object can be disposed.</returns>
        public ValueTask<bool> WaitForDataSendAsync()
        {
            return _sendCompletionSource is null ? new ValueTask<bool>(false) : WaitForFinishCore();

            async ValueTask<bool> WaitForFinishCore()
            {
                await _sendCompletionSource.Task.ConfigureAwait(false);
                return true;
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

            Debug.Assert(_sendCompletionSource is null or { Task.IsCompleted: true }, "The send task should be completed before disposing.");
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
        }
    }
}
