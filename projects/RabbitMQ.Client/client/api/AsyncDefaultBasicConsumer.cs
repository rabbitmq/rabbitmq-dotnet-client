using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client
{
    public class AsyncDefaultBasicConsumer : IAsyncBasicConsumer
    {
        private readonly HashSet<string> _consumerTags = new HashSet<string>();

        /// <summary>
        /// Constructor which sets the Channel property to the given value.
        /// </summary>
        /// <param name="channel">Common AMQP channel.</param>
        public AsyncDefaultBasicConsumer(IChannel channel)
        {
            Channel = channel;
        }

        /// <summary>
        /// Retrieve the consumer tags this consumer is registered as; to be used when discussing this consumer
        /// with the server, for instance with <see cref="IChannel.BasicCancelAsync"/>.
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
        public bool IsRunning { get; private set; }

        /// <summary>
        /// If our <see cref="IChannel"/> shuts down, this property will contain a description of the reason for the
        /// shutdown. Otherwise it will contain null. See <see cref="ShutdownEventArgs"/>.
        /// </summary>
        public ShutdownEventArgs? ShutdownReason { get; private set; }

        /// <summary>
        /// Retrieve the <see cref="IChannel"/> this consumer is associated with,
        ///  for use in acknowledging received messages, for instance.
        /// </summary>
        public IChannel Channel { get; }

        /// <summary>
        ///  Called when the consumer is cancelled for reasons other than by a basicCancel:
        ///  e.g. the queue has been deleted (either by this channel or  by any other channel).
        ///  See <see cref="HandleBasicCancelOkAsync"/> for notification of consumer cancellation due to basicCancel
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public virtual Task HandleBasicCancelAsync(string consumerTag, CancellationToken cancellationToken = default)
        {
            return OnCancelAsync(new[] { consumerTag }, cancellationToken);
        }

        /// <summary>
        /// Called upon successful deregistration of the consumer from the broker.
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public virtual Task HandleBasicCancelOkAsync(string consumerTag, CancellationToken cancellationToken = default)
        {
            return OnCancelAsync(new[] { consumerTag }, cancellationToken);
        }

        /// <summary>
        /// Called upon successful registration of the consumer with the broker.
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public virtual Task HandleBasicConsumeOkAsync(string consumerTag, CancellationToken cancellationToken = default)
        {
            _consumerTags.Add(consumerTag);
            IsRunning = true;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called each time a message is delivered for this consumer.
        /// </summary>
        /// <remarks>
        /// This is a no-op implementation. It will not acknowledge deliveries via <see cref="IChannel.BasicAckAsync"/>
        /// if consuming in automatic acknowledgement mode.
        /// Subclasses must copy or fully use delivery body before returning.
        /// Accessing the body at a later point is unsafe as its memory can
        /// be already released.
        /// </remarks>
        public virtual Task HandleBasicDeliverAsync(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IReadOnlyBasicProperties properties,
            ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken = default)
        {
            // Nothing to do here.
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the channel (channel) this consumer was registered on terminates.
        /// </summary>
        /// <param name="channel">A channel this consumer was registered on.</param>
        /// <param name="reason">Shutdown context.</param>
        public virtual Task HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason)
        {
            ShutdownReason = reason;
            return OnCancelAsync(ConsumerTags, reason.CancellationToken);
        }

        /// <summary>
        /// Default implementation - overridable in subclasses.</summary>
        /// <param name="consumerTags">The set of consumer tags that were cancelled</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <remarks>
        /// This default implementation simply sets the <see cref="IsRunning"/> property to false, and takes no further action.
        /// </remarks>
        protected virtual Task OnCancelAsync(string[] consumerTags, CancellationToken cancellationToken = default)
        {
            IsRunning = false;

            foreach (string consumerTag in consumerTags)
            {
                _consumerTags.Remove(consumerTag);
            }

            return Task.CompletedTask;
        }
    }
}
