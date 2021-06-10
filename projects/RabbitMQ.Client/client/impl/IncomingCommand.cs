using System;
using System.Buffers;
using RabbitMQ.Client.client.framing;

namespace RabbitMQ.Client.Impl
{
    internal readonly struct IncomingCommand
    {
        public static readonly IncomingCommand Empty = default;

        public readonly ProtocolCommandId CommandId;

        public readonly ReadOnlyMemory<byte> MethodBytes;
        private readonly byte[] _rentedMethodBytes;

        public readonly ContentHeaderBase Header;

        public readonly ReadOnlyMemory<byte> Body;
        private readonly byte[] _rentedBodyArray;

        public bool IsEmpty => CommandId is default(ProtocolCommandId);

        public IncomingCommand(ProtocolCommandId commandId, ReadOnlyMemory<byte> methodBytes, byte[] rentedMethodBytes, ContentHeaderBase header, ReadOnlyMemory<byte> body, byte[] rentedBodyArray)
        {
            CommandId = commandId;
            MethodBytes = methodBytes;
            _rentedMethodBytes = rentedMethodBytes;
            Header = header;
            Body = body;
            _rentedBodyArray = rentedBodyArray;
        }

        public byte[] TakeoverPayload()
        {
            return _rentedBodyArray;
        }

        public void ReturnMethodBuffer()
        {
            ArrayPool<byte>.Shared.Return(_rentedMethodBytes);
        }
    }
}
