namespace RabbitMQ.Client
{
    public enum OpenTelemetryLinkType
    {
        AlwaysLink,
        AlwaysParentChildAndLink
    }

    public class RabbitMQOpenTelemetryOptions
    {
        public bool UseRoutingKeyAsOperationName { get; set; }
        public OpenTelemetryLinkType LinkType { get; set; } = OpenTelemetryLinkType.AlwaysLink;
    }
}
