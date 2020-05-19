using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;

namespace RabbitMQ.Util
{
    internal static class StreamExtensions
    {
        internal static int Read(this Stream stream, Memory<byte> memory)
        {
            if (MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment))
            {
                return stream.Read(segment.Array, segment.Offset, segment.Count);
            }

            throw new InvalidOperationException("Unable to get array segment from memory.");
        }

        internal static int Read(this Stream stream, Span<byte> buffer)
        {
            byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                int numRead = stream.Read(sharedBuffer, 0, buffer.Length);
                new Span<byte>(sharedBuffer, 0, numRead).CopyTo(buffer);
                return numRead;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(sharedBuffer);
            }
        }
    }
}
