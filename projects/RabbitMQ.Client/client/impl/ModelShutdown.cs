using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    internal sealed class ModelShutdown : Work
    {
        private readonly ShutdownEventArgs _reason;

        public override string Context => "HandleModelShutdown";

        public ModelShutdown(IBasicConsumer consumer, ShutdownEventArgs reason) : base(consumer)
        {
            _reason = reason;
        }

        protected override Task Execute(IModel model, IAsyncBasicConsumer consumer)
        {
            return consumer.HandleModelShutdown(model, _reason);
        }
    }
}
