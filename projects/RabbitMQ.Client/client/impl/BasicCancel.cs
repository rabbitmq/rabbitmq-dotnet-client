using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    internal sealed class BasicCancel : Work
    {
        private readonly string _consumerTag;

        public override string Context => "HandleBasicCancel";

        public BasicCancel(IBasicConsumer consumer, string consumerTag) : base(consumer)
        {
            _consumerTag = consumerTag;
        }

        protected override Task Execute(IModel model, IAsyncBasicConsumer consumer)
        {
            return consumer.HandleBasicCancel(_consumerTag);
        }
    }
}
