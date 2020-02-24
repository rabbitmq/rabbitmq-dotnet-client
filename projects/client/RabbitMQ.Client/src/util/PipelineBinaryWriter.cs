using System;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Util
{
    public class PipelineBinaryWriter
    {
        private readonly PipeWriter _writer;

        public PipelineBinaryWriter(PipeWriter reader)
        {
            _writer = reader;
        }

        public void Write(double val)
        {
            SerializeDoubleBigEndian(val, _writer.GetSpan(sizeof(double)));
            _writer.Advance(sizeof(double));
        }

        public void Write(float val)
        {
            SerializeSingleBigEndian(val, _writer.GetSpan(sizeof(float)));
            _writer.Advance(sizeof(float));
        }

        public void Write(short val)
        {
            SerializeInt16BigEndian(val, _writer.GetSpan(sizeof(short)));
            _writer.Advance(sizeof(short));
        }

        public void Write(ushort val)
        {
            SerializeUInt16BigEndian(val, _writer.GetSpan(sizeof(ushort)));
            _writer.Advance(sizeof(ushort));
        }

        public void Write(int val)
        {
            SerializeInt32BigEndian(val, _writer.GetSpan(sizeof(int)));
            _writer.Advance(sizeof(int));
        }

        public void Write(uint val)
        {
            SerializeUInt32BigEndian(val, _writer.GetSpan(sizeof(uint)));
            _writer.Advance(sizeof(uint));
        }

        public void Write(long val)
        {
            SerializeInt64BigEndian(val, _writer.GetSpan(sizeof(long)));
            _writer.Advance(sizeof(long));
        }

        public void Write(ulong val)
        {
            SerializeUInt64BigEndian(val, _writer.GetSpan(sizeof(ulong)));
            _writer.Advance(sizeof(ulong));
        }

        public void Write(byte val)
        {
            _writer.GetSpan(1)[0] = val;
            _writer.Advance(1);
        }

        public void Write(ReadOnlySpan<byte> val)
        {
            int bytesLeft = val.Length;
            while (bytesLeft > 0)
            {
                Memory<byte> memory = _writer.GetMemory(bytesLeft);
                int bytesToCopy = Math.Min(memory.Length, bytesLeft);
                val.Slice(0, bytesToCopy).CopyTo(memory.Span);
                _writer.Advance(bytesToCopy);
                bytesLeft -= bytesToCopy;
                Flush();
            }
        }

        public ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
        {
            return _writer.FlushAsync(cancellationToken);
        }

        public FlushResult Flush()
        {
            var flushTask = _writer.FlushAsync();
            if (flushTask.IsCompletedSuccessfully)
            {
                return flushTask.Result;
            }
            flushTask.AsTask().Wait();
            return flushTask.Result;
        }

        private void SerializeDoubleBigEndian(double val, Span<byte> memory)
        {
            BinaryPrimitives.WriteInt64BigEndian(memory, BitConverter.DoubleToInt64Bits(val));
        }

        private void SerializeSingleBigEndian(float val, Span<byte> memory)
        {
            Span<float> bytes = stackalloc float[1];
            Span<byte> result = MemoryMarshal.Cast<float, byte>(bytes);
            result.Reverse();
            result.CopyTo(memory);
        }

        private void SerializeInt16BigEndian(short val, Span<byte> memory)
        {
            BinaryPrimitives.WriteInt16BigEndian(memory, val);
        }

        private void SerializeUInt16BigEndian(ushort val, Span<byte> memory)
        {
            BinaryPrimitives.WriteUInt16BigEndian(memory, val);
        }

        private void SerializeInt32BigEndian(int val, Span<byte> memory)
        {
            BinaryPrimitives.WriteInt32BigEndian(memory, val);
        }

        private void SerializeUInt32BigEndian(uint val, Span<byte> memory)
        {
            BinaryPrimitives.WriteUInt32BigEndian(memory, val);
        }

        private void SerializeInt64BigEndian(long val, Span<byte> memory)
        {
            BinaryPrimitives.WriteInt64BigEndian(memory, val);
        }

        private void SerializeUInt64BigEndian(ulong val, Span<byte> memory)
        {
            BinaryPrimitives.WriteUInt64BigEndian(memory, val);
        }
    }
}
