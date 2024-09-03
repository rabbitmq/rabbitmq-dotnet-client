using System;
using System.Threading.Tasks;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Consumer interface. Used to receive messages from a queue by subscription.
    /// </summary>
    public interface IAsyncBasicConsumer
    {
        /// <summary>
        /// Retrieve the <see cref="IChannel"/> this consumer is associated with,
        /// for use in acknowledging received messages, for instance.
        /// </summary>
        IChannel? Channel { get; }

        /// <summary>
        /// Called when the consumer is cancelled for reasons other than by a basicCancel:
        /// e.g. the queue has been deleted (either by this channel or by any other channel).
        /// See <see cref="HandleBasicCancelOkAsync"/> for notification of consumer cancellation due to basicCancel
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
        /// <remarks>
        ///  <para>
        ///   Does nothing with the passed in information.
        ///   Note that in particular, some delivered messages may require acknowledgement via <see cref="IChannel.BasicAckAsync"/>.
        ///   The implementation of this method in this class does NOT acknowledge such messages.
        ///  </para>
        ///  <para>
        ///    NOTE: Using the <c>body</c> outside of
        ///    <c><seealso cref="IAsyncBasicConsumer.HandleBasicDeliverAsync(string, ulong, bool, string, string, IReadOnlyBasicProperties, ReadOnlyMemory{byte})"/></c>
        ///    requires that it be copied!
        ///  </para>
        /// </remarks>
        ///</summary>
        Task HandleBasicDeliverAsync(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IReadOnlyBasicProperties properties,
            ReadOnlyMemory<byte> body);

        /// <summary>
        /// Called when the channel shuts down.
        /// </summary>
        /// <param name="channel">Common AMQP channel.</param>
        /// <param name="reason">Information about the reason why a particular channel, session, or connection was destroyed.</param>
        Task HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason);
    }
}
