using System.Collections.Generic;

namespace RabbitMQ.Client
{
#nullable enable
    public interface IRecordedQueue
    {
        string Name { get; }

        bool Durable { get; }

        bool Exclusive { get; }

        bool AutoDelete { get; }

        IDictionary<string, object>? Arguments { get; }

        bool IsServerNamed { get; }
    }
}
