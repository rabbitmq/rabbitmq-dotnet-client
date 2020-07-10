using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    internal sealed class BasicCancelOk : Work
    {
        private readonly string _consumerTag;

        public override string Context => "HandleBasicCancelOk";

        public BasicCancelOk(IBasicConsumer consumer, string consumerTag) : base(consumer)
        {
            _consumerTag = consumerTag;
        }

        protected override Task Execute(IModel model, IAsyncBasicConsumer consumer)
        {
            return consumer.HandleBasicCancelOk(_consumerTag);
        }
    }
}
