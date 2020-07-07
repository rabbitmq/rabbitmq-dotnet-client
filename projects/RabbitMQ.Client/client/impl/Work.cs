using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    internal abstract class Work
    {
        readonly IAsyncBasicConsumer _asyncConsumer;

        protected Work(IBasicConsumer consumer)
        {
            _asyncConsumer = (IAsyncBasicConsumer)consumer;
        }

        public Task Execute(IModel model)
        {
            return Execute(model, _asyncConsumer);
        }

        protected abstract Task Execute(IModel model, IAsyncBasicConsumer consumer);
    }
}
