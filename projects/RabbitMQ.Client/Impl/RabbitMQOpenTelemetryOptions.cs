namespace RabbitMQ.Client
{
    public enum OpenTelemetryLinkType
    {
        AlwaysLink,
        AlwaysParentChild
    }

    public class RabbitMQOpenTelemetryOptions
    {
        public bool UseRoutingKeyAsOperationName { get; set; }
        public OpenTelemetryLinkType LinkType { get; set; } = OpenTelemetryLinkType.AlwaysLink;
    }
}
