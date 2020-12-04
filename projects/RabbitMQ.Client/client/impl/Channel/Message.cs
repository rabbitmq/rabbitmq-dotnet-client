using System;

namespace RabbitMQ.Client.client.impl.Channel
{
    #nullable enable
    /// <summary>
    /// The message and its information.
    /// </summary>
    public readonly struct Message
    {
        /// <summary>
        /// The exchange the message was sent to.
        /// </summary>
        public readonly string Exchange;

        /// <summary>
        /// The routing key used for the message.
        /// </summary>
        public readonly string RoutingKey;

        /// <summary>
        /// The properties sent with the message.
        /// </summary>
        public readonly IBasicProperties Properties;

        /// <summary>
        /// The body of the message.
        /// </summary>
        public readonly ReadOnlyMemory<byte> Body;

        /// <summary>
        /// Creates a new instance of <see cref="Message"/>
        /// </summary>
        /// <param name="exchange">The exchange the message was sent to.</param>
        /// <param name="routingKey">The routing key used for the message.</param>
        /// <param name="properties">The properties sent with the message.</param>
        /// <param name="body">The body of the message.</param>
        public Message(string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            Exchange = exchange;
            RoutingKey = routingKey;
            Properties = properties;
            Body = body;
        }
    }
}
