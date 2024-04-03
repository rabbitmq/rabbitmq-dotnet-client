namespace RabbitMQ.Client.OpenTelemetry
{
    public class RabbitMQOpenTelemetryConfiguration
    {
        public RabbitMQOpenTelemetryConfiguration(bool useRoutingKeyAsOperationName = true,
            bool includePublishers = true,
            bool includeSubscribers = true)
        {
            UseRoutingKeyAsOperationName = useRoutingKeyAsOperationName;
            IncludePublishers = includePublishers;
            IncludeSubscribers = includeSubscribers;
        }

        public bool UseRoutingKeyAsOperationName { get; }
        public bool IncludePublishers { get; }
        public bool IncludeSubscribers { get; }
        public static RabbitMQOpenTelemetryConfiguration Default { get; } = new RabbitMQOpenTelemetryConfiguration();
    }
}
