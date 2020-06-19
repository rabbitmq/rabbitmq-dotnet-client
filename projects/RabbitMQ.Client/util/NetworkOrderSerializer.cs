using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace RabbitMQ.Util
{
    internal static class NetworkOrderSerializer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteDouble(Span<byte> span, double val)
        {
            long tempVal = BitConverter.DoubleToInt64Bits(val);
            BinaryPrimitives.WriteInt64BigEndian(span, tempVal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteInt16(Span<byte> span, short val) =>
            BinaryPrimitives.WriteInt16BigEndian(span, val);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteInt32(Span<byte> span, int val) =>
            BinaryPrimitives.WriteInt32BigEndian(span, val);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteInt64(Span<byte> span, long val) =>
            BinaryPrimitives.WriteInt64BigEndian(span, val);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteSingle(Span<byte> span, float val)
        {
            int tempVal = Unsafe.As<float, int>(ref val);
            BinaryPrimitives.WriteInt32BigEndian(span, tempVal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteUInt16(Span<byte> span, ushort val) =>
            BinaryPrimitives.WriteUInt16BigEndian(span, val);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteUInt32(Span<byte> span, uint val) =>
            BinaryPrimitives.WriteUInt32BigEndian(span, val);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteUInt64(Span<byte> span, ulong val)
        {
            if (span.Length < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "Insufficient length to write UInt64 from memory.");
            }

            BinaryPrimitives.WriteUInt64BigEndian(span, val);
        }
    }
}
