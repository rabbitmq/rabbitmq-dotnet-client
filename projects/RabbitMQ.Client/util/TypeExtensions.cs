using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RabbitMQ
{
    internal static class TypeExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte GetStart(this ReadOnlySpan<byte> span)
        {
            return ref MemoryMarshal.GetReference(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte GetStart(this byte[] array)
        {
            return ref Unsafe.AsRef(array[0]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte GetStart(this Span<byte> span)
        {
            return ref MemoryMarshal.GetReference(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte GetOffset(this ReadOnlySpan<byte> span, int offset)
        {
            return ref span.GetStart().GetOffset(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte GetOffset(this Span<byte> span, int offset)
        {
            return ref span.GetStart().GetOffset(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte GetOffset(this ref byte source, int offset)
        {
            return ref Unsafe.Add(ref source, offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBitSet(this in byte value, byte bitPosition)
        {
            return (value & (1 << bitPosition)) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBit(this ref byte value, byte bitPosition)
        {
            value |= (byte)(1 << bitPosition);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ToByte(this ref bool source)
        {
            return Unsafe.As<bool, byte>(ref source);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ToBool(this ref byte source)
        {
            return Unsafe.As<byte, bool>(ref source);
        }
    }
}
