using System;
using System.Collections.Generic;
using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Impl
{
    class ConcurrentConsumerDispatcher : IConsumerDispatcher
    {
        private ModelBase model;
        private ConsumerWorkService workService;

        public ConcurrentConsumerDispatcher(ModelBase model, ConsumerWorkService ws)
        {
            this.model = model;
            this.workService = ws;
            this.workService.RegisterKey(model);
            this.IsShutdown = false;
        }

        public void Quiesce()
        {
            IsShutdown = true;
        }

        public bool IsShutdown
        {
            get;
            private set;
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
                    var details = new Dictionary<string, object>()
                    {
                        {"consumer", consumer},
                        {"context",  "OnBasicConsumeOk"}
                    };
                    var args = CallbackExceptionEventArgs.Build(e, details);
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
                    var details = new Dictionary<string, object>()
                    {
                        {"consumer", consumer},
                        {"context",  "OnBasicDeliver"}
                    };
                    var args = CallbackExceptionEventArgs.Build(e, details);
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
                    var details = new Dictionary<string, object>()
                    {
                        {"consumer", consumer},
                        {"context",  "OnBasicCancelOk"}
                    };
                    var args = CallbackExceptionEventArgs.Build(e, details);
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
                    var details = new Dictionary<string, object>()
                    {
                        {"consumer", consumer},
                        {"context",  "OnBasicCancel"}
                    };
                    var args = CallbackExceptionEventArgs.Build(e, details);
                    model.OnCallbackException(args);
                }
            });
        }

        private void UnlessShuttingDown(Action fn)
        {
            if(!this.IsShutdown)
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
