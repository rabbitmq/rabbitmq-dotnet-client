namespace RabbitMQ.Client.OpenTelemetry
{
    public class RabbitMQOpenTelemetryConfiguration
    {
        public bool PropagateBaggage { get; set; } = true;
        public bool UseRoutingKeyAsOperationName { get; set; } = true;
        public bool IncludePublishers { get; set; } = true;
        public bool IncludeSubscribers { get; set; } = true;
    }
}
