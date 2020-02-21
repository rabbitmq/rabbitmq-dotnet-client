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

        ///<summary>Event fired on HandleBasicDeliver.</summary>
        public event AsyncEventHandler<BasicDeliverEventArgs> Received;

        ///<summary>Event fired on HandleBasicConsumeOk.</summary>
        public event AsyncEventHandler<ConsumerEventArgs> Registered;

        ///<summary>Event fired on HandleModelShutdown.</summary>
        public event AsyncEventHandler<ShutdownEventArgs> Shutdown;

        ///<summary>Event fired on HandleBasicCancelOk.</summary>
        public event AsyncEventHandler<ConsumerEventArgs> Unregistered;

        ///<summary>Fires the Unregistered event.</summary>
        public override async Task HandleBasicCancelOk(string consumerTag)
        {
            await base.HandleBasicCancelOk(consumerTag).ConfigureAwait(false);
            await (Unregistered?.Invoke(this, new ConsumerEventArgs(new[] { consumerTag })) ?? Task.CompletedTask).ConfigureAwait(false);
        }

        ///<summary>Fires the Registered event.</summary>
        public override async Task HandleBasicConsumeOk(string consumerTag)
        {
            await base.HandleBasicConsumeOk(consumerTag).ConfigureAwait(false);
            await (Registered?.Invoke(this, new ConsumerEventArgs(new[] { consumerTag })) ?? Task.CompletedTask).ConfigureAwait(false);
        }

        ///<summary>Fires the Received event.</summary>
        public override async Task HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body)
        {
            await base.HandleBasicDeliver(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body).ConfigureAwait(false);
            await (Received?.Invoke(
                this,
                new BasicDeliverEventArgs(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body)) ?? Task.CompletedTask).ConfigureAwait(false);
        }

        ///<summary>Fires the Shutdown event.</summary>
        public override async Task HandleModelShutdown(object model, ShutdownEventArgs reason)
        {
            await base.HandleModelShutdown(model, reason).ConfigureAwait(false);
            await (Shutdown?.Invoke(this, reason) ?? Task.CompletedTask).ConfigureAwait(false);
        }
    }
}
