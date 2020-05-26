using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RabbitMQ.Util
{
    internal static class NetworkOrderSerializer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteDouble(Span<byte> span, double val)
        {
#if NETFRAMEWORK
            if (span.Length < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to write Double to memory.");
            }

            long tempVal = BitConverter.DoubleToInt64Bits(val);
            span[0] = (byte)((tempVal >> 56) & 0xFF);
            span[1] = (byte)((tempVal >> 48) & 0xFF);
            span[2] = (byte)((tempVal >> 40) & 0xFF);
            span[3] = (byte)((tempVal >> 32) & 0xFF);
            span[4] = (byte)((tempVal >> 24) & 0xFF);
            span[5] = (byte)((tempVal >> 16) & 0xFF);
            span[6] = (byte)((tempVal >> 8) & 0xFF);
            span[7] = (byte)(tempVal & 0xFF);
#elif NETSTANDARD
            long tempVal = BitConverter.DoubleToInt64Bits(val);
            BinaryPrimitives.WriteInt64BigEndian(span, tempVal);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteInt16(Span<byte> span, short val)
        {
#if NETFRAMEWORK
            if (span.Length < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to write Int16 to memory.");
            }

            span[0] = (byte)((val >> 8) & 0xFF);
            span[1] = (byte)(val & 0xFF);
#elif NETSTANDARD
            BinaryPrimitives.WriteInt16BigEndian(span, val);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteInt32(Span<byte> span, int val)
        {
#if NETFRAMEWORK
            if (span.Length < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to write Int32 to memory.");
            }

            span[0] = (byte)((val >> 24) & 0xFF);
            span[1] = (byte)((val >> 16) & 0xFF);
            span[2] = (byte)((val >> 8) & 0xFF);
            span[3] = (byte)(val & 0xFF);
#elif NETSTANDARD
            BinaryPrimitives.WriteInt32BigEndian(span, val);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteInt64(Span<byte> span, long val)
        {
#if NETFRAMEWORK
            if (span.Length < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to write Int64 to memory.");
            }

            span[0] = (byte)((val >> 56) & 0xFF);
            span[1] = (byte)((val >> 48) & 0xFF);
            span[2] = (byte)((val >> 40) & 0xFF);
            span[3] = (byte)((val >> 32) & 0xFF);
            span[4] = (byte)((val >> 24) & 0xFF);
            span[5] = (byte)((val >> 16) & 0xFF);
            span[6] = (byte)((val >> 8) & 0xFF);
            span[7] = (byte)(val & 0xFF);
#elif NETSTANDARD
            BinaryPrimitives.WriteInt64BigEndian(span, val);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteSingle(Span<byte> span, float val)
        {
#if NETFRAMEWORK
            if (span.Length < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to write Single to memory.");
            }

            int tempVal = Unsafe.As<float, int>(ref val);
            span[0] = (byte)((tempVal >> 24) & 0xFF);
            span[1] = (byte)((tempVal >> 16) & 0xFF);
            span[2] = (byte)((tempVal >> 8) & 0xFF);
            span[3] = (byte)(tempVal & 0xFF);
#elif NETSTANDARD
            int tempVal = Unsafe.As<float, int>(ref val);
            BinaryPrimitives.WriteInt32BigEndian(span, tempVal);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteUInt16(Span<byte> span, ushort val)
        {
#if NETFRAMEWORK
            if (span.Length < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to write UInt16 to memory.");
            }

            span[0] = (byte)((val >> 8) & 0xFF);
            span[1] = (byte)(val & 0xFF);
#elif NETSTANDARD
            BinaryPrimitives.WriteUInt16BigEndian(span, val);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteUInt32(Span<byte> span, uint val)
        {
#if NETFRAMEWORK
            if (span.Length < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to write UInt32 to memory.");
            }

            span[0] = (byte)((val >> 24) & 0xFF);
            span[1] = (byte)((val >> 16) & 0xFF);
            span[2] = (byte)((val >> 8) & 0xFF);
            span[3] = (byte)(val & 0xFF);
#elif NETSTANDARD
            BinaryPrimitives.WriteUInt32BigEndian(span, val);
#endif
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteUInt64(Span<byte> span, ulong val)
        {
            if (span.Length < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to write UInt64 from memory.");
            }

#if NETFRAMEWORK
            span[0] = (byte)((val >> 56) & 0xFF);
            span[1] = (byte)((val >> 48) & 0xFF);
            span[2] = (byte)((val >> 40) & 0xFF);
            span[3] = (byte)((val >> 32) & 0xFF);
            span[4] = (byte)((val >> 24) & 0xFF);
            span[5] = (byte)((val >> 16) & 0xFF);
            span[6] = (byte)((val >> 8) & 0xFF);
            span[7] = (byte)(val & 0xFF);
#elif NETSTANDARD
            BinaryPrimitives.WriteUInt64BigEndian(span, val);
#endif
        }
    }
}
