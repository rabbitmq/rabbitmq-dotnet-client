using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    internal sealed class ModelShutdown : Work
    {
        private readonly ShutdownEventArgs _reason;
        private readonly IModel _model;

        public override string Context => "HandleModelShutdown";

        public ModelShutdown(IBasicConsumer consumer, ShutdownEventArgs reason, IModel model) : base(consumer)
        {
            _reason = reason;
            _model = model;
        }

        protected override Task Execute(IAsyncBasicConsumer consumer)
        {
            return consumer.HandleModelShutdown(_model, _reason);
        }
    }
}
