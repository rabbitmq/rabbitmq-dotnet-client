namespace RabbitMQ.Client.Impl
{
    internal class AsyncConsumerDispatcher : IConsumerDispatcher
    {
        private readonly ModelBase model;
        private readonly AsyncConsumerWorkService workService;

        public AsyncConsumerDispatcher(ModelBase model, AsyncConsumerWorkService ws)
        {
            this.model = model;
            this.workService = ws;
            this.IsShutdown = false;
        }

        public void Quiesce()
        {
            IsShutdown = true;
        }

        public void Shutdown()
        {
            // necessary evil
            this.workService.Stop().GetAwaiter().GetResult();
        }

        public void Shutdown(IModel model)
        {
            // necessary evil
            this.workService.Stop(model).GetAwaiter().GetResult();
        }

        public bool IsShutdown
        {
            get;
            private set;
        }

        public void HandleBasicConsumeOk(IBasicConsumer consumer,
            string consumerTag)
        {
            ScheduleUnlessShuttingDown(new BasicConsumeOk(consumer, consumerTag));
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
            ScheduleUnlessShuttingDown(new BasicDeliver(consumer, consumerTag, deliveryTag, redelivered, exchange, routingKey, basicProperties, body));
        }

        public void HandleBasicCancelOk(IBasicConsumer consumer, string consumerTag)
        {
            ScheduleUnlessShuttingDown(new BasicCancelOk(consumer, consumerTag));
        }

        public void HandleBasicCancel(IBasicConsumer consumer, string consumerTag)
        {
            ScheduleUnlessShuttingDown(new BasicCancel(consumer, consumerTag));
        }

        public void HandleModelShutdown(IBasicConsumer consumer, ShutdownEventArgs reason)
        {
            // the only case where we ignore the shutdown flag.
            new ModelShutdown(consumer,reason).Execute(model).GetAwaiter().GetResult();
        }

        private void ScheduleUnlessShuttingDown<TWork>(TWork work) 
            where TWork : Work
        {
            if (!this.IsShutdown)
            {
                Schedule(work);
            }
        }

        private void Schedule<TWork>(TWork work)
            where TWork : Work
        {
            this.workService.Schedule(this.model, work);
        }
    }
}