using System;
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
    }
}
