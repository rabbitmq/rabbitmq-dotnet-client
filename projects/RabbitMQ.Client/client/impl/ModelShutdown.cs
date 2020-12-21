using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    internal sealed class ModelShutdown : Work
    {
        private readonly ShutdownEventArgs _reason;
        private readonly object _channel;

        public override string Context => "HandleModelShutdown";

        public ModelShutdown(IBasicConsumer consumer, ShutdownEventArgs reason, object channel) : base(consumer)
        {
            _reason = reason;
            _channel = channel;
        }

        protected override Task Execute(IAsyncBasicConsumer consumer)
        {
            return consumer.HandleModelShutdown(_channel, _reason);
        }
    }
}
