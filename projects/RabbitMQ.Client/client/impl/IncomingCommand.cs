using System;
using System.Buffers;

namespace RabbitMQ.Client.Impl
{
    internal readonly struct IncomingCommand : IDisposable
    {
        public static readonly IncomingCommand Empty = default;

        public readonly MethodBase Method;
        public readonly ContentHeaderBase Header;
        public readonly ReadOnlyMemory<byte> Body;
        private readonly byte[] _rentedArray;

        public bool IsEmpty => Method is null;

        public IncomingCommand(MethodBase method, ContentHeaderBase header, ReadOnlyMemory<byte> body, byte[] rentedArray)
        {
            Method = method;
            Header = header;
            Body = body;
            _rentedArray = rentedArray;
        }

        public void Dispose()
        {
            if (_rentedArray != null)
            {
                ArrayPool<byte>.Shared.Return(_rentedArray);
            }
        }
    }
}
