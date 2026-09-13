using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

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

        private bool _isRunning;
        private ShutdownEventArgs? _shutdownReason;

        /// <summary>
        /// Returns true while the consumer is registered and expecting deliveries from the broker.
        /// </summary>
        /// <remarks>
        /// Safe to poll from another thread. See <see cref="ShutdownReason"/> for the case where this
        /// stays <c>true</c> on a channel that has shut down.
        /// </remarks>
        public bool IsRunning => Volatile.Read(ref _isRunning);

        /// <summary>
        /// If our <see cref="IChannel"/> shuts down, this property will contain a description of the reason for the
        /// shutdown. Otherwise it will contain null. See <see cref="ShutdownEventArgs"/>.
        /// </summary>
        /// <remarks>
        /// Cleared when the broker confirms a registration, which includes automatic recovery
        /// re-registering the consumer after a connection drop. So unlike previous versions the value
        /// can go from non-null back to null: copy it to a local before dereferencing it.
        /// <para>
        /// A reason that survives a recovery is the signal that this consumer was not restored -
        /// recovery reports success even when an individual consumer could not be recovered. It is per
        /// consumer instance rather than per tag, so for an instance registered under several tags one
        /// confirmed registration clears it.
        /// </para>
        /// <para>
        /// <see cref="IsRunning"/> can be <c>true</c> at the same time as a non-null reason, and stays
        /// that way: trust the reason and do not wait for <see cref="IsRunning"/> to go false. The two
        /// are written separately, so above a <c>ConsumerDispatchConcurrency</c> of one they can also
        /// be observed disagreeing transiently - see rabbitmq/rabbitmq-dotnet-client#2016.
        /// </para>
        /// </remarks>
        public ShutdownEventArgs? ShutdownReason => Volatile.Read(ref _shutdownReason);

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

            /*
             * Skipped when the dispatcher's shutdown token is already cancelled, so a registration
             * confirmed after the channel began shutting down cannot clear the reason and leave a
             * dead channel reading as healthy. Reachable for reasons that are not the obvious ones:
             * see docs/internal/connection-shutdown-and-cancellation.md, issue #2006.
             */
            if (false == cancellationToken.IsCancellationRequested)
            {
                Volatile.Write(ref _shutdownReason, null);
            }

            Volatile.Write(ref _isRunning, true);
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
            Volatile.Write(ref _shutdownReason, reason);
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
            Volatile.Write(ref _isRunning, false);

            foreach (string consumerTag in consumerTags)
            {
                _consumerTags.Remove(consumerTag);
            }

            return Task.CompletedTask;
        }
    }
}
