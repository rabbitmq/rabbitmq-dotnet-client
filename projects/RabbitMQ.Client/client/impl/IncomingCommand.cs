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
        private readonly ArrayPool<byte> _rentedArrayOwner;

        public bool IsEmpty => Method is null;

        public IncomingCommand(MethodBase method, ContentHeaderBase header, ReadOnlyMemory<byte> body, byte[] rentedArray, ArrayPool<byte> rentedArrayOwner)
        {
            Method = method;
            Header = header;
            Body = body;
            _rentedArray = rentedArray;
            _rentedArrayOwner = rentedArrayOwner;
        }

        public void Dispose()
        {
            if (_rentedArray != null)
            {
                _rentedArrayOwner.Return(_rentedArray);
            }
        }

        public override string ToString()
        {
            return $"IncomingCommand Method={Method.ProtocolMethodName}, Body.Length={Body.Length}";
        }
    }
}
