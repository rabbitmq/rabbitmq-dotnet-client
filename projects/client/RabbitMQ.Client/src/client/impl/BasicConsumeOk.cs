using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Impl
{
    sealed class BasicConsumeOk : Work
    {
        readonly string _consumerTag;

        public BasicConsumeOk(IBasicConsumer consumer, string consumerTag) : base(consumer)
        {
            _consumerTag = consumerTag;
        }

        protected override async Task Execute(ModelBase model, IAsyncBasicConsumer consumer)
        {
            try
            {
                await consumer.HandleBasicConsumeOk(_consumerTag).ConfigureAwait(false);
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
        }
    }
}
