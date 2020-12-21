using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabbitMQ.Client.client.impl.Channel
{
    #nullable enable
    /// <summary>
    /// Extension methods for the <see cref="IChannel"/>.
    /// </summary>
    public static class ChannelExtensions
    {
        /// <summary>
        /// Declares an exchange.
        /// </summary>
        /// <param name="channel">The channel to perform this operation on.</param>
        /// <param name="exchange">The name of the exchange.</param>
        /// <param name="type">The type of exchange (<see cref="ExchangeType"/>).</param>
        /// <param name="waitForConfirmation">Whether or not to wait for server confirmation.</param>
        /// <remarks>AMQP: Exchange.Declare</remarks>
        public static ValueTask DeclareExchangeAsync(this IChannel channel, string exchange, string type, bool waitForConfirmation = true)
        {
            return channel.DeclareExchangeAsync(exchange, type, false, false, waitForConfirmation: waitForConfirmation);
        }

        /// <summary>
        /// Declares an exchange passively. (not throwing on mismatch)
        /// </summary>
        /// <param name="channel">The channel to perform this operation on.</param>
        /// <param name="exchange">The name of the exchange.</param>
        /// <param name="waitForConfirmation">Whether or not to wait for server confirmation.</param>
        /// <remarks>AMQP: Exchange.Declare</remarks>
        public static ValueTask DeclareExchangePassiveAsync(this IChannel channel, string exchange, bool waitForConfirmation = true)
        {
            return channel.DeclareExchangeAsync(exchange, "", false, false, false, waitForConfirmation: waitForConfirmation);
        }

        /// <summary>
        /// Declares an queue.
        /// </summary>
        /// <param name="channel">The channel to perform this operation on.</param>
        /// <param name="queue">The name of the queue or empty if the server shall generate a name.</param>
        /// <param name="durable">Durable queues remain active when a server restarts.</param>
        /// <param name="exclusive">Exclusive queues may only be accessed by the current connection, and are deleted when that connection closes.</param>
        /// <param name="autoDelete">Whether or not the queue is deleted when all consumers have finished using it.</param>
        /// <param name="throwOnMismatch">Whether or not to throw an exception on mismatch.</param>
        /// <param name="arguments">A set of arguments for the declare. The syntax and semantics of these arguments depends on the server implementation.</param>
        /// <returns>The name of the queue, the current message count and the current consumer count.</returns>
        /// <remarks>AMQP: Queue.Declare</remarks>
        public static ValueTask<(string QueueName, uint MessageCount, uint ConsumerCount)> DeclareQueueAsync(this IChannel channel,
            string queue = "", bool durable = false, bool exclusive = true, bool autoDelete = true, bool throwOnMismatch = true, IDictionary<string, object>? arguments = null)
        {
            return channel.DeclareQueueAsync(queue, durable, exclusive, autoDelete, throwOnMismatch, arguments);
        }

        /// <summary>
        /// Declares an queue passively. (not throwing on mismatch)
        /// </summary>
        /// <param name="channel">The channel to perform this operation on.</param>
        /// <param name="queue">The name of the queue or empty if the server shall generate a name.</param>
        /// <param name="arguments">A set of arguments for the declare. The syntax and semantics of these arguments depends on the server implementation.</param>
        /// <returns>The name of the queue, the current message count and the current consumer count.</returns>
        /// <remarks>AMQP: Queue.Declare</remarks>
        public static ValueTask<(string QueueName, uint MessageCount, uint ConsumerCount)> DeclareQueuePassiveAsync(this IChannel channel, string queue, IDictionary<string, object>? arguments = null)
        {
            return channel.DeclareQueueAsync(queue, false, true, true, false, arguments);
        }

        /// <summary>
        /// Gets the number of messages in the queue.
        /// </summary>
        /// <param name="channel">The channel to perform this operation on.</param>
        /// <param name="queue">The name of the queue.</param>
        /// <returns>The current message count.</returns>
        /// <remarks>AMQP: Queue.Declare</remarks>
        public static async ValueTask<uint> GetQueueMessageCountAsync(this IChannel channel, string queue)
        {
            var result = await channel.DeclareQueueAsync(queue, false, false, false, false).ConfigureAwait(false);
            return result.MessageCount;
        }

        /// <summary>
        /// Gets the number of consumers in the queue.
        /// </summary>
        /// <param name="channel">The channel to perform this operation on.</param>
        /// <param name="queue">The name of the queue.</param>
        /// <returns>The current consumer count.</returns>
        /// <remarks>AMQP: Queue.Declare</remarks>
        public static async ValueTask<uint> GetQueueConsumerCountAsync(this IChannel channel, string queue)
        {
            var result = await channel.DeclareQueueAsync(queue, false, false, false, false).ConfigureAwait(false);
            return result.ConsumerCount;
        }

        /// <summary>
        /// Closes the channel.
        /// </summary>
        /// <param name="channel">The channel to perform this operation on.</param>
        /// <remarks>AMQP: Connection.Close</remarks>
        public static ValueTask CloseAsync(this IChannel channel)
        {
            return channel.CloseAsync(Constants.ReplySuccess, "Goodbye");
        }

        /// <summary>
        /// Aborts the channel.
        /// In comparison to normal <see cref="CloseAsync"/> method, <see cref="AbortAsync"/> will not throw
        /// <see cref="Exceptions.AlreadyClosedException"/> or <see cref="System.IO.IOException"/> or any other <see cref="System.Exception"/> during closing model.
        /// </summary>
        /// <param name="channel">The channel to perform this operation on.</param>
        /// <remarks>AMQP: Connection.Close</remarks>
        public static ValueTask AbortAsync(this IChannel channel)
        {
            return channel.AbortAsync(Constants.ReplySuccess, "Goodbye");
        }
    }
}
