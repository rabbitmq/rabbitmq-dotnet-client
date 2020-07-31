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

        public Task Execute()
        {
            return Execute(Consumer);
        }

        protected abstract Task Execute(IAsyncBasicConsumer consumer);

        public virtual void PostExecute()
        {
        }
    }
}
