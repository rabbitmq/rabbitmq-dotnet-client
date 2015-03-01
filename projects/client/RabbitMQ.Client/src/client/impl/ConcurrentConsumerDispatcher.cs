using System;

namespace RabbitMQ.Client.Impl
{
    class ConcurrentConsumerDispatcher : IConsumerDispatcher
    {
        private IModel model;
        private ConsumerWorkService workService;
        private bool isShuttingDown = false;

        public ConcurrentConsumerDispatcher(IModel model, ConsumerWorkService ws)
        {
            this.model = model;
            this.workService = ws;
        }

        public void HandleBasicConsumeOk(IBasicConsumer consumer,
                                         string consumerTag)
        {
            UnlessShuttingDown(() =>
            {
                try
                {
                    consumer.HandleBasicConsumeOk(consumerTag);
                }
                catch (Exception e)
                {
                    // TODO
                }
            });
        }

        public void HandleBasicDeliver(IBasicConsumer consumer,
                                       string consumerTag,
                                       ulong deliveryTag,
                                       bool redelivered,
                                       string exchange,
                                       string routingKey,
                                       IBasicProperties basicProperties,
                                       byte[] body)
        {
            throw new NotImplementedException();
        }

        public void HandleBasicCancelOk(IBasicConsumer consumer, string consumerTag)
        {
            throw new NotImplementedException();
        }

        public void HandleBasicCancel(IBasicConsumer consumer, string consumerTag)
        {
            throw new NotImplementedException();
        }

        public void Shutdown()
        {
            this.isShuttingDown = true;
        }

        private void UnlessShuttingDown(Action fn)
        {
            if(!this.isShuttingDown)
            {
                Execute(fn);
            }
        }

        private void Execute(Action fn)
        {
            this.workService.AddWork(this.model, fn);
        }
    }
}
