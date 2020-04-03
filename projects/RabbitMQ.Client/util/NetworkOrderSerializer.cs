using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RabbitMQ.Util
{
    internal static class NetworkOrderSerializer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteDouble(Memory<byte> memory, double val)
        {
            if (memory.Length < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to write Double to memory.");
            }

            long tempVal = BitConverter.DoubleToInt64Bits(val);
            SerializeInt64(memory.Span, tempVal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteInt16(Memory<byte> memory, short val)
        {
            if (memory.Length < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to write Int16 to memory.");
            }

            SerializeInt16(memory.Span, val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteInt32(Memory<byte> memory, int val)
        {
            if (memory.Length < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to write Int32 to memory.");
            }

            SerializeInt32(memory.Span, val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteInt64(Memory<byte> memory, long val)
        {
            if (memory.Length < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to write Int64 to memory.");
            }

            SerializeInt64(memory.Span, val);
        }        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteSingle(Memory<byte> memory, float val)
        {
            if (memory.Length < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to write Single to memory.");
            }

            int tempVal = Unsafe.As<float, int>(ref val);
            SerializeInt32(memory.Span, tempVal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteUInt16(Memory<byte> memory, ushort val)
        {
            if (memory.Length < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to write UInt16 to memory.");
            }

            SerializeUInt16(memory.Span, val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteUInt32(Memory<byte> memory, uint val)
        {
            if (memory.Length < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to write UInt32 to memory.");
            }

            SerializeUInt32(memory.Span, val);
        }

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteUInt64(Memory<byte> memory, ulong val)
        {
            if (memory.Length < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Insufficient length to write UInt64 from memory.");
            }

            SerializeUInt64(memory.Span, val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SerializeInt16(Span<byte> memory, short val)
        {
            SerializeUInt16(memory, (ushort)val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SerializeUInt16(Span<byte> memory, ushort val)
        {
            memory[0] = (byte)((val >> 8) & 0xFF);
            memory[1] = (byte)(val & 0xFF);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SerializeInt32(Span<byte> memory, int val)
        {
            SerializeUInt32(memory, (uint)val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SerializeUInt32(Span<byte> memory, uint val)
        {
            memory[0] = (byte)((val >> 24) & 0xFF);
            memory[1] = (byte)((val >> 16) & 0xFF);
            memory[2] = (byte)((val >> 8) & 0xFF);
            memory[3] = (byte)(val & 0xFF);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SerializeInt64(Span<byte> memory, long val)
        {
            SerializeUInt64(memory, (ulong)val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SerializeUInt64(Span<byte> memory, ulong val)
        {
            memory[0] = (byte)((val >> 56) & 0xFF);
            memory[1] = (byte)((val >> 48) & 0xFF);
            memory[2] = (byte)((val >> 40) & 0xFF);
            memory[3] = (byte)((val >> 32) & 0xFF);
            memory[4] = (byte)((val >> 24) & 0xFF);
            memory[5] = (byte)((val >> 16) & 0xFF);
            memory[6] = (byte)((val >> 8) & 0xFF);
            memory[7] = (byte)(val & 0xFF);
        }
    }
}
