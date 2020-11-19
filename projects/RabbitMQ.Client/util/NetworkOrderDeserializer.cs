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
            return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(span));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static short ReadInt16(ReadOnlySpan<byte> span)
        {
            return BinaryPrimitives.ReadInt16BigEndian(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadInt32(ReadOnlySpan<byte> span)
        {
            return BinaryPrimitives.ReadInt32BigEndian(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long ReadInt64(ReadOnlySpan<byte> span)
        {
            return BinaryPrimitives.ReadInt64BigEndian(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ReadSingle(ReadOnlySpan<byte> span)
        {
            return BitConverter.Int32BitsToSingle(ReadInt32(span));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort ReadUInt16(ReadOnlySpan<byte> span)
        {
            return BinaryPrimitives.ReadUInt16BigEndian(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ReadUInt32(ReadOnlySpan<byte> span)
        {
            return BinaryPrimitives.ReadUInt32BigEndian(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong ReadUInt64(ReadOnlySpan<byte> span)
        {
            return BinaryPrimitives.ReadUInt64BigEndian(span);
        }
    }
}
