using System;
using System.IO;
using System.Runtime.CompilerServices;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Util
{
    internal static class ThrowHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void InvalidProtocolHeader()
        {
            throw new MalformedFrameException("Invalid AMQP protocol header from server");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void PacketNotRecognized(int transportHigh, int transportLow, int serverMajor, int serverMinor)
        {
            throw new PacketNotRecognizedException(transportHigh, transportLow, serverMajor, serverMinor);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void InvalidFrameEndMarker(int endMarker)
        {
            throw new MalformedFrameException("Bad frame end marker: " + endMarker);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ConnectionTerminated()
        {
            throw new InvalidDataException("Connection terminated while reading a message.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void MissedAdvance()
        {
            throw new InvalidOperationException("Advance must be called before calling ReadAsync");
        }
        
    }
}
