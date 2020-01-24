using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RabbitMQ.Util
{
    public class PipelineBinaryReader
    {
        private PipeReader _reader;

        public PipelineBinaryReader(PipeReader reader)
        {
            _reader = reader;
        }

        public async ValueTask<double> ReadDoubleBigEndianAsync()
        {
            ReadResult result = await _reader.ReadAsync().ConfigureAwait(false);
            while (result.Buffer.Length < 8)
            {
                if (result.Buffer.Length > 0)
                {
                    _reader.AdvanceTo(result.Buffer.Start);
                }
                await Task.Yield();
                result = await _reader.ReadAsync().ConfigureAwait(false);
            }

            ReadOnlySequence<byte> slice = result.Buffer.Slice(0, 8);
            double returnValue = slice.ReadDoubleBigEndian();
            _reader.AdvanceTo(slice.End);
            return returnValue;
        }

        public async ValueTask<float> ReadSingleBigEndianAsync()
        {
            ReadResult result = await _reader.ReadAsync().ConfigureAwait(false);
            while (result.Buffer.Length < 4)
            {
                if (result.Buffer.Length > 0)
                {
                    _reader.AdvanceTo(result.Buffer.Start);
                }
                await Task.Yield();
                result = await _reader.ReadAsync().ConfigureAwait(false);
            }

            ReadOnlySequence<byte> slice = result.Buffer.Slice(0, 4);
            float returnValue = slice.ReadSingleBigEndian();
            _reader.AdvanceTo(slice.End);
            return returnValue;
        }

        public async ValueTask<short> ReadInt16BigEndianAsync()
        {
            ReadResult result = await _reader.ReadAsync().ConfigureAwait(false);
            while (result.Buffer.Length < 2)
            {
                if (result.Buffer.Length > 0)
                {
                    _reader.AdvanceTo(result.Buffer.Start);
                }
                await Task.Yield();
                result = await _reader.ReadAsync().ConfigureAwait(false);
            }

            ReadOnlySequence<byte> slice = result.Buffer.Slice(0, 2);
            short returnValue = slice.ReadInt16BigEndian();
            _reader.AdvanceTo(slice.End);
            return returnValue;
        }

        public async ValueTask<ushort> ReadUInt16BigEndianAsync()
        {
            ReadResult result = await _reader.ReadAsync().ConfigureAwait(false);
            while (result.Buffer.Length < 2)
            {
                if (result.Buffer.Length > 0)
                {
                    _reader.AdvanceTo(result.Buffer.Start);
                }
                await Task.Yield();
                result = await _reader.ReadAsync().ConfigureAwait(false);
            }

            ReadOnlySequence<byte> slice = result.Buffer.Slice(0, 2);
            ushort returnValue = slice.ReadUInt16BigEndian();
            _reader.AdvanceTo(slice.End);
            return returnValue;
        }


        public async ValueTask<int> ReadInt32BigEndianAsync()
        {
            ReadResult result = await _reader.ReadAsync().ConfigureAwait(false);
            while (result.Buffer.Length < 4)
            {
                if (result.Buffer.Length > 0)
                {
                    _reader.AdvanceTo(result.Buffer.Start);
                }
                await Task.Yield();
                result = await _reader.ReadAsync().ConfigureAwait(false);
            }

            ReadOnlySequence<byte> slice = result.Buffer.Slice(0, 4);
            int returnValue = slice.ReadInt32BigEndian();
            _reader.AdvanceTo(slice.End);
            return returnValue;
        }

        public async ValueTask<uint> ReadUInt32BigEndianAsync()
        {
            ReadResult result = await _reader.ReadAsync().ConfigureAwait(false);
            while (result.Buffer.Length < 4)
            {
                if (result.Buffer.Length > 0)
                {
                    _reader.AdvanceTo(result.Buffer.Start);
                }
                await Task.Yield();
                result = await _reader.ReadAsync().ConfigureAwait(false);
            }

            ReadOnlySequence<byte> slice = result.Buffer.Slice(0, 4);
            uint returnValue = slice.ReadUInt32BigEndian();
            _reader.AdvanceTo(slice.End);
            return returnValue;
        }

        public async ValueTask<long> ReadInt64BigEndianAsync()
        {
            ReadResult result = await _reader.ReadAsync().ConfigureAwait(false);
            while (result.Buffer.Length < 8)
            {
                if (result.Buffer.Length > 0)
                {
                    _reader.AdvanceTo(result.Buffer.Start);
                }
                await Task.Yield();
                result = await _reader.ReadAsync().ConfigureAwait(false);
            }

            ReadOnlySequence<byte> slice = result.Buffer.Slice(0, 8);
            long returnValue = slice.ReadInt64BigEndian();
            _reader.AdvanceTo(slice.End);
            return returnValue;
        }

        public async ValueTask<ulong> ReadUInt64BigEndianAsync()
        {
            ReadResult result = await _reader.ReadAsync().ConfigureAwait(false);
            while (result.Buffer.Length < 8)
            {
                if (result.Buffer.Length > 0)
                {
                    _reader.AdvanceTo(result.Buffer.Start);
                }
                await Task.Yield();
                result = await _reader.ReadAsync().ConfigureAwait(false);
            }

            ReadOnlySequence<byte> slice = result.Buffer.Slice(0, 8);
            ushort returnValue = slice.ReadUInt16BigEndian();
            _reader.AdvanceTo(slice.End);
            return returnValue;
        }

        public async Task<byte> ReadByteAsync()
        {
            ReadResult result = await _reader.ReadAsync().ConfigureAwait(false);
            while (result.Buffer.Length < 1)
            {
                await Task.Yield();
                result = await _reader.ReadAsync().ConfigureAwait(false);
            }

            ReadOnlySequence<byte> slice = result.Buffer.Slice(0, 1);
            byte returnValue = slice.ReadByte();
            _reader.AdvanceTo(slice.End);
            return returnValue;
        }

        public async Task ReadBytesAsync(byte[] buffer, int position, int length)
        {
            int bytesRemaining = length;
            while (bytesRemaining > 0)
            {
                ReadResult result = await _reader.ReadAsync().ConfigureAwait(false);
                while (result.Buffer.Length == 0)
                {
                    await Task.Yield();
                    result = await _reader.ReadAsync().ConfigureAwait(false);
                }

                if (result.Buffer.Length > bytesRemaining)
                {
                    ReadOnlySequence<byte> slice = result.Buffer.Slice(0, bytesRemaining);
                    slice.CopyTo(buffer.AsSpan(length - bytesRemaining, bytesRemaining));
                    _reader.AdvanceTo(slice.End);
                    bytesRemaining = 0;
                }
                else
                {
                    result.Buffer.CopyTo(buffer.AsSpan(length - bytesRemaining, (int)result.Buffer.Length));
                    bytesRemaining -= (int)result.Buffer.Length;
                    _reader.AdvanceTo(result.Buffer.End);
                }
            }
        }
    }

    public static class ReadOnlySequenceExtensions
    {
        public static double ReadDoubleBigEndian(this ReadOnlySequence<byte> slice)
        {
            return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(slice.GetSpan()));
        }

        public static float ReadSingleBigEndian(this ReadOnlySequence<byte> slice)
        {
            Span<byte> bytes = stackalloc byte[4];
            slice.CopyTo(bytes);
            bytes.Reverse();
            float result = MemoryMarshal.Cast<byte, float>(bytes)[0];
            return result;
        }

        public static short ReadInt16BigEndian(this ReadOnlySequence<byte> slice)
        {
            return BinaryPrimitives.ReadInt16BigEndian(slice.GetSpan());
        }

        public static ushort ReadUInt16BigEndian(this ReadOnlySequence<byte> slice)
        {
            return BinaryPrimitives.ReadUInt16BigEndian(slice.GetSpan());
        }

        public static int ReadInt32BigEndian(this ReadOnlySequence<byte> slice)
        {
            return BinaryPrimitives.ReadInt32BigEndian(slice.GetSpan());
        }

        public static uint ReadUInt32BigEndian(this ReadOnlySequence<byte> slice)
        {
            return BinaryPrimitives.ReadUInt32BigEndian(slice.GetSpan());
        }

        public static long ReadInt64BigEndian(this ReadOnlySequence<byte> slice)
        {
            return BinaryPrimitives.ReadInt64BigEndian(slice.GetSpan());
        }

        public static ulong ReadUInt64BigEndian(this ReadOnlySequence<byte> slice)
        {
            return BinaryPrimitives.ReadUInt64BigEndian(slice.GetSpan());
        }

        public static byte ReadByte(this ReadOnlySequence<byte> slice)
        {
            return slice.First.Span[0];
        }

        public static void ReadBytes(this ReadOnlySequence<byte> slice, Span<byte> span)
        {
            slice.CopyTo(span);
        }

        private static ReadOnlySpan<byte> GetSpan(this ReadOnlySequence<byte> slice)
        {
            return slice.IsSingleSegment ? slice.First.Span : slice.ToArray();
        }
    }
}
