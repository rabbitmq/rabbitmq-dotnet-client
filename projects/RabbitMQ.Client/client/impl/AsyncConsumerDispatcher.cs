﻿using System;
using System.Buffers;
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
            ReadOnlyMemory<byte> body)
        {
            IMemoryOwner<byte> bodyCopy = MemoryPool<byte>.Shared.Rent(body.Length);
            body.CopyTo(bodyCopy.Memory);
            ScheduleUnlessShuttingDown(new BasicDeliver(consumer, consumerTag, deliveryTag, redelivered, exchange, routingKey, basicProperties, bodyCopy, body.Length));
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
            Schedule(new ModelShutdown(consumer, reason));
        }

        private void ScheduleUnlessShuttingDown<TWork>(TWork work)
            where TWork : Work
        {
            if (!IsShutdown)
            {
                Schedule(work);
            }
        }

        private void Schedule<TWork>(TWork work)
            where TWork : Work
        {
            _workService.Schedule(_model, work);
        }
    }
}
