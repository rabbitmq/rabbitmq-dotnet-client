using System;
using System.Diagnostics.Contracts;
using RabbitMQ.Client.client.framing;

namespace RabbitMQ.Client.Framing.Impl
{
    internal interface IAmqpMethod
    {
        ProtocolCommandId ProtocolCommandId { get; }
    }

    internal interface IOutgoingAmqpMethod : IAmqpMethod
    {
        [Pure]
        int WriteArgumentsTo(Span<byte> span);
        [Pure]
        int GetRequiredBufferSize();
    }
}
