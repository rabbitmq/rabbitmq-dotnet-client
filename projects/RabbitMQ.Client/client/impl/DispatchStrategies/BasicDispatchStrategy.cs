using System;
using System.Threading.Tasks;

namespace RabbitMQ.Client.DispatchStrategies;

internal class BasicDispatchStrategy : IDispatchStrategy
{
    public IBasicConsumer Consumer { get; }

    public BasicDispatchStrategy(IBasicConsumer consumer)
    {
        Consumer = consumer;
    }

    public Task DispatchBasicCancel(string consumerTag)
    {
        Consumer.HandleBasicCancel(consumerTag);
        return Task.CompletedTask;
    }

    public Task DispatchBasicCancelOk(string consumerTag)
    {
        Consumer.HandleBasicCancelOk(consumerTag);
        return Task.CompletedTask;
    }

    public Task DispatchBasicConsumeOk(string consumerTag)
    {
        Consumer.HandleBasicConsumeOk(consumerTag);
        return Task.CompletedTask;
    }

    public Task DispatchBasicDeliver(string consumerTag,
        ulong deliveryTag,
        bool redelivered,
        string exchange,
        string routingKey,
        in ReadOnlyBasicProperties properties,
        ReadOnlyMemory<byte> body)
    { 
        Consumer.HandleBasicDeliver(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body);
        return Task.CompletedTask;
    }
    
    public Task DispatchModelShutdown(object model, ShutdownEventArgs reason)
    {
        Consumer.HandleModelShutdown(model, reason);
        return Task.CompletedTask;
    }
}
