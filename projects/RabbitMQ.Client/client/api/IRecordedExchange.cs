using System.Collections.Generic;

namespace RabbitMQ.Client
{
#nullable enable
    public interface IRecordedExchange
    {
        ExchangeName Name { get; }

        ExchangeType Type { get; }

        bool Durable { get; }

        bool AutoDelete { get; }

        IDictionary<string, object>? Arguments { get; }
    }
}
