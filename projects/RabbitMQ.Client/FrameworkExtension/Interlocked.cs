// Note:
// The code in this file is inspired by the code in `dotnet/runtime`, in this file:
// src/coreclr/nativeaot/System.Private.CoreLib/src/System/Threading/Interlocked.cs
// 
// The license from that file is as follows:
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.



using System.Runtime.CompilerServices;

namespace RabbitMQ.Client
{
#if NETSTANDARD
    // TODO GH-1308 class should be called "Interlocked" to be used by other code
    internal static class InterlockedExtensions
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
