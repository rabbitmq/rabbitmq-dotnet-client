using System;
using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Impl
{
    class ConcurrentConsumerDispatcher : IConsumerDispatcher
    {
        private ModelBase model;
        private ConsumerWorkService workService;
        private bool isShuttingDown = false;

        public ConcurrentConsumerDispatcher(ModelBase model, ConsumerWorkService ws)
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
                    var args = new CallbackExceptionEventArgs(e);
                    args.Detail["context"] = "OnBasicConsumeOk";
                    model.OnCallbackException(args);
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
            UnlessShuttingDown(() =>
            {
                try
                {
                    consumer.HandleBasicDeliver(consumerTag,
                                                deliveryTag,
                                                redelivered,
                                                exchange,
                                                routingKey,
                                                basicProperties,
                                                body);
                }
                catch (Exception e)
                {
                    var args = new CallbackExceptionEventArgs(e);
                    args.Detail["context"] = "OnBasicDeliver";
                    model.OnCallbackException(args);
                }
            });
        }

        public void HandleBasicCancelOk(IBasicConsumer consumer, string consumerTag)
        {
            UnlessShuttingDown(() => {
                try
                {
                    consumer.HandleBasicCancelOk(consumerTag);
                }
                catch (Exception e)
                {
                    var args = new CallbackExceptionEventArgs(e);
                    args.Detail["context"] = "OnBasicCancelOk";
                    model.OnCallbackException(args);
                }
            });
        }

        public void HandleBasicCancel(IBasicConsumer consumer, string consumerTag)
        {
            UnlessShuttingDown(() =>
            {
                try
                {
                    consumer.HandleBasicCancel(consumerTag);
                }
                catch (Exception e)
                {
                    var args = new CallbackExceptionEventArgs(e);
                    args.Detail["context"] = "OnBasicCancel";
                    model.OnCallbackException(args);
                }
            });
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
