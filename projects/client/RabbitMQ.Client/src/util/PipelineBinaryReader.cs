using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
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

        public async ValueTask<double> ReadDoubleBigEndianAsync(CancellationToken cancellationToken = default)
        {
            ReadOnlySequence<byte> slice = await _reader.ReadMinimumSize<double>(cancellationToken).ConfigureAwait(false);
            double returnValue = slice.ReadDoubleBigEndian();
            _reader.AdvanceTo(slice.End);
            return returnValue;
        }

        public async ValueTask<float> ReadSingleBigEndianAsync(CancellationToken cancellationToken = default)
        {
            ReadOnlySequence<byte> slice = await _reader.ReadMinimumSize<float>(cancellationToken).ConfigureAwait(false);
            float returnValue = slice.ReadSingleBigEndian();
            _reader.AdvanceTo(slice.End);
            return returnValue;
        }

        public async ValueTask<short> ReadInt16BigEndianAsync(CancellationToken cancellationToken = default)
        {
            ReadOnlySequence<byte> slice = await _reader.ReadMinimumSize<short>(cancellationToken).ConfigureAwait(false);
            short returnValue = slice.ReadInt16BigEndian();
            _reader.AdvanceTo(slice.End);
            return returnValue;
        }

        public async ValueTask<ushort> ReadUInt16BigEndianAsync(CancellationToken cancellationToken = default)
        {
            ReadOnlySequence<byte> slice = await _reader.ReadMinimumSize<ushort>(cancellationToken).ConfigureAwait(false);
            ushort returnValue = slice.ReadUInt16BigEndian();
            _reader.AdvanceTo(slice.End);
            return returnValue;
        }

        public async ValueTask<int> ReadInt32BigEndianAsync(CancellationToken cancellationToken = default)
        {
            ReadOnlySequence<byte> slice = await _reader.ReadMinimumSize<int>(cancellationToken).ConfigureAwait(false);
            int returnValue = slice.ReadInt32BigEndian();
            _reader.AdvanceTo(slice.End);
            return returnValue;
        }

        public async ValueTask<uint> ReadUInt32BigEndianAsync(CancellationToken cancellationToken = default)
        {
            ReadOnlySequence<byte> slice = await _reader.ReadMinimumSize<uint>(cancellationToken).ConfigureAwait(false);
            uint returnValue = slice.ReadUInt32BigEndian();
            _reader.AdvanceTo(slice.End);
            return returnValue;
        }

        public async ValueTask<long> ReadInt64BigEndianAsync(CancellationToken cancellationToken = default)
        {
            ReadOnlySequence<byte> slice = await _reader.ReadMinimumSize<long>(cancellationToken).ConfigureAwait(false);
            long returnValue = slice.ReadInt64BigEndian();
            _reader.AdvanceTo(slice.End);
            return returnValue;
        }

        public async ValueTask<ulong> ReadUInt64BigEndianAsync(CancellationToken cancellationToken = default)
        {
            ReadOnlySequence<byte> slice = await _reader.ReadMinimumSize<ulong>().ConfigureAwait(false);
            ushort returnValue = slice.ReadUInt16BigEndian();
            _reader.AdvanceTo(slice.End);
            return returnValue;
        }

        public async ValueTask<byte> ReadByteAsync(CancellationToken cancellationToken = default)
        {
            ReadOnlySequence<byte> slice = await _reader.ReadMinimumSize<byte>(cancellationToken).ConfigureAwait(false);
            byte returnValue = slice.ReadByte();
            _reader.AdvanceTo(slice.End);
            return returnValue;
        }

        public async ValueTask ReadBytesAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int bytesRemaining = buffer.Length;
            while (bytesRemaining > 0)
            {
                ReadResult result = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                while (result.Buffer.Length < 1)
                {
                    _reader.AdvanceTo(result.Buffer.Start);
                    result = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                }

                if (result.Buffer.Length > bytesRemaining)
                {
                    ReadOnlySequence<byte> slice = result.Buffer.Slice(0, bytesRemaining);
                    slice.CopyTo(buffer.Span.Slice(buffer.Length - bytesRemaining, bytesRemaining));
                    _reader.AdvanceTo(slice.End);
                    return;
                }
                else
                {
                    result.Buffer.CopyTo(buffer.Span.Slice(buffer.Length - bytesRemaining, (int)result.Buffer.Length));
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

    public static class PipeReaderExtensions
    {
        public static async ValueTask<ReadOnlySequence<byte>> ReadMinimumSize<T>(this PipeReader reader, CancellationToken cancellationToken = default) where T : struct
        {
            int size = Unsafe.SizeOf<T>();
            ReadResult result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            while (result.Buffer.Length < size)
            {
                reader.AdvanceReaderIfNotEmpty(result);
                result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }

            return result.Buffer.Slice(0, size);
        }

        public static void AdvanceReaderIfNotEmpty(this PipeReader reader, ReadResult result)
        {
            if (!result.Buffer.IsEmpty)
            {
                reader.AdvanceTo(result.Buffer.Start);
            }
        }
    }
}
