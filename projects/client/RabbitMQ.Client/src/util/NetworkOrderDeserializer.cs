using System;
using System.Buffers.Binary;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RabbitMQ.Util
{
    internal static class NetworkOrderDeserializer
    {
        internal static double ReadDouble(ReadOnlyMemory<byte> memory) => ReadDouble(memory.Span);

        internal static double ReadDouble(ReadOnlySpan<byte> span)
        {
            if (span.Length < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode Double from memory.");
            }

            long val = BinaryPrimitives.ReadInt64BigEndian(span);
            return Unsafe.As<long, double>(ref val);
        }

        internal static short ReadInt16(ReadOnlyMemory<byte> memory) => ReadInt16(memory.Span);

        internal static short ReadInt16(ReadOnlySpan<byte> span)
        {
            if (span.Length < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode Int16 from memory.");
            }

            return BinaryPrimitives.ReadInt16BigEndian(span);
        }

        internal static int ReadInt32(ReadOnlyMemory<byte> memory) => ReadInt32(memory.Span);

        internal static int ReadInt32(ReadOnlySpan<byte> span)
        {
            if (span.Length < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode Int32 from memory.");
            }

            return BinaryPrimitives.ReadInt32BigEndian(span);
        }

        internal static long ReadInt64(ReadOnlyMemory<byte> memory) => ReadInt64(memory.Span);

        internal static long ReadInt64(ReadOnlySpan<byte> span)
        {
            if (span.Length < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode Int64 from memory.");
            }

            return BinaryPrimitives.ReadInt64BigEndian(span);
        }

        internal static float ReadSingle(ReadOnlyMemory<byte> memory) => ReadSingle(memory.Span);

        internal static float ReadSingle(ReadOnlySpan<byte> span)
        {
            if (span.Length < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode Single from memory.");
            }

            int num = BinaryPrimitives.ReadInt32BigEndian(span);
            return Unsafe.As<int, float>(ref num);
        }

        internal static ushort ReadUInt16(Memory<byte> memory) => ReadUInt16(memory.Span);

        internal static ushort ReadUInt16(ReadOnlySpan<byte> span)
        {
            if (span.Length < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode UInt16 from memory.");
            }

            return BinaryPrimitives.ReadUInt16BigEndian(span);
        }

        internal static uint ReadUInt32(Memory<byte> memory) => ReadUInt32(memory.Span);

        internal static uint ReadUInt32(ReadOnlySpan<byte> span)
        {
            if (span.Length < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode UInt32 from memory.");
            }

            return BinaryPrimitives.ReadUInt32BigEndian(span);
        }

        internal static ulong ReadUInt64(Memory<byte> memory) => ReadUInt64(memory.Span);

        internal static ulong ReadUInt64(ReadOnlySpan<byte> span)
        {
            if (span.Length < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode UInt64 from memory.");
            }

            return BinaryPrimitives.ReadUInt64BigEndian(span);
        }
    }
}
