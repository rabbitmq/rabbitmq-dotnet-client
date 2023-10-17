// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2020 VMware, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       https://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v2.0:
//
//---------------------------------------------------------------------------
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Common AMQP model, spanning the union of the
    /// functionality offered by versions 0-8, 0-8qpid, 0-9 and 0-9-1 of AMQP.
    /// </summary>
    /// <remarks>
    /// Extends the <see cref="IDisposable"/> interface, so that the "using"
    /// statement can be used to scope the lifetime of a channel when appropriate.
    /// </remarks>
    public interface IChannel : IDisposable
    {
        /// <summary>
        /// Channel number, unique per connections.
        /// </summary>
        int ChannelNumber { get; }

        /// <summary>
        /// Returns null if the session is still in a state where it can be used,
        /// or the cause of its closure otherwise.
        /// </summary>
        ShutdownEventArgs CloseReason { get; }

        /// <summary>Signalled when an unexpected message is delivered
        ///
        /// Under certain circumstances it is possible for a channel to receive a
        /// message delivery which does not match any consumer which is currently
        /// set up via basicConsume(). This will occur after the following sequence
        /// of events:
        ///
        /// ctag = basicConsume(queue, consumer); // i.e. with explicit acks
        /// // some deliveries take place but are not acked
        /// basicCancel(ctag);
        /// basicRecover(false);
        ///
        /// Since requeue is specified to be false in the basicRecover, the spec
        /// states that the message must be redelivered to "the original recipient"
        /// - i.e. the same channel / consumer-tag. But the consumer is no longer
        /// active.
        ///
        /// In these circumstances, you can register a default consumer to handle
        /// such deliveries. If no default consumer is registered an
        /// InvalidOperationException will be thrown when such a delivery arrives.
        ///
        /// Most people will not need to use this.</summary>
        IBasicConsumer DefaultConsumer { get; set; }

        /// <summary>
        /// Returns true if the channel is no longer in a state where it can be used.
        /// </summary>
        bool IsClosed { get; }

        /// <summary>
        /// Returns true if the channel is still in a state where it can be used.
        /// Identical to checking if <see cref="CloseReason"/> equals null.</summary>
        bool IsOpen { get; }

        /// <summary>
        /// When in confirm mode, return the sequence number of the next message to be published.
        /// </summary>
        ulong NextPublishSeqNo { get; }

        /// <summary>
        /// The name of the last queue declared on this channel.
        /// </summary>
        /// <remarks>
        /// https://www.rabbitmq.com/amqp-0-9-1-reference.html#domain.queue-name
        /// </remarks>
        string CurrentQueue { get; }

        /// <summary>
        /// Signalled when a Basic.Ack command arrives from the broker.
        /// </summary>
        event EventHandler<BasicAckEventArgs> BasicAcks;

        /// <summary>
        /// Signalled when a Basic.Nack command arrives from the broker.
        /// </summary>
        event EventHandler<BasicNackEventArgs> BasicNacks;

        /// <summary>
        /// All messages received before this fires that haven't been ack'ed will be redelivered.
        /// All messages received afterwards won't be.
        /// </summary>
        /// <remarks>
        /// Handlers for this event are invoked by the connection thread.
        /// It is sometimes useful to allow that thread to know that a recover-ok
        /// has been received, rather than the thread that invoked <see cref="BasicRecover"/>.
        /// </remarks>
        event EventHandler<EventArgs> BasicRecoverOk;

        /// <summary>
        /// Signalled when a Basic.Return command arrives from the broker.
        /// </summary>
        event EventHandler<BasicReturnEventArgs> BasicReturn;

        /// <summary>
        /// Signalled when an exception occurs in a callback invoked by the channel.
        ///
        /// Examples of cases where this event will be signalled
        /// include exceptions thrown in <see cref="IBasicConsumer"/> methods, or
        /// exceptions thrown in <see cref="ChannelShutdown"/> delegates etc.
        /// </summary>
        event EventHandler<CallbackExceptionEventArgs> CallbackException;

        event EventHandler<FlowControlEventArgs> FlowControl;

        /// <summary>
        /// Notifies the destruction of the channel.
        /// </summary>
        /// <remarks>
        /// If the channel is already destroyed at the time an event
        /// handler is added to this event, the event handler will be fired immediately.
        /// </remarks>
        event EventHandler<ShutdownEventArgs> ChannelShutdown;

        /// <summary>Acknknowledges one or more messages.</summary>
        /// <param name="deliveryTag">The delivery tag.</param>
        /// <param name="multiple">Ack all messages up to the delivery tag if set to <c>true</c>.</param>
        void BasicAck(ulong deliveryTag, bool multiple);

        /// <summary>Asynchronously acknknowledges one or more messages.</summary>
        /// <param name="deliveryTag">The delivery tag.</param>
        /// <param name="multiple">Ack all messages up to the delivery tag if set to <c>true</c>.</param>
        ValueTask BasicAckAsync(ulong deliveryTag, bool multiple);

        /// <summary>Cancel a Basic content-class consumer.</summary>
        /// <param name="consumerTag">The consumer tag.</param>
        void BasicCancel(string consumerTag);

        /// <summary>Asynchronously cancel a Basic content-class consumer.</summary>
        /// <param name="consumerTag">The consumer tag.</param>
        ValueTask BasicCancelAsync(string consumerTag);

        /// <summary>
        /// Same as BasicCancel but sets nowait to true and returns void (as there
        /// will be no response from the server).
        /// </summary>
        /// <param name="consumerTag">The consumer tag.</param>
        void BasicCancelNoWait(string consumerTag);

        /// <summary>Start a Basic content-class consumer.</summary>
        /// <param name="queue">The queue.</param>
        /// <param name="autoAck">If set to <c>true</c>, automatically ack messages.</param>
        /// <param name="consumerTag">The consumer tag.</param>
        /// <param name="noLocal">If set to <c>true</c>, this consumer will not receive messages published by the same connection.</param>
        /// <param name="exclusive">If set to <c>true</c>, the consumer is exclusive.</param>
        /// <param name="arguments">Consumer arguments.</param>
        /// <param name="consumer">The consumer, an instance of <see cref="IBasicConsumer"/></param>
        /// <returns></returns>
        string BasicConsume(string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive, IDictionary<string, object> arguments, IBasicConsumer consumer);

        /// <summary>Asynchronously start a Basic content-class consumer.</summary>
        /// <param name="queue">The queue.</param>
        /// <param name="autoAck">If set to <c>true</c>, automatically ack messages.</param>
        /// <param name="consumerTag">The consumer tag.</param>
        /// <param name="noLocal">If set to <c>true</c>, this consumer will not receive messages published by the same connection.</param>
        /// <param name="exclusive">If set to <c>true</c>, the consumer is exclusive.</param>
        /// <param name="arguments">Consumer arguments.</param>
        /// <param name="consumer">The consumer, an instance of <see cref="IBasicConsumer"/></param>
        /// <returns></returns>
        ValueTask<string> BasicConsumeAsync(string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive, IDictionary<string, object> arguments, IBasicConsumer consumer);

        /// <summary>
        /// Retrieve an individual message, if
        /// one is available; returns null if the server answers that
        /// no messages are currently available. See also <see cref="IChannel.BasicAck" />.
        /// </summary>
        /// <param name="queue">The queue.</param>
        /// <param name="autoAck">If set to <c>true</c>, automatically ack the message.</param>
        /// <returns><see cref="BasicGetResult"/></returns>
        BasicGetResult BasicGet(string queue, bool autoAck);

        /// <summary>Reject one or more delivered message(s).</summary>
        void BasicNack(ulong deliveryTag, bool multiple, bool requeue);

#nullable enable

        /// <summary>
        /// Publishes a message.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Routing key must be shorter than 255 bytes.
        ///   </para>
        /// </remarks>
        void BasicPublish<TProperties>(string exchange, string routingKey, in TProperties basicProperties, ReadOnlyMemory<byte> body = default, bool mandatory = false)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader;

        /// <summary>
        /// Publishes a message.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Routing key must be shorter than 255 bytes.
        ///   </para>
        /// </remarks>
        void BasicPublish<TProperties>(CachedString exchange, CachedString routingKey, in TProperties basicProperties, ReadOnlyMemory<byte> body = default, bool mandatory = false)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader;

        /// <summary>
        /// Asynchronously publishes a message.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Routing key must be shorter than 255 bytes.
        ///   </para>
        /// </remarks>
        ValueTask BasicPublishAsync<TProperties>(string exchange, string routingKey, in TProperties basicProperties, ReadOnlyMemory<byte> body = default, bool mandatory = false)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader;

        /// <summary>
        /// Asynchronously publishes a message.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Routing key must be shorter than 255 bytes.
        ///   </para>
        /// </remarks>
        ValueTask BasicPublishAsync<TProperties>(CachedString exchange, CachedString routingKey, in TProperties basicProperties, ReadOnlyMemory<byte> body = default, bool mandatory = false)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader;

#nullable disable

        /// <summary>
        /// Configures QoS parameters of the Basic content-class.
        /// </summary>
        /// <param name="prefetchSize">Size of the prefetch in bytes.</param>
        /// <param name="prefetchCount">The prefetch count.</param>
        /// <param name="global">If set to <c>true</c>, use global prefetch.
        /// See the <seealso href="https://www.rabbitmq.com/consumer-prefetch.html#overview">Consumer Prefetch documentation</seealso>.</param>
        void BasicQos(uint prefetchSize, ushort prefetchCount, bool global);

        /// <summary>
        /// Configures QoS parameters of the Basic content-class.
        /// </summary>
        /// <param name="prefetchSize">Size of the prefetch in bytes.</param>
        /// <param name="prefetchCount">The prefetch count.</param>
        /// <param name="global">If set to <c>true</c>, use global prefetch.
        /// See the <seealso href="https://www.rabbitmq.com/consumer-prefetch.html#overview">Consumer Prefetch documentation</seealso>.</param>
        ValueTask BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global);

        /// <summary>
        /// Indicates that a consumer has recovered.
        /// Deprecated. Should not be used.
        /// </summary>
        [Obsolete]
        void BasicRecover(bool requeue);

        /// <summary>
        /// Indicates that a consumer has recovered.
        /// Deprecated. Should not be used.
        /// </summary>
        [Obsolete]
        void BasicRecoverAsync(bool requeue);

        /// <summary> Reject a delivered message.</summary>
        void BasicReject(ulong deliveryTag, bool requeue);

        /// <summary> Reject a delivered message.</summary>
        ValueTask BasicRejectAsync(ulong deliveryTag, bool requeue);

        /// <summary>Close this session.</summary>
        /// <param name="replyCode">The reply code to send for closing (See under "Reply Codes" in the AMQP specification).</param>
        /// <param name="replyText">The reply text to send for closing.</param>
        /// <param name="abort">Whether or not the close is an abort (ignoring certain exceptions).</param>
        void Close(ushort replyCode, string replyText, bool abort);

        /// <summary>
        /// Enable publisher acknowledgements.
        /// </summary>
        void ConfirmSelect();

        /// <summary>
        /// Bind an exchange to an exchange.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Routing key must be shorter than 255 bytes.
        ///   </para>
        /// </remarks>
        void ExchangeBind(string destination, string source, string routingKey, IDictionary<string, object> arguments);

        /// <summary>
        /// Asynchronously binds an exchange to an exchange.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Routing key must be shorter than 255 bytes.
        ///   </para>
        /// </remarks>
        ValueTask ExchangeBindAsync(string destination, string source, string routingKey, IDictionary<string, object> arguments);

        /// <summary>
        /// Like ExchangeBind but sets nowait to true.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Routing key must be shorter than 255 bytes.
        ///   </para>
        /// </remarks>
        void ExchangeBindNoWait(string destination, string source, string routingKey, IDictionary<string, object> arguments);

        /// <summary>Declare an exchange.</summary>
        /// <remarks>
        /// The exchange is declared non-passive and non-internal.
        /// The "nowait" option is not used.
        /// </remarks>
        void ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments);

        /// <summary>Asynchronously declare an exchange.</summary>
        /// <remarks>
        /// The exchange is declared non-internal.
        /// The "nowait" option is not used.
        /// </remarks>
        ValueTask ExchangeDeclareAsync(string exchange, string type, bool passive, bool durable, bool autoDelete, IDictionary<string, object> arguments);

        /// <summary>
        /// Same as ExchangeDeclare but sets nowait to true and returns void (as there
        /// will be no response from the server).
        /// </summary>
        void ExchangeDeclareNoWait(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments);

        /// <summary>
        /// Do a passive exchange declaration.
        /// </summary>
        /// <remarks>
        /// This method performs a "passive declare" on an exchange,
        /// which checks whether an exchange exists.
        /// It will do nothing if the exchange already exists and result
        /// in a channel-level protocol exception (channel closure) if not.
        /// </remarks>
        void ExchangeDeclarePassive(string exchange);

        /// <summary>
        /// Delete an exchange.
        /// </summary>
        void ExchangeDelete(string exchange, bool ifUnused);

        /*
         * TODO LRB rabbitmq/rabbitmq-dotnet-client#1347
        /// <summary>
        /// Asynchronously delete an exchange.
        /// </summary>
        ValueTask ExchangeDeleteAsync(string exchange, bool ifUnused);
        */

        /// <summary>
        /// Asynchronously delete an exchange.
        /// </summary>
        ValueTask ExchangeDeleteAsync(string exchange, bool ifUnused);

        /// <summary>
        /// Like ExchangeDelete but sets nowait to true.
        /// </summary>
        void ExchangeDeleteNoWait(string exchange, bool ifUnused);

        /// <summary>
        /// Unbind an exchange from an exchange.
        /// </summary>
        /// <remarks>
        /// Routing key must be shorter than 255 bytes.
        /// </remarks>
        void ExchangeUnbind(string destination, string source, string routingKey, IDictionary<string, object> arguments);

        /// <summary>
        /// Asynchronously unbind an exchange from an exchange.
        /// </summary>
        /// <remarks>
        /// Routing key must be shorter than 255 bytes.
        /// </remarks>
        ValueTask ExchangeUnbindAsync(string destination, string source, string routingKey, IDictionary<string, object> arguments);

        /// <summary>
        /// Like ExchangeUnbind but sets nowait to true.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Routing key must be shorter than 255 bytes.
        ///   </para>
        /// </remarks>
        void ExchangeUnbindNoWait(string destination, string source, string routingKey, IDictionary<string, object> arguments);

        /// <summary>
        /// Bind a queue to an exchange.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Routing key must be shorter than 255 bytes.
        ///   </para>
        /// </remarks>
        void QueueBind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments);

        /// <summary>
        /// Asynchronously bind a queue to an exchange.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Routing key must be shorter than 255 bytes.
        ///   </para>
        /// </remarks>
        ValueTask QueueBindAsync(string queue, string exchange, string routingKey, IDictionary<string, object> arguments);

        /// <summary>Same as QueueBind but sets nowait parameter to true.</summary>
        /// <remarks>
        ///   <para>
        ///     Routing key must be shorter than 255 bytes.
        ///   </para>
        /// </remarks>
        void QueueBindNoWait(string queue, string exchange, string routingKey, IDictionary<string, object> arguments);

        /// <summary>
        /// Declares a queue. See the <a href="https://www.rabbitmq.com/queues.html">Queues guide</a> to learn more.
        /// </summary>
        /// <param name="queue">The name of the queue. Pass an empty string to make the server generate a name.</param>
        /// <param name="durable">Should this queue will survive a broker restart?</param>
        /// <param name="exclusive">Should this queue use be limited to its declaring connection? Such a queue will be deleted when its declaring connection closes.</param>
        /// <param name="autoDelete">Should this queue be auto-deleted when its last consumer (if any) unsubscribes?</param>
        /// <param name="arguments">Optional; additional queue arguments, e.g. "x-queue-type"</param>
        QueueDeclareOk QueueDeclare(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments);

        /// <summary>
        /// Asynchronously declares a queue. See the <a href="https://www.rabbitmq.com/queues.html">Queues guide</a> to learn more.
        /// </summary>
        /// <param name="queue">The name of the queue. Pass an empty string to make the server generate a name.</param>
        /// <param name="passive">Set to <code>true</code> to passively declare the queue (i.e. check for its existence)</param>
        /// <param name="durable">Should this queue will survive a broker restart?</param>
        /// <param name="exclusive">Should this queue use be limited to its declaring connection? Such a queue will be deleted when its declaring connection closes.</param>
        /// <param name="autoDelete">Should this queue be auto-deleted when its last consumer (if any) unsubscribes?</param>
        /// <param name="arguments">Optional; additional queue arguments, e.g. "x-queue-type"</param>
        ValueTask<QueueDeclareOk> QueueDeclareAsync(string queue, bool passive, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments);

        /// <summary>
        /// Declares a queue. See the <a href="https://www.rabbitmq.com/queues.html">Queues guide</a> to learn more.
        /// </summary>
        /// <param name="queue">The name of the queue. Pass an empty string to make the server generate a name.</param>
        /// <param name="durable">Should this queue will survive a broker restart?</param>
        /// <param name="exclusive">Should this queue use be limited to its declaring connection? Such a queue will be deleted when its declaring connection closes.</param>
        /// <param name="autoDelete">Should this queue be auto-deleted when its last consumer (if any) unsubscribes?</param>
        /// <param name="arguments">Optional; additional queue arguments, e.g. "x-queue-type"</param>
        void QueueDeclareNoWait(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments);

        /// <summary>Declare a queue passively.</summary>
        /// <remarks>
        ///The queue is declared passive, non-durable,
        ///non-exclusive, and non-autodelete, with no arguments.
        ///The queue is declared passively; i.e. only check if it exists.
        /// </remarks>
        QueueDeclareOk QueueDeclarePassive(string queue);

        /// <summary>
        /// Returns the number of messages in a queue ready to be delivered
        /// to consumers. This method assumes the queue exists. If it doesn't,
        /// an exception will be closed with an exception.
        /// </summary>
        /// <param name="queue">The name of the queue</param>
        uint MessageCount(string queue);

        /// <summary>
        /// Returns the number of consumers on a queue.
        /// This method assumes the queue exists. If it doesn't,
        /// an exception will be closed with an exception.
        /// </summary>
        /// <param name="queue">The name of the queue</param>
        uint ConsumerCount(string queue);

        /// <summary>
        /// Deletes a queue. See the <a href="https://www.rabbitmq.com/queues.html">Queues guide</a> to learn more.
        /// </summary>
        /// <param name="queue">The name of the queue.</param>
        /// <param name="ifUnused">Only delete the queue if it is unused.</param>
        /// <param name="ifEmpty">Only delete the queue if it is empty.</param>
        /// <returns>Returns the number of messages purged during deletion.</returns>
        uint QueueDelete(string queue, bool ifUnused, bool ifEmpty);

        /// <summary>
        /// Asynchronously deletes a queue. See the <a href="https://www.rabbitmq.com/queues.html">Queues guide</a> to learn more.
        /// </summary>
        /// <remarks>
        ///Returns the number of messages purged during queue deletion.
        /// </remarks>
        ValueTask<uint> QueueDeleteAsync(string queue, bool ifUnused, bool ifEmpty);

        /// <summary>
        ///Same as QueueDelete but sets nowait parameter to true
        ///and returns void (as there will be no response from the server)
        /// </summary>
        /// <param name="queue">The name of the queue.</param>
        /// <param name="ifUnused">Only delete the queue if it is unused.</param>
        /// <param name="ifEmpty">Only delete the queue if it is empty.</param>
        /// <returns>Returns the number of messages purged during deletion.</returns>
        void QueueDeleteNoWait(string queue, bool ifUnused, bool ifEmpty);

        /// <summary>
        /// Purge a queue of messages.
        /// </summary>
        /// <remarks>
        /// Returns the number of messages purged.
        /// </remarks>
        uint QueuePurge(string queue);

        /// <summary>
        /// Unbind a queue from an exchange.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Routing key must be shorter than 255 bytes.
        ///   </para>
        /// </remarks>
        void QueueUnbind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments);

        /// <summary>
        /// Commit this session's active TX transaction.
        /// </summary>
        void TxCommit();

        /// <summary>
        /// Roll back this session's active TX transaction.
        /// </summary>
        void TxRollback();

        /// <summary>
        /// Enable TX mode for this session.
        /// </summary>
        void TxSelect();

        /// <summary>
        /// Wait until all published messages on this channel have been confirmed.
        /// </summary>
        /// <returns>True if no nacks were received within the timeout, otherwise false.</returns>
        /// <param name="token">The cancellation token.</param>
        /// <remarks>
        /// Waits until all messages published on this channel since the last call have
        /// been either ack'd or nack'd by the server. Returns whether
        /// all the messages were ack'd (and none were nack'd).
        /// Throws an exception when called on a channel
        /// that does not have publisher confirms enabled.
        /// </remarks>
        Task<bool> WaitForConfirmsAsync(CancellationToken token = default);

        /// <summary>
        /// Wait until all published messages on this channel have been confirmed.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <remarks>
        /// Waits until all messages published on this channel since the last call have
        /// been ack'd by the server. If a nack is received or the timeout
        /// elapses, throws an IOException exception immediately and closes
        /// the channel.
        /// </remarks>
        Task WaitForConfirmsOrDieAsync(CancellationToken token = default);

        /// <summary>
        /// Amount of time protocol  operations (e.g. <code>queue.declare</code>) are allowed to take before
        /// timing out.
        /// </summary>
        TimeSpan ContinuationTimeout { get; set; }
    }
}
