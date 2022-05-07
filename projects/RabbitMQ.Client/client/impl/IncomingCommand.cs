using System;
using System.Buffers;
using RabbitMQ.Client.client.framing;

namespace RabbitMQ.Client.Impl;

internal readonly struct IncomingCommand
{
    public static readonly IncomingCommand Empty = default;

    public readonly ProtocolCommandId CommandId;

    public readonly ReadOnlyMemory<byte> MethodBytes;
    private readonly byte[] _rentedMethodBytes;

    public readonly ReadOnlyMemory<byte> HeaderBytes;
    private readonly byte[] _rentedHeaderArray;

    public readonly ReadOnlyMemory<byte> Body;
    private readonly byte[] _rentedBodyArray;

    public bool IsEmpty => CommandId is default(ProtocolCommandId);

    public IncomingCommand(ProtocolCommandId commandId, ReadOnlyMemory<byte> methodBytes, byte[] rentedMethodArray, ReadOnlyMemory<byte> headerBytes, byte[] rentedHeaderArray, ReadOnlyMemory<byte> body, byte[] rentedBodyArray)
    {
        CommandId = commandId;
        MethodBytes = methodBytes;
        _rentedMethodBytes = rentedMethodArray;
        HeaderBytes = headerBytes;
        _rentedHeaderArray = rentedHeaderArray;
        Body = body;
        _rentedBodyArray = rentedBodyArray;
    }

    public byte[] TakeoverBody()
    {
        return _rentedBodyArray;
    }

    public void ReturnHeaderBuffer()
    {
        ArrayPool<byte>.Shared.Return(_rentedHeaderArray);
    }

    public void ReturnMethodBuffer()
    {
        ArrayPool<byte>.Shared.Return(_rentedMethodBytes);
    }
}
