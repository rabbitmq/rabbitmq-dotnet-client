using System;
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

        public async Task Execute(ModelBase model)
        {
            try
            {
                await Task.Yield();
                await Execute(model, _asyncConsumer).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // intentionally caught
            }
        }

        protected abstract Task Execute(ModelBase model, IAsyncBasicConsumer consumer);
    }
}
