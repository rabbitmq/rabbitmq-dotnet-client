using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.client.impl
{
    internal sealed class InlineConsumerDispatcher : IConsumerDispatcher
    {
        private readonly ModelBase _model;

        public InlineConsumerDispatcher(ModelBase model)
        {
            _model = model;
        }

        public bool IsShutdown { get; private set; }

        public void HandleBasicCancel(IBasicConsumer consumer, string consumerTag)
        {
            try
            {
                consumer.HandleBasicCancel(consumerTag);
            }
            catch (Exception e)
            {
                _model.OnCallbackException(CallbackExceptionEventArgs.Build(e, new Dictionary<string, object>
                {
                    { CallbackExceptionEventArgs.Consumer, consumer },
                    { CallbackExceptionEventArgs.Context, nameof(HandleBasicDeliver) }
                }));
            }
        }

        public void HandleBasicCancelOk(IBasicConsumer consumer, string consumerTag)
        {
            try
            {
                consumer.HandleBasicCancelOk(consumerTag);
            }
            catch (Exception e)
            {
                _model.OnCallbackException(CallbackExceptionEventArgs.Build(e, new Dictionary<string, object>
                {
                    { CallbackExceptionEventArgs.Consumer, consumer },
                    { CallbackExceptionEventArgs.Context, nameof(HandleBasicCancelOk) }
                }));
            }
        }

        public void HandleBasicConsumeOk(IBasicConsumer consumer, string consumerTag)
        {
            try
            {
                consumer.HandleBasicConsumeOk(consumerTag);
            }
            catch (Exception e)
            {
                _model.OnCallbackException(CallbackExceptionEventArgs.Build(e, new Dictionary<string, object>
                {
                    { CallbackExceptionEventArgs.Consumer, consumer },
                    { CallbackExceptionEventArgs.Context, nameof(HandleBasicConsumeOk) }
                }));
            }
        }

        public void HandleBasicDeliver(IBasicConsumer consumer, string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties basicProperties, ReadOnlyMemory<byte> body, byte[] rentedArray)
        {
            try
            {
                consumer.HandleBasicDeliver(consumerTag, deliveryTag, redelivered, exchange, routingKey, basicProperties, body);
            }
            catch (Exception e)
            {
                _model.OnCallbackException(CallbackExceptionEventArgs.Build(e, new Dictionary<string, object>
                {
                    { CallbackExceptionEventArgs.Consumer, consumer },
                    { CallbackExceptionEventArgs.Context, nameof(HandleBasicDeliver) }
                }));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }

        public void HandleModelShutdown(IBasicConsumer consumer, ShutdownEventArgs reason)
        {
            try
            {
                consumer.HandleModelShutdown(_model, reason);
            }
            catch (Exception e)
            {
                _model.OnCallbackException(CallbackExceptionEventArgs.Build(e, new Dictionary<string, object>
                {
                    { CallbackExceptionEventArgs.Consumer, consumer },
                    { CallbackExceptionEventArgs.Context, nameof(HandleModelShutdown) }
                }));
            }
        }

        public void Quiesce()
        {
            IsShutdown = true;
        }

        public Task Shutdown(IModel model)
        {
            return Task.CompletedTask;
        }
    }
}
