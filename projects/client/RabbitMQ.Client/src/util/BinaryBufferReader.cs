using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RabbitMQ.Util
{
    public class BinaryBufferReader
    {
        // Not particularly efficient. To be more efficient, we could
        // reuse BinaryReader's implementation details: m_buffer and
        // FillBuffer, if they weren't private
        // members. Private/protected claim yet another victim, film
        // at 11. (I could simply cut-n-paste all that good code from
        // BinaryReader, but two wrongs do not make a right)

        /// <summary>
        /// Construct a NetworkBinaryReader over the given input stream.
        /// </summary>
        public BinaryBufferReader(ReadOnlyMemory<byte> input)
        {
            _input = input;
            _position = 0;
        }

        private int _position;
        private ReadOnlyMemory<byte> _input;

        public ReadOnlySpan<byte> Unreaded => _input.Span.Slice(_position);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            _position += count;
        }

        public byte ReadByte()
        {
            byte result = Unreaded[0];
            Advance(sizeof(byte));
            return result;
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
#if NETSTANDARD2_1
        public double ReadDouble()
        {
            double result;
            if (BitConverter.IsLittleEndian)
            {
                result = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(Unreaded));
            }
            else
            {
                result = BitConverter.ToDouble(Unreaded);
            }
            Advance(sizeof(double));
            return result;
        }
#else
        public double ReadDouble()
        {
            double result;
            if (BitConverter.IsLittleEndian)
            {
                result = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(Unreaded));
            }
            else
            {
                byte[] bytes = null;
                try
                {
                    bytes = ArrayPool<byte>.Shared.Rent(sizeof(double));
                    Unreaded.Slice(0, sizeof(double)).CopyTo(bytes);
                    result = BitConverter.ToDouble(bytes, 0);
                }
                finally
                {
                    if (bytes != null)
                    {
                        ArrayPool<byte>.Shared.Return(bytes);
                    }
                }
            }
            Advance(sizeof(double));
            return result;
        }
#endif

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public short ReadInt16()
        {
            short result = BinaryPrimitives.ReadInt16BigEndian(Unreaded);
            Advance(sizeof(short));
            return result;
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public int ReadInt32()
        {
            int result = BinaryPrimitives.ReadInt32BigEndian(Unreaded);
            Advance(sizeof(int));
            return result;
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public long ReadInt64()
        {
            long result = BinaryPrimitives.ReadInt64BigEndian(Unreaded);
            Advance(sizeof(long));
            return result;
        }


        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
#if NETSTANDARD2_1
        public float ReadSingle()
        {
            ReadOnlySpan<byte> rospan = Unreaded.Slice(0, sizeof(float));
            Span<byte> span = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(rospan), rospan.Length);
            span.Reverse();
            float result = BitConverter.ToSingle(span);
            Advance(sizeof(float));
            return result;
        }
#else
        public float ReadSingle()
        {
            byte[] bytes = null;
            try
            {
                bytes = ArrayPool<byte>.Shared.Rent(sizeof(float));
                var span = bytes.AsSpan(0, sizeof(float));
                Unreaded.Slice(0, sizeof(float)).CopyTo(span);
                span.Reverse();
                float result = BitConverter.ToSingle(bytes, 0);
                Advance(sizeof(float));
                return result;
            }
            finally
            {
                if (bytes != null)
                {
                    ArrayPool<byte>.Shared.Return(bytes);
                }
            }
        }
#endif
        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public ushort ReadUInt16()
        {
            ushort result = BinaryPrimitives.ReadUInt16BigEndian(Unreaded);
            Advance(sizeof(ushort));
            return result;
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public uint ReadUInt32()
        {
            uint result = BinaryPrimitives.ReadUInt32BigEndian(Unreaded);
            Advance(sizeof(uint));
            return result;
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public ulong ReadUInt64()
        {
            ulong result = BinaryPrimitives.ReadUInt64BigEndian(Unreaded);
            Advance(sizeof(ulong));
            return result;
        }

        public sbyte ReadSByte()
        {
            sbyte result = (sbyte)Unreaded[0];
            Advance(sizeof(sbyte));
            return result;
        }

        public byte[] ReadBytes(int count)
        {
            byte[] result = Unreaded.Slice(0, count).ToArray();
            Advance(result.Length);
            return result;
        }

        public int ReadBytes(Span<byte> span)
        {
            int count = span.Length;
            if (Unreaded.Length < span.Length)
            {
                count = Unreaded.Length;
            }
            Unreaded.Slice(0, count).CopyTo(span);
            Advance(count);
            return count;
        }
    }
}
