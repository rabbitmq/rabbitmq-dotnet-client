using System;
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
        ///  See <see cref="HandleBasicCancelOk"/> for notification of consumer cancellation due to basicCancel
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        Task HandleBasicCancel(string consumerTag);

        /// <summary>
        /// Called upon successful deregistration of the consumer from the broker.
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        Task HandleBasicCancelOk(string consumerTag);

        /// <summary>
        /// Called upon successful registration of the consumer with the broker.
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        Task HandleBasicConsumeOk(string consumerTag);

        /// <summary>
        /// Called each time a message arrives for this consumer.
        /// </summary>
        /// <remarks>
        /// Does nothing with the passed in information.
        /// Note that in particular, some delivered messages may require acknowledgement via <see cref="IChannel.BasicAckAsync"/>.
        /// The implementation of this method in this class does NOT acknowledge such messages.
        /// </remarks>
        Task HandleBasicDeliver(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            ReadOnlyMemory<byte> exchange,
            ReadOnlyMemory<byte> routingKey,
            in ReadOnlyBasicProperties properties,
            ReadOnlyMemory<byte> body);

        /// <summary>
        ///  Called when the channel shuts down.
        ///  </summary>
        ///  <param name="channel"> Common AMQP channel.</param>
        /// <param name="reason"> Information about the reason why a particular channel, session, or connection was destroyed.</param>
        Task HandleChannelShutdown(object channel, ShutdownEventArgs reason);
    }
}
