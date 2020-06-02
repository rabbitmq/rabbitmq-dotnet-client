using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
        readonly ReadOnlyMemory<byte> _body;

        public BasicDeliver(IBasicConsumer consumer,
            string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body) : base(consumer)
        {
            _consumerTag = consumerTag;
            _deliveryTag = deliveryTag;
            _redelivered = redelivered;
            _exchange = exchange;
            _routingKey = routingKey;
            _basicProperties = basicProperties;
            _body = body;
        }

        protected override async Task Execute(IModel model, IAsyncBasicConsumer consumer)
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
                if (!(model is ModelBase modelBase))
                {
                    return;
                }

                var details = new Dictionary<string, object>()
                {
                    {"consumer", consumer},
                    {"context",  "HandleBasicDeliver"}
                };
                modelBase.OnCallbackException(CallbackExceptionEventArgs.Build(e, details));
            }
            finally
            {
                if (MemoryMarshal.TryGetArray(_body, out ArraySegment<byte> segment))
                {
                    ArrayPool<byte>.Shared.Return(segment.Array);
                }
            }
        }
    }
}
