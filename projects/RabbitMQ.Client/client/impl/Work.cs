using System;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    internal abstract class Work
    {
        private readonly IAsyncBasicConsumer _asyncConsumer;

        protected Work(IBasicConsumer consumer) => _asyncConsumer = (IAsyncBasicConsumer)consumer;

        public async ValueTask Execute(Model model)
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

        protected abstract ValueTask Execute(Model model, IAsyncBasicConsumer consumer);
    }
}
