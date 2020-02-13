using System;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    internal abstract class Work
    {
        readonly IAsyncBasicConsumer asyncConsumer;

        protected Work(IBasicConsumer consumer)
        {
            asyncConsumer = (IAsyncBasicConsumer)consumer;
        }

        public async Task Execute(ModelBase model)
        {
            try
            {
                await Execute(model, asyncConsumer).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // intentionally caught
            }
        }

        protected abstract Task Execute(ModelBase model, IAsyncBasicConsumer consumer);
    }
}
