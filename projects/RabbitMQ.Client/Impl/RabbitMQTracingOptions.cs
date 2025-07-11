namespace RabbitMQ.Client
{
    public class RabbitMQTracingOptions
    {
        public bool UseRoutingKeyAsOperationName { get; set; } = true;
        public bool UsePublisherAsParent { get; set; } = true;
    }
}
