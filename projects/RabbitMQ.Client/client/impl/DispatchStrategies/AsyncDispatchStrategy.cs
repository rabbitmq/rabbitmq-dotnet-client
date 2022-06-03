using System;
using System.Threading.Tasks;

namespace RabbitMQ.Client.DispatchStrategies;

internal class AsyncDispatchStrategy : IDispatchStrategy
{
    private readonly IAsyncBasicConsumer _asyncBasicConsumer;

    public IBasicConsumer Consumer { get; }

    public AsyncDispatchStrategy(IAsyncBasicConsumer asyncBasicConsumer)
    {
        Consumer = (IBasicConsumer)asyncBasicConsumer;
        _asyncBasicConsumer = asyncBasicConsumer;
    }

    public Task DispatchBasicCancel(string consumerTag) => _asyncBasicConsumer.HandleBasicCancel(consumerTag);

    public Task DispatchBasicCancelOk(string consumerTag) => _asyncBasicConsumer.HandleBasicCancelOk(consumerTag);

    public Task DispatchBasicConsumeOk(string consumerTag) => _asyncBasicConsumer.HandleBasicConsumeOk(consumerTag);

    public Task DispatchBasicDeliver(string consumerTag,
        ulong deliveryTag,
        bool redelivered,
        string exchange,
        string routingKey,
        in ReadOnlyBasicProperties properties,
        ReadOnlyMemory<byte> body) 
        => _asyncBasicConsumer.HandleBasicDeliver(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body);

    public Task DispatchModelShutdown(object model, ShutdownEventArgs reason) => _asyncBasicConsumer.HandleModelShutdown(model, reason);
}
