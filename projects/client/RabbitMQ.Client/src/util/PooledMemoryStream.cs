using System;
using System.Buffers;
using System.IO;

namespace RabbitMQ.Util
{
    internal class PooledMemoryStream : Stream
    {
        private static readonly byte[] m_empty_array = new byte[0];

        private readonly ArrayPool<byte> pool;

        private byte[] array = m_empty_array;
        private int length;
        private int position;

        public PooledMemoryStream() : this(ArrayPool<byte>.Shared)
        {
        }

        public PooledMemoryStream(ArrayPool<byte> pool)
        {
            this.pool = pool;
        }

        public override bool CanRead => false;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => length;

        public override long Position
        {
            get => position;
            set
            {
                if (value > int.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
                }

                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
                }

                position = (int)value;
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (offset < 0 || offset > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset, null);
            }

            switch (origin)
            {
                case SeekOrigin.Begin:
                    position = (int)offset;
                    break;
                case SeekOrigin.Current:
                {
                    var newPosition = position + (int)offset;
                    if (newPosition < 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(offset), offset, null);
                    }

                    position = newPosition;
                    break;
                }
                case SeekOrigin.End:
                {
                    var newPosition = length + (int)offset;
                    if (newPosition < 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(offset), offset, null);
                    }

                    position = newPosition;
                    break;
                }
                default:
                {
                    throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
                }
            }

            return position;
        }

        public override void SetLength(long value)
        {
            if (value < 0 || value > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }

            var newLength = (int)value;
            ReallocateArrayIfNeeded(newLength);

            if (position > newLength) position = newLength;
            length = newLength;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset, null);
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, null);
            }

            var newPosition = position + count;
            if (newPosition < 0)
            {
                throw new IOException("Stream was too long");
            }

            ReallocateArrayIfNeeded(newPosition);

            Buffer.BlockCopy(buffer, offset, array, position, count);

            if (length < newPosition) length = newPosition;
            position = newPosition;
        }

        private void ReallocateArrayIfNeeded(int minimumLength)
        {
            if (minimumLength <= array.Length) return;

            var newArray = pool.Rent(minimumLength);
            Buffer.BlockCopy(array, 0, newArray, 0, array.Length);
            pool.Return(array);
            array = newArray;
        }

        public ArraySegment<byte> GetBufferSegment()
        {
            return new ArraySegment<byte>(array, 0, length);
        }

        protected override void Dispose(bool disposing)
        {
            pool.Return(array);

            base.Dispose(disposing);
        }

        public override void WriteByte(byte value)
        {
            var newPosition = position + 1;
            if (newPosition < 0)
            {
                throw new IOException("Stream was too long");
            }

            ReallocateArrayIfNeeded(newPosition);

            array[position] = value;

            if (length < newPosition) length = newPosition;
            position = newPosition;
        }
    }
}
