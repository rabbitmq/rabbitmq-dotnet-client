using System;
using System.Threading.Tasks;

using TaskExtensions = RabbitMQ.Client.Impl.TaskExtensions;

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
            await Raise(Unregistered, new ConsumerEventArgs(new []{consumerTag})).ConfigureAwait(false);
        }

        ///<summary>Fires the Registered event.</summary>
        public override async Task HandleBasicConsumeOk(string consumerTag)
        {
            await base.HandleBasicConsumeOk(consumerTag).ConfigureAwait(false);
            await Raise(Registered, new ConsumerEventArgs(new[] { consumerTag })).ConfigureAwait(false);
        }

        ///<summary>Fires the Received event.</summary>
        public override async Task HandleBasicDeliver(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties properties,
            byte[] body)
        {
            await base.HandleBasicDeliver(consumerTag,
                deliveryTag,
                redelivered,
                exchange,
                routingKey,
                properties,
                body).ConfigureAwait(false);
            await Raise(Received, new BasicDeliverEventArgs(consumerTag,
                deliveryTag,
                redelivered,
                exchange,
                routingKey,
                properties,
                body)).ConfigureAwait(false);
        }

        ///<summary>Fires the Shutdown event.</summary>
        public override async Task HandleModelShutdown(object model, ShutdownEventArgs reason)
        {
            await base.HandleModelShutdown(model, reason).ConfigureAwait(false);
            await Raise(Shutdown, reason).ConfigureAwait(false);
        }

        private Task Raise<TEvent>(AsyncEventHandler<TEvent> eventHandler, TEvent evt) 
            where TEvent : EventArgs
        {
            AsyncEventHandler<TEvent> handler = eventHandler;
            if (handler != null)
            {
                return handler(this, evt);
            }
            return TaskExtensions.CompletedTask;
        }
    }
}