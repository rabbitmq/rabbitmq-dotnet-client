using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace RabbitMQ.Util
{
    internal static class NetworkOrderDeserializer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double ReadDouble(ReadOnlySpan<byte> span)
        {
#if NETFRAMEWORK
            if (span.Length < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode Double from memory.");
            }
            ulong val = ReadUInt64(span);
#elif NETSTANDARD
            ulong val = BinaryPrimitives.ReadUInt64BigEndian(span);
#endif
            return Unsafe.As<ulong, double>(ref val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static short ReadInt16(ReadOnlySpan<byte> span)
        {
#if NETFRAMEWORK
            if (span.Length < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode Int16 from memory.");
            }

            return (short)ReadUInt16(span);
#elif NETSTANDARD
            return BinaryPrimitives.ReadInt16BigEndian(span);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadInt32(ReadOnlySpan<byte> span)
        {
#if NETFRAMEWORK
            if (span.Length < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode Int32 from memory.");
            }

            return (int)ReadUInt32(span);
#elif NETSTANDARD
            return BinaryPrimitives.ReadInt32BigEndian(span);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long ReadInt64(ReadOnlySpan<byte> span)
        {
#if NETFRAMEWORK
            if (span.Length < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode Int64 from memory.");
            }

            return (long)ReadUInt64(span);
#elif NETSTANDARD
            return BinaryPrimitives.ReadInt64BigEndian(span);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ReadSingle(ReadOnlySpan<byte> span)
        {
#if NETFRAMEWORK
            if (span.Length < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode Single from memory.");
            }

            uint num = ReadUInt32(span);
#elif NETSTANDARD
            uint num = BinaryPrimitives.ReadUInt32BigEndian(span);
#endif
            return Unsafe.As<uint, float>(ref num);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort ReadUInt16(ReadOnlySpan<byte> span)
        {
#if NETFRAMEWORK
            if (span.Length < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode UInt16 from memory.");
            }

            return (ushort)((span[0] << 8) | span[1]);
#elif NETSTANDARD
            return BinaryPrimitives.ReadUInt16BigEndian(span);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ReadUInt32(ReadOnlySpan<byte> span)
        {
#if NETFRAMEWORK
            if (span.Length < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode UInt32 from memory.");
            }

            return (uint)((span[0] << 24) | (span[1] << 16) | (span[2] << 8) | span[3]);
#elif NETSTANDARD
            return BinaryPrimitives.ReadUInt32BigEndian(span);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong ReadUInt64(ReadOnlySpan<byte> span)
        {
#if NETFRAMEWORK
            if (span.Length < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to decode UInt64 from memory.");
            }

            uint num1 = (uint)((span[0] << 24) | (span[1] << 16) | (span[2] << 8) | span[3]);
            uint num2 = (uint)((span[4] << 24) | (span[5] << 16) | (span[6] << 8) | span[7]);
            return ((ulong)num1 << 32) | num2;
#elif NETSTANDARD
            return BinaryPrimitives.ReadUInt64BigEndian(span);
#endif
        }
    }
}
