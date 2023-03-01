using System;
using System.Buffers;
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
            uint num = BinaryPrimitives.ReadUInt32BigEndian(span);
            return Unsafe.As<uint, float>(ref num);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort ReadUInt16(ReadOnlySpan<byte> span)
        {
            return BinaryPrimitives.ReadUInt16BigEndian(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort ReadUInt16(ReadOnlySequence<byte> buffer)
        {
            if (!BinaryPrimitives.TryReadUInt16BigEndian(buffer.First.Span, out ushort value))
            {
                Span<byte> bytes = stackalloc byte[2];
                buffer.Slice(0, 2).CopyTo(bytes);
                value = BinaryPrimitives.ReadUInt16BigEndian(bytes);
            }

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ReadUInt32(ReadOnlySpan<byte> span)
        {
            return BinaryPrimitives.ReadUInt32BigEndian(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadInt32(ReadOnlySequence<byte> buffer)
        {
            if (!BinaryPrimitives.TryReadInt32BigEndian(buffer.First.Span, out int value))
            {
                Span<byte> bytes = stackalloc byte[4];
                buffer.Slice(0, 4).CopyTo(bytes);
                value = BinaryPrimitives.ReadInt32BigEndian(bytes);
            }

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong ReadUInt64(ReadOnlySpan<byte> span)
        {
            return BinaryPrimitives.ReadUInt64BigEndian(span);
        }
    }
}
