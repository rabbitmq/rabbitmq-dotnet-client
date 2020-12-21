using System;
using System.Collections.Generic;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.client.impl.Channel
{
    #nullable enable
    /// <summary>
    /// A batch of messages.
    /// </summary>
    public sealed class MessageBatch
    {
        private readonly List<OutgoingCommand> _commands;

        internal List<OutgoingCommand> Commands => _commands;

        /// <summary>
        /// Creates a new instance of <see cref="MessageBatch"/> which can be sent by <see cref="IChannel.PublishMessageAsync"/>.
        /// </summary>
        public MessageBatch()
        {
            _commands = new List<OutgoingCommand>();
        }

        /// <summary>
        /// Creates a new instance of <see cref="MessageBatch"/> which can be sent by <see cref="IChannel.PublishMessageAsync"/>.
        /// </summary>
        /// <param name="sizeHint">The initial capacity of the message list.</param>
        public MessageBatch(int sizeHint)
        {
            _commands = new List<OutgoingCommand>(sizeHint);
        }

        /// <summary>
        /// Adds a message to the batch.
        /// </summary>
        /// <param name="exchange">The exchange to publish it to.</param>
        /// <param name="routingKey">The routing key to use. Must be shorter than 255 bytes.</param>
        /// <param name="basicProperties">The properties sent along with the body.</param>
        /// <param name="body">The body to send.</param>
        /// <param name="mandatory">Whether or not to raise a <see cref="IChannel.MessageDeliveryFailed"/> if the message could not be routed to a queue.</param>
        public void Add(string exchange, string routingKey, IBasicProperties? basicProperties, ReadOnlyMemory<byte> body, bool mandatory)
        {
            var method = new BasicPublish
            {
                _exchange = exchange,
                _routingKey = routingKey,
                _mandatory = mandatory
            };

            _commands.Add(new OutgoingCommand(method, (ContentHeaderBase?)basicProperties ?? Channel.EmptyBasicProperties, body));
        }
    }
}
