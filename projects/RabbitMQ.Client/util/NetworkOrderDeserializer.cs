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
        internal static int ReadInt32(ReadOnlySequence<byte> buffer)
        {
            if (buffer.First.Length >= 4)
            {
                return ReadInt32(buffer.First.Span);
            }

            Span<byte> span = stackalloc byte[4];
            buffer.Slice(0, 4).CopyTo(span);
            return ReadInt32(span);
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
        internal static ushort ReadUInt16(ref byte source)
        {
            return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<ushort>(ref source)) : Unsafe.ReadUnaligned<ushort>(ref source);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort ReadUInt16(ReadOnlySequence<byte> buffer)
        {
            if (2 <= buffer.First.Length)
            {
                return ReadUInt16(buffer.First.Span);
            }
            else
            {
                Span<byte> span = stackalloc byte[2];
                buffer.Slice(0, 2).CopyTo(span);
                return ReadUInt16(span);
            }
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
