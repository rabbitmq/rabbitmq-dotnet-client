using System.Collections.Generic;

namespace RabbitMQ.Client
{
    public interface IRecordedBinding
    {
        string Source { get; }

        string Destination { get; }

        string RoutingKey { get; }

        IDictionary<string, object> Arguments { get; }
    }
}
