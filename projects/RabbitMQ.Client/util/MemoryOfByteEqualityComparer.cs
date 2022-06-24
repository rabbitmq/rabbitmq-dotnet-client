using System;
using System.Collections.Generic;

namespace RabbitMQ.Util;

public sealed class MemoryOfByteEqualityComparer : IEqualityComparer<ReadOnlyMemory<byte>>
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
                foreach (byte item in value.Span)
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
