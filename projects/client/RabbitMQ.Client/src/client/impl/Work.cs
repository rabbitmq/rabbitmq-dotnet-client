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

        public Task Execute(ModelBase model)
        {
            try
            {
                return Execute(model, asyncConsumer);
            }
            catch (Exception)
            {
                return TaskExtensions.CompletedTask;
            }
        }

        protected abstract Task Execute(ModelBase model, IAsyncBasicConsumer consumer);
    }
}