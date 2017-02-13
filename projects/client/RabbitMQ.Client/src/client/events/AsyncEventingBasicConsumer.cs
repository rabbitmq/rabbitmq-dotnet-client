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
            Received = (model1, args) => TaskExtensions.CompletedTask;
            Registered = (model1, args) => TaskExtensions.CompletedTask;
            Unregistered = (model1, args) => TaskExtensions.CompletedTask;
            Shutdown = (model1, args) => TaskExtensions.CompletedTask;
        }

        public Func<IModel, BasicDeliverEventArgs, Task> Received { get; set; }

        public Func<IModel, ConsumerEventArgs, Task> Registered { get; set; }
        public Func<IModel, ConsumerEventArgs, Task> Unregistered { get; set; }
        public Func<IModel, ShutdownEventArgs, Task> Shutdown { get; set; }

        public override Task HandleBasicCancelOk(string consumerTag)
        {
            return base.HandleBasicCancelOk(consumerTag).ContinueWith(t => Unregistered(Model, new ConsumerEventArgs(consumerTag))).Unwrap();
        }

        public override Task HandleBasicConsumeOk(string consumerTag)
        {
            return base.HandleBasicCancelOk(consumerTag).ContinueWith(t => Registered(Model, new ConsumerEventArgs(consumerTag))).Unwrap();
        }

        public override Task HandleBasicDeliver(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties properties,
            byte[] body)
        {
            return base.HandleBasicDeliver(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body)
                .ContinueWith(t => Received(Model, new BasicDeliverEventArgs(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body)))
                .Unwrap();
        }

        public override Task HandleModelShutdown(object model, ShutdownEventArgs reason)
        {
            return base.HandleModelShutdown(model, reason).ContinueWith(t => Shutdown(Model, reason)).Unwrap();
        }
    }
}