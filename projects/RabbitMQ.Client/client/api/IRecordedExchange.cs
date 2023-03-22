using System.Collections.Generic;

namespace RabbitMQ.Client
{
    public interface IRecordedExchange
    {
        string Name { get; }

        string Type { get; }

        bool Durable { get; }

        bool AutoDelete { get; }

        IDictionary<string, object> Arguments { get; }
    }
}
