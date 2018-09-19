using System;
using System.Collections.Generic;

namespace RabbitMQ.Util
{
    internal class ArrayPool<TElement>
    {
        public static ArrayPool<TElement> Shared { get; } = new ArrayPool<TElement>();

        private const int minArrayLength = 256;

        private readonly ArrayPoolSegment[] segments;

        public ArrayPool(int arraysPerSegment = 8, int maxArrayLength = 1024 * 1024)
        {
            segments = InitializeSegments(arraysPerSegment, maxArrayLength);
        }

        public TElement[] Rent(int minimumLength)
        {
            var segmentIndex = GetSegmentIndex(minimumLength);
            if (segmentIndex >= segments.Length)
            {
                return new TElement[minimumLength];
            }

            for (var i = segmentIndex; i < segments.Length; ++i)
            {
                if (segments[i].TryRent(out var buffer))
                {
                    return buffer;
                }
            }

            return segments[segmentIndex].Rent();
        }

        public void Return(TElement[] array)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            if (array.Length == 0)
            {
                return;
            }

            var segmentIndex = GetSegmentIndex(array.Length);
            if (segmentIndex >= segments.Length)
            {
                return;
            }

            segments[segmentIndex].Return(array);
        }

        private static ArrayPoolSegment[] InitializeSegments(int arraysPerSegment, int maxArrayLength)
        {
            var segments = new List<ArrayPoolSegment>();
            for (var length = minArrayLength; length <= maxArrayLength; length *= 2)
            {
                segments.Add(new ArrayPoolSegment(arraysPerSegment, length));
            }
            return segments.ToArray();
        }
        
        private static int GetSegmentIndex(int minimumLength)
        {
            var index = 0;
            for (var length = minArrayLength; length < minimumLength; length *= 2)
            {
                ++index;
            }
            return index;
        }

        private class ArrayPoolSegment
        {
            private readonly Pool<TElement[]> pool;
            private readonly int arrayLength;

            public ArrayPoolSegment(int numberOfArrays, int arrayLength)
            {
                pool = new Pool<TElement[]>(numberOfArrays, () => new TElement[arrayLength]);
                this.arrayLength = arrayLength;
            }

            public bool TryRent(out TElement[] array)
            {
                return pool.TryRent(out array);
            }

            public TElement[] Rent()
            {
                return pool.Rent();
            }

            public void Return(TElement[] array)
            {
                if (array.Length != arrayLength)
                {
                    throw new ArgumentOutOfRangeException(nameof(array.Length), array.Length, null);
                }

                pool.Return(array);
            }
        }
    }
}
