// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
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
        ShutdownEventArgs? CloseReason { get; }

        /// <summary>Signalled when an unexpected message is delivered.</summary>
        ///
        /// <remarks>
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
        /// Most people will not need to use this.
        /// </remarks>
        IBasicConsumer? DefaultConsumer { get; set; }

        /// <summary>
        /// Returns true if the channel is no longer in a state where it can be used.
        /// </summary>
        bool IsClosed { get; }

        /// <summary>
        /// Returns true if the channel is still in a state where it can be used.
        /// Identical to checking if <see cref="CloseReason"/> equals null.
        /// </summary>
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
        string? CurrentQueue { get; }

        /// <summary>
        /// Signalled when a Basic.Ack command arrives from the broker.
        /// </summary>
        event EventHandler<BasicAckEventArgs> BasicAcks;

        /// <summary>
        /// Signalled when a Basic.Nack command arrives from the broker.
        /// </summary>
        event EventHandler<BasicNackEventArgs> BasicNacks;

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

        /// <summary>Asynchronously acknknowledges one or more messages.</summary>
        /// <param name="deliveryTag">The delivery tag.</param>
        /// <param name="multiple">Ack all messages up to the delivery tag if set to <c>true</c>.</param>
        /// <param name="cancellationToken">Cancellation token for this operation.</param>
        ValueTask BasicAckAsync(ulong deliveryTag, bool multiple,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously nack one or more delivered message(s).
        /// </summary>
        /// <param name="deliveryTag">The delivery tag.</param>
        /// <param name="multiple">If set to <c>true</c>, nack all messages up to the current tag.</param>
        /// <param name="requeue">If set to <c>true</c>, requeue nack'd messages.</param>
        /// <param name="cancellationToken">Cancellation token for this operation.</param>
        ValueTask BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue,
            CancellationToken cancellationToken = default);

        /// <summary>Asynchronously cancel a Basic content-class consumer.</summary>
        /// <param name="consumerTag">The consumer tag.</param>
        /// <param name="noWait">If set to <c>true</c>, do not require a response from the server.</param>
        /// <param name="cancellationToken">Cancellation token for this operation.</param>
        Task BasicCancelAsync(string consumerTag, bool noWait = false,
            CancellationToken cancellationToken = default);

        /// <summary>Asynchronously start a Basic content-class consumer.</summary>
        /// <param name="queue">The queue.</param>
        /// <param name="autoAck">If set to <c>true</c>, automatically ack messages.</param>
        /// <param name="consumerTag">The consumer tag.</param>
        /// <param name="noLocal">If set to <c>true</c>, this consumer will not receive messages published by the same connection.</param>
        /// <param name="exclusive">If set to <c>true</c>, the consumer is exclusive.</param>
        /// <param name="arguments">Consumer arguments.</param>
        /// <param name="consumer">The consumer, an instance of <see cref="IBasicConsumer"/></param>
        /// <param name="cancellationToken">Cancellation token for this operation.</param>
        /// <returns></returns>
        Task<string> BasicConsumeAsync(string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive,
            IDictionary<string, object?>? arguments, IBasicConsumer consumer,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieve an individual message, if
        /// one is available; returns null if the server answers that
        /// no messages are currently available. See also <see cref="IChannel.BasicAckAsync" />.
        /// </summary>
        /// <param name="queue">The queue.</param>
        /// <param name="autoAck">If set to <c>true</c>, automatically ack the message.</param>
        /// <param name="cancellationToken">Cancellation token for this operation.</param>
        /// <returns><see cref="BasicGetResult"/></returns>
        ValueTask<BasicGetResult?> BasicGetAsync(string queue, bool autoAck,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously publishes a message.
        /// </summary>
        /// <param name="exchange">The exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="basicProperties">The message properties.</param>
        /// <param name="body">The message body.</param>
        /// <param name="mandatory">If set to <c>true</c>, the message must route to a queue.</param>
        /// <param name="cancellationToken">CancellationToken for this operation.</param>
        /// <remarks>
        /// Routing key must be shorter than 255 bytes.
        /// </remarks>
        ValueTask BasicPublishAsync<TProperties>(string exchange, string routingKey, TProperties basicProperties,
            ReadOnlyMemory<byte> body = default, bool mandatory = false,
            CancellationToken cancellationToken = default)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader;

        /// <summary>
        /// Asynchronously publishes a message.
        /// </summary>
        /// <param name="exchange">The exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="basicProperties">The message properties.</param>
        /// <param name="body">The message body.</param>
        /// <param name="mandatory">If set to <c>true</c>, the message must route to a queue.</param>
        /// <param name="cancellationToken">CancellationToken for this operation.</param>
        /// <remarks>
        /// Routing key must be shorter than 255 bytes.
        /// </remarks>
        ValueTask BasicPublishAsync<TProperties>(CachedString exchange, CachedString routingKey, TProperties basicProperties,
            ReadOnlyMemory<byte> body = default, bool mandatory = false,
            CancellationToken cancellationToken = default)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader;

        /// <summary>
        /// Configures QoS parameters of the Basic content-class.
        /// </summary>
        /// <param name="prefetchSize">Size of the prefetch in bytes.</param>
        /// <param name="prefetchCount">The prefetch count.</param>
        /// <param name="global">If set to <c>true</c>, use global prefetch.</param>
        /// <param name="cancellationToken">Cancellation token for this operation.</param>
        /// <remarks>See the <seealso href="https://www.rabbitmq.com/consumer-prefetch.html#overview">Consumer Prefetch documentation</seealso>.</remarks>
        Task BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global,
            CancellationToken cancellationToken = default);

        /// <summary> Reject a delivered message.</summary>
        /// <param name="deliveryTag">The delivery tag.</param>
        /// <param name="requeue">If set to <c>true</c>, requeue rejected messages.</param>
        /// <param name="cancellationToken">CancellationToken for this operation.</param>
        Task BasicRejectAsync(ulong deliveryTag, bool requeue,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously close this session.
        /// </summary>
        /// <param name="replyCode">The reply code to send for closing (See under "Reply Codes" in the AMQP specification).</param>
        /// <param name="replyText">The reply text to send for closing.</param>
        /// <param name="abort">Whether or not the close is an abort (ignoring certain exceptions).</param>
        /// <param name="cancellationToken">CancellationToken for this operation.</param>
        Task CloseAsync(ushort replyCode, string replyText, bool abort,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously close this session.
        /// </summary>
        /// <param name="reason">The <see cref="ShutdownEventArgs"/> instance containing the close data.</param>
        /// <param name="abort">Whether or not the close is an abort (ignoring certain exceptions).</param>
        /// <param name="cancellationToken">CancellationToken for this operation.</param>
        /// <returns></returns>
        Task CloseAsync(ShutdownEventArgs reason, bool abort,
            CancellationToken cancellationToken = default);

        /// <summary>Asynchronously enable publisher confirmations.</summary>
        /// <param name="cancellationToken">CancellationToken for this operation.</param>
        Task ConfirmSelectAsync(CancellationToken cancellationToken = default);

        /// <summary>Asynchronously declare an exchange.</summary>
        /// <param name="exchange">The name of the exchange.</param>
        /// <param name="type">The type of the exchange.</param>
        /// <param name="durable">Should this exchange survive a broker restart?</param>
        /// <param name="autoDelete">Should this exchange be auto-deleted?</param>
        /// <param name="arguments">The arguments.</param>
        /// <param name="passive">Optional; Set to <code>true</code> to passively declare the exchange (i.e. check for its existence)</param>
        /// <param name="noWait">If set to <c>true</c>, do not require a response from the server.</param>
        /// <param name="cancellationToken">CancellationToken for this operation.</param>
        /// <remarks>
        /// The exchange is declared non-internal.
        /// </remarks>
        Task ExchangeDeclareAsync(string exchange, string type, bool durable, bool autoDelete,
            IDictionary<string, object?>? arguments = null, bool passive = false, bool noWait = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously do a passive exchange declaration.
        /// </summary>
        /// <param name="exchange">The name of the exchange.</param>
        /// <param name="cancellationToken">CancellationToken for this operation.</param>
        /// <remarks>
        /// This method performs a "passive declare" on an exchange,
        /// which checks whether an exchange exists.
        /// It will do nothing if the exchange already exists and result
        /// in a channel-level protocol exception (channel closure) if not.
        /// </remarks>
        Task ExchangeDeclarePassiveAsync(string exchange, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously delete an exchange.
        /// </summary>
        /// <param name="exchange">The name of the exchange.</param>
        /// <param name="ifUnused">Only delete the exchange if it is unused.</param>
        /// <param name="noWait">If set to <c>true</c>, do not require a response from the server.</param>
        /// <param name="cancellationToken">CancellationToken for this operation.</param>
        Task ExchangeDeleteAsync(string exchange, bool ifUnused = false, bool noWait = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously binds an exchange to an exchange.
        /// </summary>
        /// <param name="destination">The name of the destination exchange.</param>
        /// <param name="source">The name of the source exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="arguments">The binding arguments.</param>
        /// <param name="noWait">If set to <c>true</c>, do not require a response from the server.</param>
        /// <param name="cancellationToken">CancellationToken for this operation.</param>
        /// <remarks>
        /// Routing key must be shorter than 255 bytes.
        /// </remarks>
        Task ExchangeBindAsync(string destination, string source, string routingKey,
            IDictionary<string, object?>? arguments = null, bool noWait = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously unbind an exchange from an exchange.
        /// </summary>
        /// <param name="destination">The name of the destination exchange.</param>
        /// <param name="source">The name of the source exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="arguments">The binding arguments.</param>
        /// <param name="noWait">If set to <c>true</c>, do not require a response from the server.</param>
        /// <param name="cancellationToken">CancellationToken for this operation.</param>
        /// <remarks>
        /// Routing key must be shorter than 255 bytes.
        /// </remarks>
        Task ExchangeUnbindAsync(string destination, string source, string routingKey,
            IDictionary<string, object?>? arguments = null, bool noWait = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously declares a queue. See the <a href="https://www.rabbitmq.com/queues.html">Queues guide</a> to learn more.
        /// </summary>
        /// <param name="queue">The name of the queue. Pass an empty string to make the server generate a name.</param>
        /// <param name="durable">Should this queue survive a broker restart?</param>
        /// <param name="exclusive">Should this queue use be limited to its declaring connection? Such a queue will be deleted when its declaring connection closes.</param>
        /// <param name="autoDelete">Should this queue be auto-deleted when its last consumer (if any) unsubscribes?</param>
        /// <param name="arguments">Optional; additional queue arguments, e.g. "x-queue-type"</param>
        /// <param name="passive">Optional; Set to <code>true</code> to passively declare the queue (i.e. check for its existence)</param>
        /// <param name="noWait">Optional; Set to <c>true</c> to not require a response from the server.</param>
        /// <param name="cancellationToken">CancellationToken for this operation.</param>
        Task<QueueDeclareOk> QueueDeclareAsync(string queue, bool durable, bool exclusive, bool autoDelete,
            IDictionary<string, object?>? arguments = null, bool passive = false, bool noWait = false,
            CancellationToken cancellationToken = default);

        /// <summary>Asynchronously declare a queue passively.</summary>
        /// <param name="queue">The name of the queue. Pass an empty string to make the server generate a name.</param>
        /// <param name="cancellationToken">CancellationToken for this operation.</param>
        /// <remarks>
        ///The queue is declared passive, non-durable,
        ///non-exclusive, and non-autodelete, with no arguments.
        ///The queue is declared passively; i.e. only check if it exists.
        /// </remarks>
        Task<QueueDeclareOk> QueueDeclarePassiveAsync(string queue,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously deletes a queue. See the <a href="https://www.rabbitmq.com/queues.html">Queues guide</a> to learn more.
        /// </summary>
        /// <param name="queue">The name of the queue.</param>
        /// <param name="ifUnused">Only delete the queue if it is unused.</param>
        /// <param name="ifEmpty">Only delete the queue if it is empty.</param>
        /// <param name="noWait">If set to <c>true</c>, do not require a response from the server.</param>
        /// <param name="cancellationToken">CancellationToken for this operation.</param>
        /// <remarks>
        ///Returns the number of messages purged during queue deletion.
        /// </remarks>
        Task<uint> QueueDeleteAsync(string queue, bool ifUnused, bool ifEmpty, bool noWait = false,
            CancellationToken cancellationToken = default);

        /// <summary>Asynchronously purge a queue of messages.</summary>
        /// <param name="queue">The queue.</param>
        /// <param name="cancellationToken">CancellationToken for this operation.</param>
        /// <returns>Returns the number of messages purged.</returns>
        Task<uint> QueuePurgeAsync(string queue, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously bind a queue to an exchange.
        /// </summary>
        /// <param name="queue">The queue.</param>
        /// <param name="exchange">The exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="arguments">The arguments.</param>
        /// <param name="noWait">If set to <c>true</c>, do not require a response from the server.</param>
        /// <param name="cancellationToken">CancellationToken for this operation.</param>
        /// <remarks>
        /// Routing key must be shorter than 255 bytes.
        /// </remarks>
        Task QueueBindAsync(string queue, string exchange, string routingKey,
            IDictionary<string, object?>? arguments = null, bool noWait = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously unbind a queue from an exchange.
        /// </summary>
        /// <param name="queue">The queue.</param>
        /// <param name="exchange">The exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="arguments">The arguments.</param>
        /// <param name="cancellationToken">CancellationToken for this operation.</param>
        /// <remarks>
        /// Routing key must be shorter than 255 bytes.
        /// </remarks>
        Task QueueUnbindAsync(string queue, string exchange, string routingKey,
            IDictionary<string, object?>? arguments = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the number of messages in a queue ready to be delivered
        /// to consumers. This method assumes the queue exists. If it doesn't,
        /// an exception will be closed with an exception.
        /// </summary>
        /// <param name="queue">The name of the queue</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task<uint> MessageCountAsync(string queue, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the number of consumers on a queue.
        /// This method assumes the queue exists. If it doesn't,
        /// an exception will be closed with an exception.
        /// </summary>
        /// <param name="queue">The name of the queue</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task<uint> ConsumerCountAsync(string queue, CancellationToken cancellationToken = default);

        /// <summary>Asynchronously commit this session's active TX transaction.</summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task TxCommitAsync(CancellationToken cancellationToken = default);

        /// <summary>Asynchronously roll back this session's active TX transaction.</summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task TxRollbackAsync(CancellationToken cancellationToken = default);

        /// <summary>Asynchronously enable TX mode for this session.</summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task TxSelectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously wait until all published messages on this channel have been confirmed.
        /// </summary>
        /// <returns>True if no nacks were received within the timeout, otherwise false.</returns>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <remarks>
        /// Waits until all messages published on this channel since the last call have
        /// been either ack'd or nack'd by the server. Returns whether
        /// all the messages were ack'd (and none were nack'd).
        /// Throws an exception when called on a channel
        /// that does not have publisher confirms enabled.
        /// </remarks>
        Task<bool> WaitForConfirmsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Wait until all published messages on this channel have been confirmed.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <remarks>
        /// Waits until all messages published on this channel since the last call have
        /// been ack'd by the server. If a nack is received or the timeout
        /// elapses, throws an IOException exception immediately and closes
        /// the channel.
        /// </remarks>
        Task WaitForConfirmsOrDieAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Amount of time protocol  operations (e.g. <code>queue.declare</code>) are allowed to take before
        /// timing out.
        /// </summary>
        TimeSpan ContinuationTimeout { get; set; }
    }
}
