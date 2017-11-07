namespace RabbitMQ.Client
{
    public interface IMessageBatch
    {
        void Add(string exchange, string routingKey, bool mandatory, IBasicProperties properties, byte[] body);
        void Publish();
    }
}
