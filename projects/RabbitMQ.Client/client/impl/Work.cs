using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    internal abstract class Work
    {
        public IAsyncBasicConsumer Consumer { get; }

        public abstract string Context { get; }

        protected Work(IBasicConsumer consumer)
        {
            Consumer = (IAsyncBasicConsumer)consumer;
        }

        public Task Execute(IModel model)
        {
            return Execute(model, Consumer);
        }

        protected abstract Task Execute(IModel model, IAsyncBasicConsumer consumer);

        public virtual void PostExecute()
        {
        }
    }
}
