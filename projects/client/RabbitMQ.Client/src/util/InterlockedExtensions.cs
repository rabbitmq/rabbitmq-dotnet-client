using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace RabbitMQ.Util
{
    public static class InterlockedEx
    {
        /// <summary>
        /// unsigned equivalent of <see cref="Interlocked.Increment(ref Int32)"/>
        /// </summary>
        public static ulong Increment(ref uint location)
        {
            int incrementedSigned = Interlocked.Increment(ref Unsafe.As<uint, int>(ref location));
            return Unsafe.As<int, uint>(ref incrementedSigned);
        }

        /// <summary>
        /// unsigned equivalent of <see cref="Interlocked.Increment(ref Int64)"/>
        /// </summary>
        public static ulong Increment(ref ulong location)
        {
            long incrementedSigned = Interlocked.Increment(ref Unsafe.As<ulong, long>(ref location));
            return Unsafe.As<long, ulong>(ref incrementedSigned);
        }

        /// <summary>
        /// unsigned equivalent of <see cref="Interlocked.Increment(ref Int32)"/>
        /// </summary>
        public static ulong Decrement(ref uint location)
        {
            int incrementedSigned = Interlocked.Decrement(ref Unsafe.As<uint, int>(ref location));
            return Unsafe.As<int, uint>(ref incrementedSigned);
        }

        /// <summary>
        /// unsigned equivalent of <see cref="Interlocked.Increment(ref Int64)"/>
        /// </summary>
        public static ulong Decrement(ref ulong location)
        {
            long incrementedSigned = Interlocked.Decrement(ref Unsafe.As<ulong, long>(ref location));
            return Unsafe.As<long, ulong>(ref incrementedSigned);
        }
    }
}
