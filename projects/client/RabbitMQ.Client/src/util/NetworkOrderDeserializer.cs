using System;
using System.Buffers.Binary;
using System.Linq;
using System.Runtime.InteropServices;

namespace RabbitMQ.Util
{
    internal static class NetworkOrderDeserializer
    {
        internal static double ReadDouble(Memory<byte> memory, int offset = 0)
        {
            if (memory.Length - offset < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to decode Double from memory.");
            }

            long val = BinaryPrimitives.ReadInt64BigEndian((offset > 0 ? memory.Slice(offset) : memory).Span);
            return BitConverter.Int64BitsToDouble(val);
        }

        internal static short ReadInt16(Memory<byte> memory, int offset = 0)
        {
            if (memory.Length - offset < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to decode Int16 from memory.");
            }

            return BinaryPrimitives.ReadInt16BigEndian((offset > 0 ? memory.Slice(offset) : memory).Span);
        }

        internal static int ReadInt32(Memory<byte> memory, int offset = 0)
        {
            if (memory.Length - offset < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to decode Int32 from memory.");
            }

            return BinaryPrimitives.ReadInt32BigEndian((offset > 0 ? memory.Slice(offset) : memory).Span);
        }

        internal static long ReadInt64(Memory<byte> memory, int offset = 0)
        {
            if (memory.Length - offset < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to decode Int64 from memory.");
            }

            return BinaryPrimitives.ReadInt64BigEndian((offset > 0 ? memory.Slice(offset) : memory).Span);
        }

        internal static float ReadSingle(Memory<byte> memory, int offset = 0)
        {
            if (memory.Length - offset < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to decode Single from memory.");
            }

            Memory<byte> bytes = memory.Slice(offset, 4);
            if (BitConverter.IsLittleEndian)
            {
                bytes.Span.Reverse();
            }

            if (MemoryMarshal.TryGetArray(bytes, out ArraySegment<byte> segment))
            {
                return BitConverter.ToSingle(segment.Array, segment.Offset);
            }

            throw new InvalidOperationException("Unable to get ArraySegment from memory.");
        }

        internal static ushort ReadUInt16(Memory<byte> memory, int offset = 0)
        {
            if (memory.Length - offset < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to decode UInt16 from memory.");
            }

            return BinaryPrimitives.ReadUInt16BigEndian((offset > 0 ? memory.Slice(offset) : memory).Span);
        }

        internal static uint ReadUInt32(Memory<byte> memory, int offset = 0)
        {
            if (memory.Length - offset < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to decode UInt32 from memory.");
            }

            return BinaryPrimitives.ReadUInt32BigEndian((offset > 0 ? memory.Slice(offset) : memory).Span);
        }

        internal static ulong ReadUInt64(Memory<byte> memory, int offset = 0)
        {
            if (memory.Length - offset < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to decode UInt64 from memory.");
            }

            return BinaryPrimitives.ReadUInt64BigEndian((offset > 0 ? memory.Slice(offset) : memory).Span);
        }
    }
}
