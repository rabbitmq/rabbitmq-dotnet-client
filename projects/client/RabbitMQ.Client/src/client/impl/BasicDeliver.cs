using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Impl
{
    sealed class BasicDeliver : Work
    {
        readonly string _consumerTag;
        readonly ulong _deliveryTag;
        readonly bool _redelivered;
        readonly string _exchange;
        readonly string _routingKey;
        readonly IBasicProperties _basicProperties;
        readonly byte[] _body;

        public BasicDeliver(IBasicConsumer consumer, 
            string consumerTag, 
            ulong deliveryTag, 
            bool redelivered, 
            string exchange, 
            string routingKey, 
            IBasicProperties basicProperties, 
            byte[] body) : base(consumer)
        {
            _consumerTag = consumerTag;
            _deliveryTag = deliveryTag;
            _redelivered = redelivered;
            _exchange = exchange;
            _routingKey = routingKey;
            _basicProperties = basicProperties;
            _body = body;
        }

        protected override async Task Execute(ModelBase model, IAsyncBasicConsumer consumer)
        {
            try
            {
                await consumer.HandleBasicDeliver(_consumerTag,
                    _deliveryTag,
                    _redelivered,
                    _exchange,
                    _routingKey,
                    _basicProperties,
                    _body).ConfigureAwait(false);
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
