using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Impl
{
    internal class AsyncConsumerDispatcher : IConsumerDispatcher
    {
        private ModelBase model;
        private AsyncConsumerWorkService workService;

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
            this.workService.StopWork().GetAwaiter().GetResult();
        }

        public void Shutdown(IModel model)
        {
            // necessary evil
            this.workService.StopWork(model).GetAwaiter().GetResult();
        }

        public bool IsShutdown
        {
            get;
            private set;
        }

        public void HandleBasicConsumeOk(IBasicConsumer consumer,
            string consumerTag)
        {
            UnlessShuttingDown(async () =>
            {
                try
                {
                    await ((IAsyncBasicConsumer)consumer).HandleBasicConsumeOk(consumerTag).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    var details = new Dictionary<string, object>()
                    {
                        {"consumer", consumer},
                        {"context",  "HandleBasicConsumeOk"}
                    };
                    model.OnCallbackException(CallbackExceptionEventArgs.Build(e, details));
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
            UnlessShuttingDown(async () =>
            {
                try
                {
                    await ((IAsyncBasicConsumer)consumer).HandleBasicDeliver(consumerTag,
                        deliveryTag,
                        redelivered,
                        exchange,
                        routingKey,
                        basicProperties,
                        body).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    var details = new Dictionary<string, object>()
                    {
                        {"consumer", consumer},
                        {"context",  "HandleBasicDeliver"}
                    };
                    model.OnCallbackException(CallbackExceptionEventArgs.Build(e, details));
                }
            });
        }

        public void HandleBasicCancelOk(IBasicConsumer consumer, string consumerTag)
        {
            UnlessShuttingDown(async () =>
            {
                try
                {
                    await ((IAsyncBasicConsumer)consumer).HandleBasicCancelOk(consumerTag);
                }
                catch (Exception e)
                {
                    var details = new Dictionary<string, object>()
                    {
                        {"consumer", consumer},
                        {"context",  "HandleBasicCancelOk"}
                    };
                    model.OnCallbackException(CallbackExceptionEventArgs.Build(e, details));
                }
            });
        }

        public void HandleBasicCancel(IBasicConsumer consumer, string consumerTag)
        {
            UnlessShuttingDown(async () =>
            {
                try
                {
                    await ((IAsyncBasicConsumer)consumer).HandleBasicCancel(consumerTag).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    var details = new Dictionary<string, object>()
                    {
                        {"consumer", consumer},
                        {"context",  "HandleBasicCancel"}
                    };
                    model.OnCallbackException(CallbackExceptionEventArgs.Build(e, details));
                }
            });
        }

        public void HandleModelShutdown(IBasicConsumer consumer, ShutdownEventArgs reason)
        {
            // the only case where we ignore the shutdown flag.
            try
            {
                ((IAsyncBasicConsumer)consumer).HandleModelShutdown(model, reason).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                var details = new Dictionary<string, object>()
                {
                    {"consumer", consumer},
                    {"context",  "HandleModelShutdown"}
                };
                model.OnCallbackException(CallbackExceptionEventArgs.Build(e, details));
            };
        }

        private void UnlessShuttingDown(Func<Task> fn)
        {
            if (!this.IsShutdown)
            {
                Execute(fn);
            }
        }

        private void Execute(Func<Task> fn)
        {
            this.workService.AddWork(this.model, fn);
        }
    }
}