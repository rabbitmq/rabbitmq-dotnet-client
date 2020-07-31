using System;

namespace RabbitMQ.Client.Impl
{
    internal readonly struct IncomingCommand
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

        public byte[] TakeoverPayload()
        {
            return _rentedArray;
        }
    }
}
