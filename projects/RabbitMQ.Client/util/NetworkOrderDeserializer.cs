using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace RabbitMQ.Util
{
    internal static class NetworkOrderDeserializer
    {
        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        internal static double ReadDouble(ReadOnlySpan<byte> span, int offset)
        {
            long result = Unsafe.ReadUnaligned<long>(ref Unsafe.AsRef(span[offset]));
            return BitConverter.Int64BitsToDouble(BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(result) : result);
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        internal static short ReadInt16(ReadOnlySpan<byte> span, int offset)
        {
            short result = Unsafe.ReadUnaligned<short>(ref Unsafe.AsRef(span[offset]));
            return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(result) : result;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        internal static int ReadInt32(ReadOnlySpan<byte> span, int offset)
        {
            int result = Unsafe.ReadUnaligned<int>(ref Unsafe.AsRef(span[offset]));
            return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(result) : result;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        internal static long ReadInt64(ReadOnlySpan<byte> span, int offset)
        {
            long result = Unsafe.ReadUnaligned<long>(ref Unsafe.AsRef(span[offset]));
            return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(result) : result;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        internal static float ReadSingle(ReadOnlySpan<byte> span, int offset)
        {
            uint result = Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef(span[offset]));
            if (BitConverter.IsLittleEndian)
            {
                result = BinaryPrimitives.ReverseEndianness(result);
            }

            return Unsafe.As<uint, float>(ref result);
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        internal static ushort ReadUInt16(ReadOnlySpan<byte> span, int offset)
        {
            ushort result = Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef(span[offset]));
            return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(result) : result;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        internal static uint ReadUInt32(ReadOnlySpan<byte> span, int offset)
        {
            uint result = Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef(span[offset]));
            return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(result) : result;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        internal static ulong ReadUInt64(ReadOnlySpan<byte> span, int offset)
        {
            ulong result = Unsafe.ReadUnaligned<ulong>(ref Unsafe.AsRef(span[offset]));
            return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(result) : result;
        }
    }
}
