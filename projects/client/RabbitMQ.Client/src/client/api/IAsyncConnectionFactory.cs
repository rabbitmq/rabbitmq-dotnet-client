namespace RabbitMQ.Client
{
    internal interface IAsyncConnectionFactory : IConnectionFactory
    {
        bool DispatchConsumersAsync { get; set; }
    }
}