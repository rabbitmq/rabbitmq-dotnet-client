using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client
{
    public class AsyncDefaultBasicConsumer : IBasicConsumer, IAsyncBasicConsumer
    {
        private readonly HashSet<string> _consumerTags = new HashSet<string>();

        /// <summary>
        /// Creates a new instance of an <see cref="DefaultBasicConsumer"/>.
        /// </summary>
        public AsyncDefaultBasicConsumer()
        {
        }

        /// <summary>
        /// Constructor which sets the Model property to the given value.
        /// </summary>
        /// <param name="model">Common AMQP model.</param>
        public AsyncDefaultBasicConsumer(IModel model)
        {
            Model = model;
        }

        /// <summary>
        /// Retrieve the consumer tags this consumer is registered as; to be used when discussing this consumer
        /// with the server, for instance with <see cref="IModel.BasicCancel"/>.
        /// </summary>
        public string[] ConsumerTags
        {
            get
            {
                return _consumerTags.ToArray();
            }
        }

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
        /// Signalled when the consumer gets cancelled.
        /// </summary>
        public event AsyncEventHandler<ConsumerEventArgs> ConsumerCancelled
        {
            add => _consumerCancelledWrapper.AddHandler(value);
            remove => _consumerCancelledWrapper.RemoveHandler(value);
        }
        private AsyncEventingWrapper<ConsumerEventArgs> _consumerCancelledWrapper;

        /// <summary>
        /// Retrieve the <see cref="IModel"/> this consumer is associated with,
        ///  for use in acknowledging received messages, for instance.
        /// </summary>
        public IModel Model { get; set; }

        /// <summary>
        ///  Called when the consumer is cancelled for reasons other than by a basicCancel:
        ///  e.g. the queue has been deleted (either by this channel or  by any other channel).
        ///  See <see cref="HandleBasicCancelOk"/> for notification of consumer cancellation due to basicCancel
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        public virtual Task HandleBasicCancel(string consumerTag)
        {
            return OnCancel(consumerTag);
        }

        /// <summary>
        /// Called upon successful deregistration of the consumer from the broker.
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        public virtual Task HandleBasicCancelOk(string consumerTag)
        {
            return OnCancel(consumerTag);
        }

        /// <summary>
        /// Called upon successful registration of the consumer with the broker.
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        public virtual Task HandleBasicConsumeOk(string consumerTag)
        {
            _consumerTags.Add(consumerTag);
            IsRunning = true;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called each time a message is delivered for this consumer.
        /// </summary>
        /// <remarks>
        /// This is a no-op implementation. It will not acknowledge deliveries via <see cref="IModel.BasicAck"/>
        /// if consuming in automatic acknowledgement mode.
        /// Subclasses must copy or fully use delivery body before returning.
        /// Accessing the body at a later point is unsafe as its memory can
        /// be already released.
        /// </remarks>
        public virtual Task HandleBasicDeliver(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties properties,
            ReadOnlyMemory<byte> body)
        {
            // Nothing to do here.
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the model (channel) this consumer was registered on terminates.
        /// </summary>
        /// <param name="model">A channel this consumer was registered on.</param>
        /// <param name="reason">Shutdown context.</param>
        public virtual Task HandleModelShutdown(object model, ShutdownEventArgs reason)
        {
            ShutdownReason = reason;
            return OnCancel(_consumerTags.ToArray());
        }

        /// <summary>
        /// Default implementation - overridable in subclasses.</summary>
        /// <param name="consumerTags">The set of consumer tags that where cancelled</param>
        /// <remarks>
        /// This default implementation simply sets the <see cref="IsRunning"/> property to false, and takes no further action.
        /// </remarks>
        public virtual async Task OnCancel(params string[] consumerTags)
        {
            IsRunning = false;
            if (!_consumerCancelledWrapper.IsEmpty)
            {
                await _consumerCancelledWrapper.InvokeAsync(this, new ConsumerEventArgs(consumerTags)).ConfigureAwait(false);
            }
            foreach (string consumerTag in consumerTags)
            {
                _consumerTags.Remove(consumerTag);
            }
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

        void IBasicConsumer.HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
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
