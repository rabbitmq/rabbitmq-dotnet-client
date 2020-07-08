using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Impl
{
    sealed class BasicCancelOk : Work
    {
        readonly string _consumerTag;

        public BasicCancelOk(IBasicConsumer consumer, string consumerTag) : base(consumer)
        {
            _consumerTag = consumerTag;
        }

        protected override async Task Execute(IModel model, IAsyncBasicConsumer consumer)
        {
            try
            {
                await consumer.HandleBasicCancelOk(_consumerTag).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if (!(model is ModelBase modelBase))
                {
                    return;
                }

                var details = new Dictionary<string, object>()
                {
                    {"consumer", consumer},
                    {"context",  "HandleBasicCancelOk"}
                };
                modelBase.OnCallbackException(CallbackExceptionEventArgs.Build(e, details));
            }
        }
    }
}
