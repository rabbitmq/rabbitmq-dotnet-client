using RabbitMQ.Client.client.framing;

namespace RabbitMQ.Client.Framing.Impl;

internal interface IAmqpMethod
{
    ProtocolCommandId ProtocolCommandId { get; }
}

internal interface IOutgoingAmqpMethod : IAmqpMethod, IAmqpWriteable
{
}
