using System;
using System.Buffers.Binary;

namespace RabbitMQ.Util
{
    internal static class NetworkOrderSerializer
    {
        internal static void WriteDouble(Memory<byte> memory, double val)
        {
            if (memory.Length < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to write Double to memory.");
            }

            BinaryPrimitives.WriteInt64BigEndian(memory.Span, BitConverter.DoubleToInt64Bits(val));
        }

        internal static void WriteInt16(Memory<byte> memory, short val)
        {
            if (memory.Length < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to write Int16 to memory.");
            }

            BinaryPrimitives.WriteInt16BigEndian(memory.Span, val);
        }

        internal static void WriteInt32(Memory<byte> memory, int val)
        {
            if (memory.Length < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to write Int32 to memory.");
            }

            BinaryPrimitives.WriteInt32BigEndian(memory.Span, val);
        }

        internal static void WriteInt64(Memory<byte> memory, long val)
        {
            if (memory.Length < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to write Int64 to memory.");
            }

            BinaryPrimitives.WriteInt64BigEndian(memory.Span, val);
        }

        internal static void WriteSingle(Memory<byte> memory, float val)
        {
            if (memory.Length < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to write Single to memory.");
            }

            byte[] bytes = BitConverter.GetBytes(val);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            bytes.AsMemory().CopyTo(memory);
        }

        internal static void WriteUInt16(Memory<byte> memory, ushort val)
        {
            if (memory.Length < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to write UInt16 to memory.");
            }

            BinaryPrimitives.WriteUInt16BigEndian(memory.Span, val);
        }

        internal static void WriteUInt32(Memory<byte> memory, uint val)
        {
            if (memory.Length < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to write UInt32 to memory.");
            }

            BinaryPrimitives.WriteUInt32BigEndian(memory.Span, val);
        }

        internal static void WriteUInt64(Memory<byte> memory, ulong val)
        {
            if (memory.Length < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to write UInt64 from memory.");
            }

            BinaryPrimitives.WriteUInt64BigEndian(memory.Span, val);
        }
    }
}
