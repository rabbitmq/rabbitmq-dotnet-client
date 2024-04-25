using System;
using System.Threading;
using System.Threading.Tasks;

using RabbitMQ.Client.Events;

namespace RabbitMQ.Client
{
    public interface IAsyncBasicConsumer
    {
        /// <summary>
        /// Retrieve the <see cref="IChannel"/> this consumer is associated with,
        ///  for use in acknowledging received messages, for instance.
        /// </summary>
        IChannel Channel { get; }

        /// <summary>
        /// Signalled when the consumer gets cancelled.
        /// </summary>
        event AsyncEventHandler<ConsumerEventArgs> ConsumerCancelled;

        /// <summary>
        ///  Called when the consumer is cancelled for reasons other than by a basicCancel:
        ///  e.g. the queue has been deleted (either by this channel or  by any other channel).
        ///  See <see cref="HandleBasicCancelOkAsync"/> for notification of consumer cancellation due to basicCancel
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        Task HandleBasicCancelAsync(string consumerTag);

        /// <summary>
        /// Called upon successful deregistration of the consumer from the broker.
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        Task HandleBasicCancelOkAsync(string consumerTag);

        /// <summary>
        /// Called upon successful registration of the consumer with the broker.
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        Task HandleBasicConsumeOkAsync(string consumerTag);

        /// <summary>
        /// Called each time a message arrives for this consumer.
        /// </summary>
        /// <remarks>
        /// Does nothing with the passed in information.
        /// Note that in particular, some delivered messages may require acknowledgement via <see cref="IChannel.BasicAckAsync"/>.
        /// The implementation of this method in this class does NOT acknowledge such messages.
        /// </remarks>
        Task HandleBasicDeliverAsync(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            ReadOnlyBasicProperties properties,
            ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///  Called when the channel shuts down.
        ///  </summary>
        ///  <param name="channel"> Common AMQP channel.</param>
        /// <param name="reason"> Information about the reason why a particular channel, session, or connection was destroyed.</param>
        Task HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason);
    }
}
