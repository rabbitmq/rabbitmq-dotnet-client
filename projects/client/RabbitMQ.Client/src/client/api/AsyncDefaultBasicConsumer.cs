using System;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using TaskExtensions = RabbitMQ.Client.Impl.TaskExtensions;

namespace RabbitMQ.Client
{
    public class AsyncDefaultBasicConsumer : IBasicConsumer, IAsyncBasicConsumer
    {
        /// <summary>
        /// Creates a new instance of an <see cref="AsyncDefaultBasicConsumer"/>.
        /// </summary>
        public AsyncDefaultBasicConsumer()
        {
            ShutdownReason = null;
            Model = null;
            IsRunning = false;
            ConsumerTag = null;
        }

        /// <summary>
        /// Constructor which sets the Model property to the given value.
        /// </summary>
        /// <param name="model">Common AMQP model.</param>
        public AsyncDefaultBasicConsumer(IModel model)
        {
            ShutdownReason = null;
            IsRunning = false;
            ConsumerTag = null;
            Model = model;
        }

        /// <summary>
        /// Retrieve the consumer tag this consumer is registered as; to be used when discussing this consumer
        /// with the server, for instance with <see cref="IModel.BasicCancel"/>.
        /// </summary>
        public string ConsumerTag { get; protected set; }

        /// <summary>
        /// Returns true while the consumer is registered and expecting deliveries from the broker.
        /// </summary>
        public bool IsRunning { get; protected set; }

        /// <summary>
        /// If our <see cref="IModel"/> shuts down, this property will contain a description of the reason for the
        /// shutdown. Otherwise it will contain null. See <see cref="ShutdownEventArgs"/>.
        /// </summary>
        public ShutdownEventArgs ShutdownReason { get; protected set; }

        /// <summary>
        /// Retrieve the <see cref="IModel"/> this consumer is associated with,
        ///  for use in acknowledging received messages, for instance.
        /// </summary>
        public IModel Model { get; protected set; }

        /// <summary>
        ///  Called when the consumer is cancelled for reasons other than by a basicCancel:
        ///  e.g. the queue has been deleted (either by this channel or  by any other channel).
        ///  See <see cref="HandleBasicCancelOk"/> for notification of consumer cancellation due to basicCancel
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        public virtual Task HandleBasicCancel(string consumerTag)
        {
            IsRunning = false;
            return TaskExtensions.CompletedTask;
        }

        /// <summary>
        /// Called upon successful deregistration of the consumer from the broker.
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        public virtual Task HandleBasicCancelOk(string consumerTag)
        {
            IsRunning = false;
            return TaskExtensions.CompletedTask;
        }

        /// <summary>
        /// Called upon successful registration of the consumer with the broker.
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        public virtual Task HandleBasicConsumeOk(string consumerTag)
        {
            ConsumerTag = consumerTag;
            IsRunning = true;
            return TaskExtensions.CompletedTask;
        }

        /// <summary>
        /// Called each time a message arrives for this consumer.
        /// </summary>
        /// <remarks>
        /// Does nothing with the passed in information.
        /// Note that in particular, some delivered messages may require acknowledgement via <see cref="IModel.BasicAck"/>.
        /// The implementation of this method in this class does NOT acknowledge such messages.
        /// </remarks>
        public virtual Task HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body)
        {
            return TaskExtensions.CompletedTask;
        }

        /// <summary>
        ///  Called when the model shuts down.
        ///  </summary>
        ///  <param name="model"> Common AMQP model.</param>
        /// <param name="reason"> Information about the reason why a particular model, session, or connection was destroyed.</param>
        public virtual Task HandleModelShutdown(object model, ShutdownEventArgs reason)
        {
            ShutdownReason = reason;
            return TaskExtensions.CompletedTask;
        }

        event EventHandler<ConsumerEventArgs> IBasicConsumer.ConsumerCancelled
        {
            add { throw new InvalidOperationException("Should never be called."); }
            remove { throw new InvalidOperationException("Should never be called."); }
        }

        void IBasicConsumer.HandleBasicCancelOk(string consumerTag)
        {
            throw new InvalidOperationException("Should never be called.");
        }

        void IBasicConsumer.HandleBasicConsumeOk(string consumerTag)
        {
            throw new InvalidOperationException("Should never be called.");
        }

        void IBasicConsumer.HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body)
        {
            throw new InvalidOperationException("Should never be called.");
        }

        void IBasicConsumer.HandleModelShutdown(object model, ShutdownEventArgs reason)
        {
            throw new InvalidOperationException("Should never be called.");
        }

        void IBasicConsumer.HandleBasicCancel(string consumerTag)
        {
            throw new InvalidOperationException("Should never be called.");
        }
    }
}