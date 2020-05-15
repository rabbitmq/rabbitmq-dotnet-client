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

        public async ValueTask Execute(ModelBase model)
        {
            try
            {
                await Execute(model, _asyncConsumer).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // intentionally caught
            }
        }

        protected abstract ValueTask Execute(ModelBase model, IAsyncBasicConsumer consumer);
    }
}
