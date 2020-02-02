using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Pipelines.Sockets.Unofficial.Arenas;

namespace RabbitMQ.Util
{
    public class PipelineBinaryWriter
    {
        private PipeWriter _writer;

        public PipelineBinaryWriter(PipeWriter reader)
        {
            _writer = reader;
        }

        public void Write(double val)
        {
            SerializeDoubleBigEndian(val, _writer.GetSpan(8));
            _writer.Advance(8);
        }

        public void Write(float val)
        {
            SerializeSingleBigEndian(val, _writer.GetSpan(4));
            _writer.Advance(4);
        }

        public void Write(short val)
        {
            SerializeInt16BigEndian(val, _writer.GetSpan(2));
            _writer.Advance(2);
        }

        public void Write(ushort val)
        {
            SerializeUInt16BigEndian(val, _writer.GetSpan(2));
            _writer.Advance(2);
        }

        public void Write(int val)
        {
            SerializeInt32BigEndian(val, _writer.GetSpan(4));
            _writer.Advance(4);
        }

        public void Write(uint val)
        {
            SerializeUInt32BigEndian(val, _writer.GetSpan(2));
            _writer.Advance(4);
        }

        public void Write(long val)
        {
            Span<byte> array = stackalloc byte[8];
            _writer.Write(SerializeInt64BigEndian(val, array));
        }

        public void Write(ulong val)
        {
            SerializeUInt64BigEndian(val, _writer.GetSpan(8));
            _writer.Advance(8);
        }

        public void Write(byte[] val)
        {
            Write(val, 0, val.Length);
        }

        public void Write(byte val)
        {
            _writer.GetSpan(1)[0] = val;
            _writer.Advance(1);
        }

        public void Write(Span<byte> val, int offset, int length)
        {
            int bytesLeft = length;
            while (bytesLeft > 0)
            {
                var memory = _writer.GetSpan(bytesLeft);
                int bytesToCopy = Math.Min(memory.Length, bytesLeft);
                val.Slice(offset + (length - bytesLeft), bytesToCopy).CopyTo(memory);
                _writer.Advance(bytesToCopy);
                bytesLeft -= bytesToCopy;
            }
        }

        public ValueTask<FlushResult> FlushAsync()
        {
            return _writer.FlushAsync();
        }

        public FlushResult Flush()
        {
            return _writer.FlushAsync().GetAwaiter().GetResult();
        }

        private Span<byte> SerializeDoubleBigEndian(double val, Span<byte> memory)
        {
            BinaryPrimitives.WriteInt64BigEndian(memory, BitConverter.DoubleToInt64Bits(val));
            return memory;
        }

        private Span<byte> SerializeSingleBigEndian(float val, Span<byte> memory)
        {
            Span<float> bytes = stackalloc float[1];
            Span<byte> result = MemoryMarshal.Cast<float, byte>(bytes);
            result.Reverse();
            result.CopyTo(memory);
            return memory;
        }

        private Span<byte> SerializeInt16BigEndian(short val, Span<byte> memory)
        {
            BinaryPrimitives.WriteInt16BigEndian(memory, val);
            return memory;
        }

        private Span<byte> SerializeUInt16BigEndian(ushort val, Span<byte> memory)
        {
            BinaryPrimitives.WriteUInt16BigEndian(memory, val);
            return memory;
        }

        private Span<byte> SerializeInt32BigEndian(int val, Span<byte> memory)
        {
            BinaryPrimitives.WriteInt32BigEndian(memory, val);
            return memory;
        }

        private Span<byte> SerializeUInt32BigEndian(uint val, Span<byte> memory)
        {
            BinaryPrimitives.WriteUInt32BigEndian(memory, val);
            return memory;
        }

        private Span<byte> SerializeInt64BigEndian(long val, Span<byte> memory)
        {
            BinaryPrimitives.WriteInt64BigEndian(memory, val);
            return memory;
        }

        private Span<byte> SerializeUInt64BigEndian(ulong val, Span<byte> memory)
        {
            BinaryPrimitives.WriteUInt64BigEndian(memory, val);
            return memory;
        }
    }
}
