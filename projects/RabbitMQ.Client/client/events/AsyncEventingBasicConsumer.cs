using System;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Events
{
    public class AsyncEventingBasicConsumer : AsyncDefaultBasicConsumer
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
        public override async Task HandleBasicCancelOk(string consumerTag)
        {
            await base.HandleBasicCancelOk(consumerTag).ConfigureAwait(false);
            await (Unregistered?.Invoke(this, new ConsumerEventArgs(new[] { consumerTag })) ?? Task.CompletedTask).ConfigureAwait(false);
        }

        ///<summary>Fires when the server confirms successful consumer registration.</summary>
        public override async Task HandleBasicConsumeOk(string consumerTag)
        {
            await base.HandleBasicConsumeOk(consumerTag).ConfigureAwait(false);
            await (Registered?.Invoke(this, new ConsumerEventArgs(new[] { consumerTag })) ?? Task.CompletedTask).ConfigureAwait(false);
        }

        ///<summary>Fires the Received event.</summary>
        public override async Task HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            await base.HandleBasicDeliver(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body);
            await (Received?.Invoke(this, new BasicDeliverEventArgs(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body)) ?? Task.CompletedTask).ConfigureAwait(false);
        }

        ///<summary>Fires the Shutdown event.</summary>
        public override async Task HandleModelShutdown(object model, ShutdownEventArgs reason)
        {
            await base.HandleModelShutdown(model, reason).ConfigureAwait(false);
            await (Shutdown?.Invoke(this, reason) ?? Task.CompletedTask).ConfigureAwait(false);
        }
    }
}
