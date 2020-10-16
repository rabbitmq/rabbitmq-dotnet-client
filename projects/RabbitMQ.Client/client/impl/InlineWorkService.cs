using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.client.impl
{
    internal sealed class InlineWorkService : ConsumerWorkService
    {
        public InlineWorkService(int concurrency) : base(concurrency)
        {
        }
    }
}
