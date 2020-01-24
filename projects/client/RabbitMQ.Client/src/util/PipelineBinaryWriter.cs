using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RabbitMQ.Util
{
    public class PipelineBinaryWriter
    {
        private PipeWriter _writer;

        public PipelineBinaryWriter(PipeWriter reader)
        {
            _writer = reader;
        }

        public async ValueTask<FlushResult> WriteAsync(double val)
        {
            return await _writer.WriteAsync(SerializeDoubleBigEndian(val)).ConfigureAwait(false);
        }

        public void Write(double val)
        {
            _writer.Write(SerializeDoubleBigEndian(val).Span);
        }

        public async ValueTask<FlushResult> WriteAsync(float val)
        {
            return await _writer.WriteAsync(SerializeSingleBigEndian(val)).ConfigureAwait(false);
        }

        public void Write(float val)
        {
            _writer.Write(SerializeSingleBigEndian(val).Span);
        }

        public async ValueTask<FlushResult> WriteAsync(short val)
        {
            return await _writer.WriteAsync(SerializeInt16BigEndian(val)).ConfigureAwait(false);
        }

        public void Write(short val)
        {
            _writer.Write(SerializeInt16BigEndian(val).Span);
        }

        public async ValueTask<FlushResult> WriteAsync(ushort val)
        {
            return await _writer.WriteAsync(SerializeUInt16BigEndian(val)).ConfigureAwait(false);
        }

        public void Write(ushort val)
        {
            _writer.Write(SerializeUInt16BigEndian(val).Span);
        }

        public async ValueTask<FlushResult> WriteAsync(int val)
        {
            return await _writer.WriteAsync(SerializeInt32BigEndian(val)).ConfigureAwait(false);
        }

        public void Write(int val)
        {
            _writer.Write(SerializeInt32BigEndian(val).Span);
        }

        public async ValueTask<FlushResult> WriteAsync(uint val)
        {
            return await _writer.WriteAsync(SerializeUInt32BigEndian(val)).ConfigureAwait(false);
        }

        public void Write(uint val)
        {
            _writer.Write(SerializeUInt32BigEndian(val).Span);
        }

        public async ValueTask<FlushResult> WriteAsync(long val)
        {
            return await _writer.WriteAsync(SerializeInt64BigEndian(val)).ConfigureAwait(false);
        }

        public void Write(long val)
        {
            _writer.Write(SerializeInt64BigEndian(val).Span);
        }

        public async ValueTask<FlushResult> WriteAsync(ulong val)
        {
            return await _writer.WriteAsync(SerializeUInt64BigEndian(val)).ConfigureAwait(false);
        }

        public void Write(ulong val)
        {
            _writer.Write(SerializeUInt64BigEndian(val).Span);
        }

        public async ValueTask<FlushResult> WriteAsync(byte[] val)
        {
            Memory<byte> memory = _writer.GetMemory(val.Length).Slice(0, val.Length);
            val.CopyTo(memory.Span);
            return await _writer.WriteAsync(memory).ConfigureAwait(false);
        }

        public void Write(byte[] val)
        {
            _writer.Write(val);
        }

        public async ValueTask<FlushResult> WriteAsync(byte val)
        {
            Memory<byte> memory = _writer.GetMemory(1);
            memory.Span[0] = val;
            return await _writer.WriteAsync(memory.Slice(0, 1)).ConfigureAwait(false);
        }

        public void Write(byte val)
        {
            Span<byte> singleByte = stackalloc byte[1];
            singleByte[0] = val;
            _writer.Write(singleByte);
        }

        public async ValueTask<FlushResult> WriteAsync(byte[] val, int offset, int length)
        {
            Memory<byte> memory = _writer.GetMemory(length).Slice(0, length);
            val.AsSpan(offset, length).CopyTo(memory.Span);
            return await _writer.WriteAsync(memory).ConfigureAwait(false);
        }

        public void Write(Span<byte> val, int offset, int length)
        {
            Memory<byte> memory = _writer.GetMemory(length).Slice(0, length);
            val.Slice(offset, length).CopyTo(memory.Span);
            _writer.Write(memory.Span);
        }

        public ValueTask<FlushResult> FlushAsync()
        {
            return _writer.FlushAsync();
        }

        public FlushResult Flush()
        {
            return _writer.FlushAsync().GetAwaiter().GetResult();
        }

        private Memory<byte> SerializeDoubleBigEndian(double val)
        {
            Memory<byte> memory = _writer.GetMemory(8).Slice(0, 8);
            BinaryPrimitives.WriteInt64BigEndian(memory.Span, BitConverter.DoubleToInt64Bits(val));
            return memory;
        }

        private Memory<byte> SerializeSingleBigEndian(float val)
        {
            Memory<byte> memory = _writer.GetMemory(4).Slice(0, 4);
            Span<float> bytes = stackalloc float[1];
            Span<byte> result = MemoryMarshal.Cast<float, byte>(bytes);
            result.Reverse();
            result.CopyTo(memory.Span);
            return memory;
        }

        private Memory<byte> SerializeInt16BigEndian(short val)
        {
            Memory<byte> memory = _writer.GetMemory(2).Slice(0, 2);
            BinaryPrimitives.WriteInt16BigEndian(memory.Span, val);
            return memory;
        }

        private Memory<byte> SerializeUInt16BigEndian(ushort val)
        {
            Memory<byte> memory = _writer.GetMemory(2).Slice(0, 2);
            BinaryPrimitives.WriteUInt16BigEndian(memory.Span, val);
            return memory;
        }

        private Memory<byte> SerializeInt32BigEndian(int val)
        {
            Memory<byte> memory = _writer.GetMemory(4).Slice(0, 4);
            BinaryPrimitives.WriteInt32BigEndian(memory.Span, val);
            return memory;
        }

        private Memory<byte> SerializeUInt32BigEndian(uint val)
        {
            Memory<byte> memory = _writer.GetMemory(4).Slice(0, 4);
            BinaryPrimitives.WriteUInt32BigEndian(memory.Span, val);
            return memory;
        }

        private Memory<byte> SerializeInt64BigEndian(long val)
        {
            Memory<byte> memory = _writer.GetMemory(8).Slice(0, 8);
            BinaryPrimitives.WriteInt64BigEndian(memory.Span, val);
            return memory;
        }

        private Memory<byte> SerializeUInt64BigEndian(ulong val)
        {
            Memory<byte> memory = _writer.GetMemory(8).Slice(0, 8);
            BinaryPrimitives.WriteUInt64BigEndian(memory.Span, val);
            return memory;
        }
    }
}
