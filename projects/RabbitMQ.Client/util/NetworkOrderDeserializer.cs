using System;
using System.Runtime.CompilerServices;

namespace RabbitMQ.Util
{
    internal static class NetworkOrderDeserializer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double ReadDouble(ReadOnlyMemory<byte> memory) => ReadDouble(memory.Span);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double ReadDouble(ReadOnlySpan<byte> span)
        {
            if (span.Length < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode Double from memory.");
            }

            ulong val = ReadUInt64(span);
            return Unsafe.As<ulong, double>(ref val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static short ReadInt16(ReadOnlyMemory<byte> memory) => ReadInt16(memory.Span);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static short ReadInt16(ReadOnlySpan<byte> span)
        {
            if (span.Length < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode Int16 from memory.");
            }

            return (short)ReadUInt16(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadInt32(ReadOnlyMemory<byte> memory) => ReadInt32(memory.Span);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadInt32(ReadOnlySpan<byte> span)
        {
            if (span.Length < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode Int32 from memory.");
            }

            return (int)ReadUInt32(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long ReadInt64(ReadOnlyMemory<byte> memory) => ReadInt64(memory.Span);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long ReadInt64(ReadOnlySpan<byte> span)
        {
            if (span.Length < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode Int64 from memory.");
            }

            return (long)ReadUInt64(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ReadSingle(ReadOnlyMemory<byte> memory) => ReadSingle(memory.Span);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ReadSingle(ReadOnlySpan<byte> span)
        {
            if (span.Length < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode Single from memory.");
            }

            uint num = ReadUInt32(span);
            return Unsafe.As<uint, float>(ref num);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort ReadUInt16(ReadOnlyMemory<byte> memory) => ReadUInt16(memory.Span);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort ReadUInt16(ReadOnlySpan<byte> span)
        {
            if (span.Length < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode UInt16 from memory.");
            }

            return (ushort)((span[0] << 8) | span[1]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ReadUInt32(ReadOnlyMemory<byte> memory) => ReadUInt32(memory.Span);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ReadUInt32(ReadOnlySpan<byte> span)
        {
            if (span.Length < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode UInt32 from memory.");
            }

            return (uint)((span[0] << 24) | (span[1] << 16) | (span[2] << 8) | span[3]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong ReadUInt64(ReadOnlyMemory<byte> memory) => ReadUInt64(memory.Span);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong ReadUInt64(ReadOnlySpan<byte> span)
        {
            if (span.Length < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode UInt64 from memory.");
            }

            uint num1 = (uint)((span[0] << 24) | (span[1] << 16) | (span[2] << 8) | span[3]);
            uint num2 = (uint)((span[4] << 24) | (span[5] << 16) | (span[6] << 8) | span[7]);
            return ((ulong)num1 << 32) | num2;
        }
    }
}
