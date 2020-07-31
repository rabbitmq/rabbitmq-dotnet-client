using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    internal sealed class BasicConsumeOk : Work
    {
        private readonly string _consumerTag;

        public override string Context => "HandleBasicConsumeOk";

        public BasicConsumeOk(IBasicConsumer consumer, string consumerTag) : base(consumer)
        {
            _consumerTag = consumerTag;
        }

        protected override Task Execute(IAsyncBasicConsumer consumer)
        {
            return consumer.HandleBasicConsumeOk(_consumerTag);
        }
    }
}
