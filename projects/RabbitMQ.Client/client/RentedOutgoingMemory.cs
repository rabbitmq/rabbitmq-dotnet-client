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
        private TaskCompletionSource<bool>? _sendCompletionSource;

        internal ReadOnlySequence<byte> Data;
        internal byte[]? RentedArray;

        internal int Size => (int) Data.Length;

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

            if (disposing && RentedArray != null)
            {
                ClientArrayPool.Return(RentedArray);
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
            RentedArray = default;
            _sendCompletionSource = default;
            Data = default;
            return true;
        }

        public static RentedOutgoingMemory Create(ReadOnlySequence<byte> mem, byte[] buffer, bool waitSend = false)
        {
            var rented = s_pool.Get();

            rented.Data = mem;
            rented.RentedArray = buffer;

            if (waitSend)
            {
                rented._sendCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            return rented;
        }

        public static RentedOutgoingMemory Create(ReadOnlyMemory<byte> mem, byte[] buffer, bool waitSend = false)
        {
            return Create(new ReadOnlySequence<byte>(mem), buffer, waitSend);
        }
    }
}
