using System;
using System.Buffers;
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
        readonly IMemoryOwner<byte> _body;
        readonly int _bodyLength;

        public BasicDeliver(IBasicConsumer consumer, 
            string consumerTag, 
            ulong deliveryTag, 
            bool redelivered, 
            string exchange, 
            string routingKey, 
            IBasicProperties basicProperties,
            IMemoryOwner<byte> body,
            int bodyLength) : base(consumer)
        {
            _consumerTag = consumerTag;
            _deliveryTag = deliveryTag;
            _redelivered = redelivered;
            _exchange = exchange;
            _routingKey = routingKey;
            _basicProperties = basicProperties;
            _body = body;
            _bodyLength = bodyLength;
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
                    _body.Memory.Slice(0, _bodyLength)).ConfigureAwait(false);
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
            finally
            {
                _body.Dispose();
            }
        }
    }
}
