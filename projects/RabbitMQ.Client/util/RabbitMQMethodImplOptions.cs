using System.Runtime.CompilerServices;

namespace RabbitMQ
{
    internal struct RabbitMQMethodImplOptions
    {
#if NETCOREAPP
        public const short Optimized = (short)(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization);
#else
        public const short Optimized = (short)MethodImplOptions.AggressiveInlining;
#endif
    }
}
