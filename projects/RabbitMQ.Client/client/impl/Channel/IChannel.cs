using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabbitMQ.Client.client.impl.Channel
{
    #nullable enable
    public interface IChannel : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Gets the channel number, unique per connections.
        /// </summary>
        int ChannelNumber { get; }

        /// <summary>
        /// Gets the timeout for protocol operations (e.g. <code>queue.declare</code>).
        /// </summary>
        TimeSpan ContinuationTimeout { get; set; }

        /// <summary>
        /// Gets a value that indicates whether the channel is still in a state where it can be used.
        /// Identical to checking if <see cref="CloseReason"/> equals null.
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// Gets the reason why this channel was closed or <c>null</c> if it is still open.
        /// </summary>
        ShutdownEventArgs? CloseReason { get; }

        /// <summary>
        /// Occurs when an exception was caught during a callback invoked by the channel.
        /// Example are exceptions thrown during <see cref="IBasicConsumer"/> methods.
        /// Exception - ex - the unhandled exception.
        /// Dictionary{string, object}? - context - the context of the unhandled exception.
        /// </summary>
        event Action<Exception, Dictionary<string, object>?>? UnhandledExceptionOccurred;

        /// <summary>
        /// Occurs when the flow control has changed.
        /// bool - active - whether or not the flow control is active.
        /// </summary>
        event Action<bool>? FlowControlChanged;

        /// <summary>
        /// Configures the quality of service parameters.
        /// </summary>
        /// <param name="prefetchSize">The number of messages be sent in advance so that when the client finishes processing a message, the following message is already held locally, rather than needing to be sent down the channel.</param>
        /// <param name="prefetchCount">Specifies a prefetch window in terms of not yet acknowledged messages. The prefetch-count is ignored if the no-ack option is set.</param>
        /// <param name="global">Whether or not to set only for this channel or global.</param>
        /// <remarks>AMQP: Basic.Qos</remarks>
        ValueTask SetQosAsync(uint prefetchSize, ushort prefetchCount, bool global);

        /// <summary>
        /// Acknowledges one or more delivered message(s).
        /// </summary>
        /// <param name="deliveryTag">The delivery tag to acknowledge.</param>
        /// <param name="multiple">Multiple means up to and including to the delivery tag, false is just the delivery tag.</param>
        /// <remarks>AMQP: Basic.Ack</remarks>
        ValueTask AckMessageAsync(ulong deliveryTag, bool multiple);

        /// <summary>
        /// Negative acknowledges one or more delivered message(s).
        /// </summary>
        /// <param name="deliveryTag">The delivery tag to negative acknowledge.</param>
        /// <param name="multiple">Multiple means up to and including to the delivery tag, false is just the delivery tag.</param>
        /// <param name="requeue">If requeue, the server will attempt to requeue the message, otherwise messages are discarded or dead-lettered.</param>
        /// <remarks>AMQP: Basic.Nack</remarks>
        ValueTask NackMessageAsync(ulong deliveryTag, bool multiple, bool requeue);

        /// <summary>
        /// Activates a consumer to receive messages.
        /// </summary>
        /// <param name="consumer">The consumer to activate.</param>
        /// <param name="queue">The name of the queue to consume from.</param>
        /// <param name="autoAck">Whether or not messages need to be acknowledged by <see cref="AckMessageAsync"/> or <see cref="NackMessageAsync"/>.</param>
        /// <param name="consumerTag">Specifies the identifier for the consumer. If this field is empty the server will generate a unique tag.</param>
        /// <param name="noLocal">If the no-local field is set the server will not send messages to the connection that published them.</param>
        /// <param name="exclusive">Request exclusive consumer access, meaning only this consumer can access the queue.</param>
        /// <param name="arguments">A set of arguments for the consume. The syntax and semantics of these arguments depends on the server implementation.</param>
        /// <remarks>AMQP: Basic.Consume</remarks>
        ValueTask<string> ActivateConsumerAsync(IBasicConsumer consumer, string queue, bool autoAck, string consumerTag = "", bool noLocal = false, bool exclusive = false, IDictionary<string, object>? arguments = null);

        /// <summary>
        /// Cancels an activated consumer.
        /// </summary>
        /// <param name="consumerTag">The consumer tag returned by <see cref="ActivateConsumerAsync"/>.</param>
        /// <param name="waitForConfirmation">Whether or not to wait for server confirmation.</param>
        /// <remarks>AMQP: Basic.Cancel</remarks>
        ValueTask CancelConsumerAsync(string consumerTag, bool waitForConfirmation = true);

        /// <summary>
        /// Retrieves a single message from the queue.
        /// </summary>
        /// <param name="queue">The name of the queue to consume from.</param>
        /// <param name="autoAck">Whether or not the message is automatically acknowledged.</param>
        /// <remarks>AMQP: Basic.Get</remarks>
        ValueTask<SingleMessageRetrieval> RetrieveSingleMessageAsync(string queue, bool autoAck);

        /// <summary>
        /// Activates publish tags.
        /// </summary>
        /// <remarks>AMQP: Confirm.Select</remarks>
        ValueTask ActivatePublishTagsAsync();

        /// <summary>
        /// Occurs when a publish tag was acknowledged.
        /// ulong - deliveryTag - the publish tag.
        /// bool - multiple - whether or not the publish tag is up to and inclusive or just a single tag.
        /// bool - isAck - whether or not it was a positive or negative acknowledged.
        /// </summary>
        /// <remarks>AMQP: Basic.Ack / Basic.Nack</remarks>
        event Action<ulong, bool, bool>? PublishTagAcknowledged;

        /// <summary>
        /// Occurs when a publish tag was used for publishing a message.
        /// ulong - deliveryTag - the publish tag.
        /// </summary>
        event Action<ulong>? NewPublishTagUsed;

        /// <summary>
        /// Occurs when a message delivery failed.
        /// ulong - deliveryTag - the publish tag.
        /// </summary>
        event MessageDeliveryFailedDelegate? MessageDeliveryFailed;

        /// <summary>
        /// Publishes a message to the exchange with the routing key.
        /// </summary>
        /// <param name="exchange">The exchange to publish it to.</param>
        /// <param name="routingKey">The routing key to use. Must be shorter than 255 bytes.</param>
        /// <param name="basicProperties">The properties sent along with the body.</param>
        /// <param name="body">The body to send.</param>
        /// <param name="mandatory">Whether or not to raise a <see cref="MessageDeliveryFailed"/> if the message could not be routed to a queue.</param>
        /// <remarks>AMQP: Basic.Publish</remarks>
        ValueTask PublishMessageAsync(string exchange, string routingKey, IBasicProperties? basicProperties, ReadOnlyMemory<byte> body, bool mandatory = false);

        /// <summary>
        /// Publishes a batch of messages.
        /// </summary>
        /// <param name="batch">The batch of messages to send.</param>
        /// <remarks>AMQP: Basic.Publish</remarks>
        ValueTask PublishBatchAsync(MessageBatch batch);

        /// <summary>
        /// Declares an exchange.
        /// </summary>
        /// <param name="exchange">The name of the exchange.</param>
        /// <param name="type">The type of exchange (<see cref="ExchangeType"/>).</param>
        /// <param name="durable">Durable exchanges remain active when a server restarts</param>
        /// <param name="autoDelete">Whether or not to delete the exchange when all queues have finished using it.</param>
        /// <param name="throwOnMismatch">Whether or not to throw an exception on mismatch.</param>
        /// <param name="arguments">A set of arguments for the declare. The syntax and semantics of these arguments depends on the server implementation.</param>
        /// <param name="waitForConfirmation">Whether or not to wait for server confirmation.</param>
        /// <remarks>AMQP: Exchange.Declare</remarks>
        ValueTask DeclareExchangeAsync(string exchange, string type, bool durable, bool autoDelete, bool throwOnMismatch = true, IDictionary<string, object>? arguments = null, bool waitForConfirmation = true);

        /// <summary>
        /// Deletes an exchange.
        /// </summary>
        /// <param name="exchange">The name of the exchange.</param>
        /// <param name="ifUnused">Whether or not the server will only delete the exchange if it has no queue bindings.</param>
        /// <param name="waitForConfirmation">Whether or not to wait for server confirmation.</param>
        /// <remarks>AMQP: Exchange.Delete</remarks>
        ValueTask DeleteExchangeAsync(string exchange, bool ifUnused = false, bool waitForConfirmation = true);

        /// <summary>
        /// Binds an exchange.
        /// </summary>
        /// <param name="destination">The name of the destination exchange to bind.</param>
        /// <param name="source">The name of the source exchange to bind.</param>
        /// <param name="routingKey">The routing key to use. Must be shorter than 255 bytes.</param>
        /// <param name="arguments">A set of arguments for the bind. The syntax and semantics of these arguments depends on the server implementation.</param>
        /// <param name="waitForConfirmation">Whether or not to wait for server confirmation.</param>
        /// <remarks>AMQP: Exchange.Bind</remarks>
        ValueTask BindExchangeAsync(string destination, string source, string routingKey, IDictionary<string, object>? arguments = null, bool waitForConfirmation = true);

        /// <summary>
        /// Unbinds an exchange.
        /// </summary>
        /// <param name="destination">The name of the destination exchange to unbind.</param>
        /// <param name="source">The name of the source exchange to unbind.</param>
        /// <param name="routingKey">The routing key to use. Must be shorter than 255 bytes.</param>
        /// <param name="arguments">A set of arguments for the unbind. The syntax and semantics of these arguments depends on the server implementation.</param>
        /// <param name="waitForConfirmation">Whether or not to wait for server confirmation.</param>
        /// <remarks>AMQP: Exchange.Unbind</remarks>
        ValueTask UnbindExchangeAsync(string destination, string source, string routingKey, IDictionary<string, object>? arguments = null, bool waitForConfirmation = true);

        /// <summary>
        /// Declares an queue.
        /// </summary>
        /// <param name="queue">The name of the queue or empty if the server shall generate a name.</param>
        /// <param name="durable">Durable queues remain active when a server restarts.</param>
        /// <param name="exclusive">Exclusive queues may only be accessed by the current connection, and are deleted when that connection closes.</param>
        /// <param name="autoDelete">Whether or not the queue is deleted when all consumers have finished using it.</param>
        /// <param name="throwOnMismatch">Whether or not to throw an exception on mismatch.</param>
        /// <param name="arguments">A set of arguments for the declare. The syntax and semantics of these arguments depends on the server implementation.</param>
        /// <returns>The name of the queue, the current message count and the current consumer count.</returns>
        /// <remarks>AMQP: Queue.Declare</remarks>
        ValueTask<(string QueueName, uint MessageCount, uint ConsumerCount)> DeclareQueueAsync(string queue, bool durable, bool exclusive, bool autoDelete, bool throwOnMismatch = true, IDictionary<string, object>? arguments = null);

        /// <summary>
        /// Declares a queue without a confirmation.
        /// </summary>
        /// <param name="queue">The name of the queue or empty if the server shall generate a name.</param>
        /// <param name="durable">Durable queues remain active when a server restarts.</param>
        /// <param name="exclusive">Exclusive queues may only be accessed by the current connection, and are deleted when that connection closes.</param>
        /// <param name="autoDelete">Whether or not the queue is deleted when all consumers have finished using it.</param>
        /// <param name="arguments">A set of arguments for the declare. The syntax and semantics of these arguments depends on the server implementation.</param>
        /// <remarks>AMQP: Queue.Declare</remarks>
        ValueTask DeclareQueueWithoutConfirmationAsync(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object>? arguments = null);

        /// <summary>
        /// Deletes a queue.
        /// </summary>
        /// <param name="queue">The name of the queue.</param>
        /// <param name="ifUnused">Whether or not the server will only delete the queue if it has no consumers.</param>
        /// <param name="ifEmpty">Whether or not the server will only delete the queue if it has no messages.</param>
        /// <returns>The number of messages deleted.</returns>
        /// <remarks>AMQP: Queue.Delete</remarks>
        ValueTask<uint> DeleteQueueAsync(string queue, bool ifUnused = false, bool ifEmpty = false);

        /// <summary>
        /// Deletes a queue without confirmation.
        /// </summary>
        /// <param name="queue">The name of the queue.</param>
        /// <param name="ifUnused">Whether or not the server will only delete the queue if it has no consumers.</param>
        /// <param name="ifEmpty">Whether or not the server will only delete the queue if it has no messages.</param>
        /// <remarks>AMQP: Queue.Delete</remarks>
        ValueTask DeleteQueueWithoutConfirmationAsync(string queue, bool ifUnused = false, bool ifEmpty = false);

        /// <summary>
        /// Binds a queue to an exchange.
        /// </summary>
        /// <param name="queue">The name of the queue.</param>
        /// <param name="exchange">The name of the exchange to bind to.</param>
        /// <param name="routingKey">The routing key to use. Must be shorter than 255 bytes.</param>
        /// <param name="arguments">A set of arguments for the bind. The syntax and semantics of these arguments depends on the server implementation.</param>
        /// <param name="waitForConfirmation">Whether or not to wait for server confirmation.</param>
        /// <remarks>AMQP: Queue.Bind</remarks>
        ValueTask BindQueueAsync(string queue, string exchange, string routingKey, IDictionary<string, object>? arguments = null, bool waitForConfirmation = true);

        /// <summary>
        /// Unbinds a queue from an exchange.
        /// </summary>
        /// <param name="queue">The name of the queue.</param>
        /// <param name="exchange">The name of the exchange to unbind from.</param>
        /// <param name="routingKey">The routing key to use. Must be shorter than 255 bytes.</param>
        /// <param name="arguments">A set of arguments for the unbind. The syntax and semantics of these arguments depends on the server implementation.</param>
        /// <remarks>AMQP: Queue.Unbind</remarks>
        ValueTask UnbindQueueAsync(string queue, string exchange, string routingKey, IDictionary<string, object>? arguments = null);

        /// <summary>
        /// Purges the queue of all messages.
        /// </summary>
        /// <param name="queue">The name of the queue.</param>
        /// <returns>The number of messages purged.</returns>
        /// <remarks>AMQP: Queue.Purge</remarks>
        ValueTask<uint> PurgeQueueAsync(string queue);

        /// <summary>
        /// Activates the transaction mode.
        /// </summary>
        /// <remarks>AMQP: Tx.Select</remarks>
        ValueTask ActivateTransactionsAsync();

        /// <summary>
        /// Commits the current transaction.
        /// </summary>
        /// <remarks>AMQP: Tx.Commit</remarks>
        ValueTask CommitTransactionAsync();

        /// <summary>
        /// Rollbacks the current transaction.
        /// </summary>
        /// <remarks>AMQP: Tx.Rollback</remarks>
        ValueTask RollbackTransactionAsync();

        /// <summary>
        /// Resend all unacknowledged messages.
        /// </summary>
        /// <remarks>AMQP: Basic.Recover</remarks>
        ValueTask ResendUnackedMessages(bool requeue);

        /// <summary>
        /// Occurs when the channel is shut down.
        /// If the model is already destroyed at the time an event handler is added to this event, the event handler will be fired immediately.
        /// </summary>
        event Action<ShutdownEventArgs>? Shutdown;

        /// <summary>
        /// Aborts the channel.
        /// In comparison to normal <see cref="CloseAsync"/> method, <see cref="AbortAsync"/> will not throw
        /// <see cref="Exceptions.AlreadyClosedException"/> or <see cref="System.IO.IOException"/> or any other <see cref="Exception"/> during closing model.
        /// </summary>
        /// <param name="replyCode">The close code (See under "Reply Codes" in the AMQP specification).</param>
        /// <param name="replyText">A message indicating the reason for closing the model.</param>
        /// <remarks>AMQP: Connection.Close</remarks>
        ValueTask AbortAsync(ushort replyCode, string replyText);

        /// <summary>
        /// Closes the channel.
        /// </summary>
        /// <param name="replyCode">The close code (See under "Reply Codes" in the AMQP specification).</param>
        /// <param name="replyText">A message indicating the reason for closing the model.</param>
        /// <remarks>AMQP: Connection.Close</remarks>
        ValueTask CloseAsync(ushort replyCode, string replyText);

        /// <summary>
        /// Wait until all published messages have been confirmed.
        /// </summary>
        /// <returns>Whether or not all messages were acknowledged.</returns>
        bool WaitForConfirms();

        /// <summary>
        /// Wait until all published messages have been confirmed.
        /// </summary>
        /// <param name="timeout">The max time to wait for.</param>
        /// <returns>Whether or not all messages were acknowledged.</returns>
        bool WaitForConfirms(TimeSpan timeout);

        /// <summary>
        /// Waits until all published messages have been confirmed. If not the channel will be closed.
        /// </summary>
        void WaitForConfirmsOrDie();

        /// <summary>
        /// Waits until all published messages have been confirmed. If not the channel will be closed.
        /// </summary>
        /// <param name="timeout">The max time to wait for.</param>
        void WaitForConfirmsOrDie(TimeSpan timeout);
    }
}
