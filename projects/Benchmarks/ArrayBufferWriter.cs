// We only need this if we aren't targeting .NET 6.0 or greater since it already exists there
#if !NET6_0_OR_GREATER
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace System.Buffers
{
    public class ArrayBufferWriter<T> : IBufferWriter<T>, IDisposable
    {
        private T[] _rentedBuffer;
        private int _written;
        private long _committed;

        private const int MinimumBufferSize = 256;

        public ArrayBufferWriter(int initialCapacity = MinimumBufferSize)
        {
            if (initialCapacity <= 0)
            {
                throw new ArgumentException(null, nameof(initialCapacity));
            }

            _rentedBuffer = ArrayPool<T>.Shared.Rent(initialCapacity);
            _written = 0;
            _committed = 0;
        }

        public Memory<T> WrittenMemory
        {
            get
            {
                CheckIfDisposed();

                return _rentedBuffer.AsMemory(0, _written);
            }
        }

        public Span<T> WrittenSpan
        {
            get
            {
                CheckIfDisposed();

                return _rentedBuffer.AsSpan(0, _written);
            }
        }

        public int BytesWritten
        {
            get
            {
                CheckIfDisposed();

                return _written;
            }
        }

        public long BytesCommitted
        {
            get
            {
                CheckIfDisposed();

                return _committed;
            }
        }

        public void Clear()
        {
            CheckIfDisposed();

            ClearHelper();
        }

        private void ClearHelper()
        {
            _rentedBuffer.AsSpan(0, _written).Clear();
            _written = 0;
        }

        public void Advance(int count)
        {
            CheckIfDisposed();

            if (count < 0)
                throw new ArgumentException(nameof(count));

            if (_written > _rentedBuffer.Length - count)
                throw new InvalidOperationException("Cannot advance past the end of the buffer.");

            _written += count;
        }

        // Returns the rented buffer back to the pool
        public void Dispose()
        {
            if (_rentedBuffer == null)
            {
                return;
            }

            ArrayPool<T>.Shared.Return(_rentedBuffer, clearArray: true);
            _rentedBuffer = null;
            _written = 0;
        }

        private void CheckIfDisposed()
        {
            if (_rentedBuffer == null)
                throw new ObjectDisposedException(nameof(ArrayBufferWriter<T>));
        }

        public Memory<T> GetMemory(int sizeHint = 0)
        {
            CheckIfDisposed();

            if (sizeHint < 0)
                throw new ArgumentException(nameof(sizeHint));

            CheckAndResizeBuffer(sizeHint);
            return _rentedBuffer.AsMemory(_written);
        }

        public Span<T> GetSpan(int sizeHint = 0)
        {
            CheckIfDisposed();

            if (sizeHint < 0)
                throw new ArgumentException(nameof(sizeHint));

            CheckAndResizeBuffer(sizeHint);
            return _rentedBuffer.AsSpan(_written);
        }

        private void CheckAndResizeBuffer(int sizeHint)
        {
            Debug.Assert(sizeHint >= 0);

            if (sizeHint == 0)
            {
                sizeHint = MinimumBufferSize;
            }

            int availableSpace = _rentedBuffer.Length - _written;

            if (sizeHint > availableSpace)
            {
                int growBy = sizeHint > _rentedBuffer.Length ? sizeHint : _rentedBuffer.Length;

                int newSize = checked(_rentedBuffer.Length + growBy);

                T[] oldBuffer = _rentedBuffer;

                _rentedBuffer = ArrayPool<T>.Shared.Rent(newSize);

                Debug.Assert(oldBuffer.Length >= _written);
                Debug.Assert(_rentedBuffer.Length >= _written);

                oldBuffer.AsSpan(0, _written).CopyTo(_rentedBuffer);
                ArrayPool<T>.Shared.Return(oldBuffer, clearArray: true);
            }

            Debug.Assert(_rentedBuffer.Length - _written > 0);
            Debug.Assert(_rentedBuffer.Length - _written >= sizeHint);
        }
    }
}
#endif
