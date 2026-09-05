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
        /// Read and written with <see cref="Volatile"/>, because these are set from a dispatcher
        /// worker and read from application threads. Without that a polling loop can be optimised
        /// into a single read and never observe a change.
        /// </remarks>
        public bool IsRunning => Volatile.Read(ref _isRunning);

        /// <summary>
        /// If our <see cref="IChannel"/> shuts down, this property will contain a description of the reason for the
        /// shutdown. Otherwise it will contain null. See <see cref="ShutdownEventArgs"/>.
        /// </summary>
        /// <remarks>
        /// The default implementation of <see cref="HandleBasicConsumeOkAsync"/> clears this when the
        /// broker confirms a registration, which includes automatic recovery re-registering the
        /// consumer after a connection drop. A subclass that overrides that method without calling
        /// the base implementation does not clear it.
        /// <para>
        /// A reason therefore survives whenever no registration was confirmed afterwards. That
        /// includes the default configuration: consumer recovery can fail per consumer, for instance
        /// when the queue is gone after the drop, and recovery still reports success, so a non-null
        /// reason after a recovery is the signal that this consumer was not restored. It also
        /// includes a consumer excluded from topology recovery, whether by disabling it or through a
        /// <see cref="TopologyRecoveryFilter"/>. A registration confirmed after the channel has begun
        /// shutting down does not clear it either, so a consumer on a dead channel keeps reporting
        /// the shutdown rather than appearing healthy.
        /// </para>
        /// <para>
        /// Two cautions. The value is per consumer instance, not per consumer tag, so for an instance
        /// registered under several tags a single confirmed registration clears the reason even if
        /// the other registrations were not restored. And unlike previous versions the value can go
        /// from non-null back to null, so do not test it and then dereference the result; copy it to
        /// a local first.
        /// </para>
        /// <para>
        /// This and <see cref="IsRunning"/> are written separately and never published as a pair, at
        /// any dispatch concurrency, so a reader can observe one updated and not the other. Above a
        /// concurrency of one they are also written from concurrent workers. Note that a channel's
        /// concurrency comes from <see cref="CreateChannelOptions.ConsumerDispatchConcurrency"/> when
        /// set, which takes precedence over <see cref="IConnectionFactory.ConsumerDispatchConcurrency"/>.
        /// See rabbitmq/rabbitmq-dotnet-client#2006 and rabbitmq/rabbitmq-dotnet-client#2016.
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
             * The broker has accepted this registration, so clear any reason left from an earlier
             * shutdown; see the ShutdownReason docs for the full contract. The token is the
             * dispatcher's shutdown token, cancelled by Quiesce(), and it is what keeps this reset
             * fail-safe: a registration confirmed after the channel began shutting down must not
             * clear the reason, or a consumer on a permanently dead channel would report a null
             * reason with IsRunning true, which reads as fully healthy. That interleaving is
             * reachable because the shutdown work item and an already-enqueued consume-ok are
             * ordered only by the dispatcher queue. See rabbitmq/rabbitmq-dotnet-client#2006.
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
