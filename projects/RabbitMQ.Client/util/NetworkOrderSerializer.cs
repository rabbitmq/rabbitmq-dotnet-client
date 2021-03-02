using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace RabbitMQ.Util
{
    internal static class NetworkOrderSerializer
    {
        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        internal static void WriteDouble(ref byte destination, double val)
        {
            long longVal = BitConverter.DoubleToInt64Bits(val);
            Unsafe.WriteUnaligned(ref destination, BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(longVal) : longVal);
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        internal static void WriteInt16(ref byte destination, short val)
        {
            Unsafe.WriteUnaligned(ref destination, BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(val) : val);
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        internal static void WriteInt32(ref byte destination, int val)
        {
            Unsafe.WriteUnaligned(ref destination, BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(val) : val);
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        internal static void WriteInt64(ref byte destination, long val)
        {
            Unsafe.WriteUnaligned(ref destination, BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(val) : val);
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        internal static void WriteSingle(ref byte destination, float val)
        {
            int tempVal = Unsafe.As<float, int>(ref val);
            Unsafe.WriteUnaligned(ref destination, BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(tempVal) : tempVal);
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        internal static void WriteUInt16(ref byte destination, ushort val)
        {
            Unsafe.WriteUnaligned(ref destination, BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(val) : val);
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        internal static void WriteUInt32(ref byte destination, uint val)
        {
            Unsafe.WriteUnaligned(ref destination, BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(val) : val);
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        internal static void WriteUInt64(ref byte destination, ulong val)
        {
            Unsafe.WriteUnaligned(ref destination, BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(val) : val);
        }
    }
}
