using System;
using System.Threading.Tasks;

namespace RabbitMQ.Client.DispatchStrategies;

internal interface IDispatchStrategy
{
    IBasicConsumer Consumer { get; }
    
    Task DispatchBasicCancel(string consumerTag);

    Task DispatchBasicCancelOk(string consumerTag);

    Task DispatchBasicConsumeOk(string consumerTag);
    
    Task DispatchBasicDeliver(string consumerTag,
        ulong deliveryTag,
        bool redelivered,
        string exchange,
        string routingKey,
        in ReadOnlyBasicProperties properties,
        ReadOnlyMemory<byte> body);
    
    Task DispatchModelShutdown(object model, ShutdownEventArgs reason);
}
