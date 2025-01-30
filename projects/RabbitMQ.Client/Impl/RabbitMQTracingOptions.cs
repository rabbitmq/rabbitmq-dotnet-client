namespace RabbitMQ.Client
{
    public enum TracingLinkType
    {
        AlwaysLink,
        AlwaysParentChildAndLink
    }

    public class RabbitMQTracingOptions
    {
        public bool UseRoutingKeyAsOperationName { get; set; } = true;
        public TracingLinkType LinkType { get; set; } = TracingLinkType.AlwaysLink;
    }
}
