namespace RabbitMQ.Client
{
    interface IAsyncConnection : IConnection
    {
        AsyncConsumerWorkService AsyncConsumerWorkService { get; }
    }
}