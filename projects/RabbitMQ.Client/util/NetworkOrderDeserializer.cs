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
            ulong val = BinaryPrimitives.ReadUInt64BigEndian(span);
            return Unsafe.As<ulong, double>(ref val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static short ReadInt16(ReadOnlySpan<byte> span) =>
            BinaryPrimitives.ReadInt16BigEndian(span);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadInt32(ReadOnlySpan<byte> span) =>
            BinaryPrimitives.ReadInt32BigEndian(span);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long ReadInt64(ReadOnlySpan<byte> span) =>
            BinaryPrimitives.ReadInt64BigEndian(span);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ReadSingle(ReadOnlySpan<byte> span)
        {
            uint num = BinaryPrimitives.ReadUInt32BigEndian(span);
            return Unsafe.As<uint, float>(ref num);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort ReadUInt16(ReadOnlySpan<byte> span) =>
            BinaryPrimitives.ReadUInt16BigEndian(span);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ReadUInt32(ReadOnlySpan<byte> span) =>
            BinaryPrimitives.ReadUInt32BigEndian(span);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong ReadUInt64(ReadOnlySpan<byte> span) =>
            BinaryPrimitives.ReadUInt64BigEndian(span);
    }
}
