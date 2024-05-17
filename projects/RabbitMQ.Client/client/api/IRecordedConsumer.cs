using System.Collections.Generic;

namespace RabbitMQ.Client
{
#nullable enable
    public interface IRecordedConsumer
    {
        ConsumerTag ConsumerTag { get; }

        QueueName Queue { get; }

        bool AutoAck { get; }

        bool Exclusive { get; }

        IDictionary<string, object>? Arguments { get; }
    }
}
