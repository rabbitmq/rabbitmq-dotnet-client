using System.Runtime.CompilerServices;

namespace RabbitMQ.Client
{
#if NETCOREAPP3_1 || NETSTANDARD
    internal static class Interlocked
    {
        public static ulong CompareExchange(ref ulong location1, ulong value, ulong comparand)
        {
            return (ulong)System.Threading.Interlocked.CompareExchange(ref Unsafe.As<ulong, long>(ref location1), (long)value, (long)comparand);
        }

        public static ulong Increment(ref ulong location1)
        {
            return (ulong)System.Threading.Interlocked.Add(ref Unsafe.As<ulong, long>(ref location1), 1L);
        }
    }
#endif
}
