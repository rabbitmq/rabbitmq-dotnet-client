using System.Collections.Generic;

namespace RabbitMQ.Client
{
#nullable enable
    public interface IRecordedBinding
    {
        AmqpString Source { get; }

        AmqpString Destination { get; }

        RoutingKey RoutingKey { get; }

        IDictionary<string, object>? Arguments { get; }
    }
}
