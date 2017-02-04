using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Impl
{
    sealed class BasicDeliver : Work
    {
        readonly string consumerTag;
        readonly ulong deliveryTag;
        readonly bool redelivered;
        readonly string exchange;
        readonly string routingKey;
        readonly IBasicProperties basicProperties;
        readonly byte[] body;

        public BasicDeliver(IBasicConsumer consumer, 
            string consumerTag, 
            ulong deliveryTag, 
            bool redelivered, 
            string exchange, 
            string routingKey, 
            IBasicProperties basicProperties, 
            byte[] body) : base(consumer)
        {
            this.consumerTag = consumerTag;
            this.deliveryTag = deliveryTag;
            this.redelivered = redelivered;
            this.exchange = exchange;
            this.routingKey = routingKey;
            this.basicProperties = basicProperties;
            this.body = body;
        }

        protected override async Task Execute(ModelBase model, IAsyncBasicConsumer consumer)
        {
            try
            {
                await consumer.HandleBasicDeliver(consumerTag,
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
        }
    }
}