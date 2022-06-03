using System;

namespace RabbitMQ.Client.DispatchStrategies;

internal static class DispatchStrategyFactory
{
    public static IDispatchStrategy Create(IBasicConsumer consumer)
    {
        return consumer switch
        {
            IAsyncBasicConsumer asyncBasicConsumer => new AsyncDispatchStrategy(asyncBasicConsumer),
            { } => new BasicDispatchStrategy(consumer),
            _ => throw new ArgumentNullException(nameof(consumer))
        };
    }
}
