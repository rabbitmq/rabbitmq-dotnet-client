#nullable enable

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;

namespace RabbitMQ.Client
{
    internal class RentedOutgoingMemory : IDisposable, IResettable
    {
        private static readonly ObjectPool<RentedOutgoingMemory> s_pool = ObjectPool.Create<RentedOutgoingMemory>();

        private bool _disposedValue;
        private byte[]? _rentedArray;
        private TaskCompletionSource<bool>? _sendCompletionSource;

        internal int Size => (int) Data.Length;

        public int RentedArraySize => _rentedArray?.Length ?? 0;

        internal ReadOnlySequence<byte> Data { get; private set; }

        /// <summary>
        /// Mark the data as sent.
        /// </summary>
        public void DidSend()
        {
            if (_sendCompletionSource is null)
            {
                Dispose();
                s_pool.Return(this);
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
                s_pool.Return(this);
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

            if (disposing)
            {
                if (_rentedArray != null)
                {
                    ClientArrayPool.Return(_rentedArray);
                    Data = default;
                    _rentedArray = null;
                }
            }

            _disposedValue = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        bool IResettable.TryReset()
        {
            if (!_disposedValue)
            {
                return false;
            }

            _disposedValue = false;
            _rentedArray = default;
            Data = default;
            _sendCompletionSource = default;
            return true;
        }

        public static RentedOutgoingMemory GetAndInitialize(ReadOnlySequence<byte> mem, byte[]? buffer = null, bool waitSend = false)
        {
            var rented = s_pool.Get();

            rented.Data = mem;
            rented._rentedArray = buffer;

            if (waitSend)
            {
                rented._sendCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            return rented;
        }

        public static RentedOutgoingMemory GetAndInitialize(ReadOnlyMemory<byte> mem, byte[]? buffer = null, bool waitSend = false)
        {
            return GetAndInitialize(new ReadOnlySequence<byte>(mem), buffer, waitSend);
        }
    }
}
