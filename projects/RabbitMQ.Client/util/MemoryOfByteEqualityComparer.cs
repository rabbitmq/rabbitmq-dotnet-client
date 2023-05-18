using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RabbitMQ.Util;

internal sealed class MemoryOfByteEqualityComparer : IEqualityComparer<ReadOnlyMemory<byte>>
{
    public static MemoryOfByteEqualityComparer Instance { get; } = new MemoryOfByteEqualityComparer();

    public bool Equals(ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right)
    {
        return left.Span.SequenceEqual(right.Span);
    }

    public int GetHashCode(ReadOnlyMemory<byte> value)
    {
#if NETSTANDARD
            unchecked
            {
                int hashCode = 0;
                var longPart = MemoryMarshal.Cast<byte, long>(value.Span);
                foreach (long item in longPart)
                {
                    hashCode = (hashCode * 397) ^ item.GetHashCode();
                }

                foreach (int item in value.Span.Slice(longPart.Length * 8))
                {
                    hashCode = (hashCode * 397) ^ item.GetHashCode();
                }

                return hashCode;
            }
#else
        HashCode result = default;
        result.AddBytes(value.Span);
        return result.ToHashCode();
#endif
    }
}
