using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Impl
{
    sealed class ModelShutdown : Work
    {
        readonly ShutdownEventArgs _reason;

        public ModelShutdown(IBasicConsumer consumer, ShutdownEventArgs reason) : base(consumer)
        {
            _reason = reason;
        }

        protected override async Task Execute(IModel model, IAsyncBasicConsumer consumer)
        {
            try
            {
                await consumer.HandleModelShutdown(model, _reason).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if (!(model is ModelBase modelBase))
                {
                    return;
                }

                var details = new Dictionary<string, object>()
                {
                    { "consumer", consumer },
                    { "context", "HandleModelShutdown" }
                };
                modelBase.OnCallbackException(CallbackExceptionEventArgs.Build(e, details));
            }
        }
    }
}
