using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    internal sealed class AsyncConsumerDispatcher : IConsumerDispatcher
    {
        private readonly ModelBase _model;
        private readonly AsyncConsumerWorkService _workService;

        public AsyncConsumerDispatcher(ModelBase model, AsyncConsumerWorkService ws)
        {
            _model = model;
            _workService = ws;
            IsShutdown = false;
        }

        public void Quiesce()
        {
            IsShutdown = true;
        }

        public Task Shutdown(IModel model)
        {
            return _workService.Stop(model);
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
            ReadOnlyMemory<byte> body,
            byte[] rentedArray)
        {
            ScheduleUnlessShuttingDown(new BasicDeliver(consumer, consumerTag, deliveryTag, redelivered, exchange, routingKey, basicProperties, body, rentedArray));
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
            Schedule(new ModelShutdown(consumer, reason, _model));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ScheduleUnlessShuttingDown(Work work)
        {
            if (!IsShutdown)
            {
                Schedule(work);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Schedule(Work work)
        {
            _workService.Schedule(_model, work);
        }
    }
}
