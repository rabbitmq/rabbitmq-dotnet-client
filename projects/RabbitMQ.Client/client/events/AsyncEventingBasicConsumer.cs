using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Events
{
    public class AsyncEventingBasicConsumer : DefaultBasicConsumer
    {
        ///<summary>Constructor which sets the Model property to the
        ///given value.</summary>
        public AsyncEventingBasicConsumer(IModel model) : base(model)
        {
        }

        ///<summary>
        /// Event fired when a delivery arrives for the consumer.
        /// </summary>
        /// <remarks>
        /// Handlers must copy or fully use delivery body before returning.
        /// Accessing the body at a later point is unsafe as its memory can
        /// be already released.
        /// </remarks>
        public event AsyncEventHandler<BasicDeliverEventArgs> Received;

        ///<summary>Fires when the server confirms successful consumer cancelation.</summary>
        public event AsyncEventHandler<ConsumerEventArgs> Registered;

        ///<summary>Fires on model (channel) shutdown, both client and server initiated.</summary>
        public event AsyncEventHandler<ShutdownEventArgs> Shutdown;

        ///<summary>Fires when the server confirms successful consumer cancelation.</summary>
        public event AsyncEventHandler<ConsumerEventArgs> Unregistered;

        ///<summary>Fires when the server confirms successful consumer cancelation.</summary>
        public override async ValueTask HandleBasicCancelOk(string consumerTag)
        {
            ValueTask baseTask = base.HandleBasicCancelOk(consumerTag);
            if (!baseTask.IsCompletedSuccessfully)
            {
                await baseTask.ConfigureAwait(false);
            }

            if (Unregistered != null)
            {
                var args = new ConsumerEventArgs(new[] { consumerTag });
                foreach (AsyncEventHandler<ConsumerEventArgs> handlerInstance in Unregistered.GetInvocationList())
                {
                    try
                    {
                        ValueTask handlerTask = handlerInstance(this, args);
                        if (!handlerTask.IsCompletedSuccessfully)
                        {
                            await handlerTask.ConfigureAwait(false);
                        }
                    }
                    catch (Exception e)
                    {
                        if (Model is Model modelBase)
                        {
                            modelBase.OnCallbackException(CallbackExceptionEventArgs.Build(e, new Dictionary<string, object> { { "consumer", this } }));
                        }
                    }
                }
            }
        }

        ///<summary>Fires when the server confirms successful consumer registration.</summary>
        public override async ValueTask HandleBasicConsumeOk(string consumerTag)
        {
            ValueTask baseTask = base.HandleBasicConsumeOk(consumerTag);
            if (!baseTask.IsCompletedSuccessfully)
            {
                await baseTask.ConfigureAwait(false);
            }

            if (Registered != null)
            {
                var args = new ConsumerEventArgs(new[] { consumerTag });
                foreach (AsyncEventHandler<ConsumerEventArgs> handlerInstance in Registered.GetInvocationList())
                {
                    try
                    {
                        ValueTask handlerTask = handlerInstance(this, args);
                        if (!handlerTask.IsCompletedSuccessfully)
                        {
                            await handlerTask.ConfigureAwait(false);
                        }
                    }
                    catch (Exception e)
                    {
                        if (Model is Model modelBase)
                        {
                            modelBase.OnCallbackException(CallbackExceptionEventArgs.Build(e, new Dictionary<string, object> { { "consumer", this } }));
                        }
                    }
                }
            }
        }

        ///<summary>Fires the Received event.</summary>
        public override async ValueTask HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            ValueTask baseTask = base.HandleBasicDeliver(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body);
            if (!baseTask.IsCompletedSuccessfully)
            {
                await baseTask.ConfigureAwait(false);
            }

            if (Received != null)
            {
                var args = new BasicDeliverEventArgs(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body);
                foreach (AsyncEventHandler<BasicDeliverEventArgs> handlerInstance in Received.GetInvocationList())
                {
                    try
                    {
                        ValueTask handlerTask = handlerInstance(this, args);
                        if (!handlerTask.IsCompletedSuccessfully)
                        {
                            await handlerTask.ConfigureAwait(false);
                        }
                    }
                    catch (Exception e)
                    {
                        if (Model is Model modelBase)
                        {
                            modelBase.OnCallbackException(CallbackExceptionEventArgs.Build(e, new Dictionary<string, object> { { "consumer", this } }));
                        }
                    }
                }
            }
        }

        ///<summary>Fires the Shutdown event.</summary>
        public override async ValueTask HandleModelShutdown(object model, ShutdownEventArgs reason)
        {
            ValueTask baseTask = base.HandleModelShutdown(model, reason);
            if (!baseTask.IsCompletedSuccessfully)
            {
                await baseTask.ConfigureAwait(false);
            }

            if (Shutdown != null)
            {
                foreach (AsyncEventHandler<ShutdownEventArgs> handlerInstance in Shutdown.GetInvocationList())
                {
                    try
                    {
                        ValueTask handlerTask = handlerInstance(this, reason);
                        if (!handlerTask.IsCompletedSuccessfully)
                        {
                            await handlerTask.ConfigureAwait(false);
                        }
                    }
                    catch (Exception e)
                    {
                        if (Model is Model modelBase)
                        {
                            modelBase.OnCallbackException(CallbackExceptionEventArgs.Build(e, new Dictionary<string, object> { { "consumer", this } }));
                        }
                    }
                }
            }
        }
    }
}
