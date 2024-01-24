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
        IBasicConsumer DefaultConsumer { get; set; }

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
        ValueTask BasicAckAsync(ulong deliveryTag, bool multiple);

        /// <summary>Asynchronously cancel a Basic content-class consumer.</summary>
        /// <param name="consumerTag">The consumer tag.</param>
        /// <param name="noWait">If set to <c>true</c>, do not require a response from the server.</param>
        Task BasicCancelAsync(string consumerTag, bool noWait = false);

        /// <summary>Asynchronously start a Basic content-class consumer.</summary>
        /// <param name="queue">The queue.</param>
        /// <param name="autoAck">If set to <c>true</c>, automatically ack messages.</param>
        /// <param name="consumerTag">The consumer tag.</param>
        /// <param name="noLocal">If set to <c>true</c>, this consumer will not receive messages published by the same connection.</param>
        /// <param name="exclusive">If set to <c>true</c>, the consumer is exclusive.</param>
        /// <param name="arguments">Consumer arguments.</param>
        /// <param name="consumer">The consumer, an instance of <see cref="IBasicConsumer"/></param>
        /// <returns></returns>
        Task<string> BasicConsumeAsync(string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive, IDictionary<string, object> arguments, IBasicConsumer consumer);

        /// <summary>
        /// Asynchronously retrieve an individual message, if
        /// one is available; returns null if the server answers that
        /// no messages are currently available. See also <see cref="IChannel.BasicAckAsync" />.
        /// </summary>
        /// <param name="queue">The queue.</param>
        /// <param name="autoAck">If set to <c>true</c>, automatically ack the message.</param>
        /// <returns><see cref="BasicGetResult"/></returns>
        ValueTask<BasicGetResult> BasicGetAsync(string queue, bool autoAck);

        /// <summary>
        /// Asynchronously nack one or more delivered message(s).
        /// </summary>
        /// <param name="deliveryTag">The delivery tag.</param>
        /// <param name="multiple">If set to <c>true</c>, nack all messages up to the current tag.</param>
        /// <param name="requeue">If set to <c>true</c>, requeue nack'd messages.</param>
        ValueTask BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue);

#nullable enable

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
        Task BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global);

        /// <summary> Reject a delivered message.</summary>
        Task BasicRejectAsync(ulong deliveryTag, bool requeue);

        /// <summary>
        /// Asynchronously close this session.
        /// </summary>
        /// <param name="replyCode">The reply code to send for closing (See under "Reply Codes" in the AMQP specification).</param>
        /// <param name="replyText">The reply text to send for closing.</param>
        /// <param name="abort">Whether or not the close is an abort (ignoring certain exceptions).</param>
        Task CloseAsync(ushort replyCode, string replyText, bool abort);

        /// <summary>
        /// Asynchronously close this session.
        /// </summary>
        /// <param name="reason">The <see cref="ShutdownEventArgs"/> instance containing the close data.</param>
        /// <param name="abort">Whether or not the close is an abort (ignoring certain exceptions).</param>
        /// <returns></returns>
        Task CloseAsync(ShutdownEventArgs reason, bool abort);

        /// <summary>Asynchronously enable publisher confirmations.</summary>
        Task ConfirmSelectAsync();

        /// <summary>
        /// Asynchronously binds an exchange to an exchange.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Routing key must be shorter than 255 bytes.
        ///   </para>
        /// </remarks>
        Task ExchangeBindAsync(string destination, string source, string routingKey,
            IDictionary<string, object> arguments = null, bool noWait = false);

        /// <summary>Asynchronously declare an exchange.</summary>
        /// <remarks>
        /// The exchange is declared non-internal.
        /// </remarks>
        Task ExchangeDeclareAsync(string exchange, string type, bool durable, bool autoDelete,
            IDictionary<string, object> arguments = null, bool passive = false, bool noWait = false);

        /// <summary>
        /// Asynchronously do a passive exchange declaration.
        /// </summary>
        /// <remarks>
        /// This method performs a "passive declare" on an exchange,
        /// which checks whether an exchange exists.
        /// It will do nothing if the exchange already exists and result
        /// in a channel-level protocol exception (channel closure) if not.
        /// </remarks>
        Task ExchangeDeclarePassiveAsync(string exchange);

        /// <summary>
        /// Asynchronously delete an exchange.
        /// </summary>
        Task ExchangeDeleteAsync(string exchange, bool ifUnused = false, bool noWait = false);

        /// <summary>
        /// Asynchronously unbind an exchange from an exchange.
        /// </summary>
        /// <remarks>
        /// Routing key must be shorter than 255 bytes.
        /// </remarks>
        Task ExchangeUnbindAsync(string destination, string source, string routingKey,
            IDictionary<string, object> arguments = null, bool noWait = false);

        /// <summary>
        /// Asynchronously bind a queue to an exchange.
        /// </summary>
        /// <param name="queue">The queue.</param>
        /// <param name="exchange">The exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="arguments">The arguments.</param>
        /// <param name="noWait">If set to <c>true</c>, do not require a response from the server.</param>
        /// <remarks>
        /// Routing key must be shorter than 255 bytes.
        /// </remarks>
        Task QueueBindAsync(string queue, string exchange, string routingKey,
            IDictionary<string, object> arguments = null, bool noWait = false);

        /// <summary>
        /// Asynchronously declares a queue. See the <a href="https://www.rabbitmq.com/queues.html">Queues guide</a> to learn more.
        /// </summary>
        /// <param name="queue">The name of the queue. Pass an empty string to make the server generate a name.</param>
        /// <param name="durable">Should this queue will survive a broker restart?</param>
        /// <param name="exclusive">Should this queue use be limited to its declaring connection? Such a queue will be deleted when its declaring connection closes.</param>
        /// <param name="autoDelete">Should this queue be auto-deleted when its last consumer (if any) unsubscribes?</param>
        /// <param name="arguments">Optional; additional queue arguments, e.g. "x-queue-type"</param>
        /// <param name="passive">Optional; Set to <code>true</code> to passively declare the queue (i.e. check for its existence)</param>
        /// <param name="noWait">Optional; Set to <c>true</c> to not require a response from the server.</param>
        Task<QueueDeclareOk> QueueDeclareAsync(string queue, bool durable, bool exclusive, bool autoDelete,
            IDictionary<string, object> arguments = null, bool passive = false, bool noWait = false);

        /// <summary>Asynchronously declare a queue passively.</summary>
        /// <remarks>
        ///The queue is declared passive, non-durable,
        ///non-exclusive, and non-autodelete, with no arguments.
        ///The queue is declared passively; i.e. only check if it exists.
        /// </remarks>
        Task<QueueDeclareOk> QueueDeclarePassiveAsync(string queue);

        /// <summary>
        /// Returns the number of messages in a queue ready to be delivered
        /// to consumers. This method assumes the queue exists. If it doesn't,
        /// an exception will be closed with an exception.
        /// </summary>
        /// <param name="queue">The name of the queue</param>
        Task<uint> MessageCountAsync(string queue);

        /// <summary>
        /// Returns the number of consumers on a queue.
        /// This method assumes the queue exists. If it doesn't,
        /// an exception will be closed with an exception.
        /// </summary>
        /// <param name="queue">The name of the queue</param>
        Task<uint> ConsumerCountAsync(string queue);

        /// <summary>
        /// Asynchronously deletes a queue. See the <a href="https://www.rabbitmq.com/queues.html">Queues guide</a> to learn more.
        /// </summary>
        /// <param name="queue">The name of the queue.</param>
        /// <param name="ifUnused">Only delete the queue if it is unused.</param>
        /// <param name="ifEmpty">Only delete the queue if it is empty.</param>
        /// <param name="noWait">If set to <c>true</c>, do not require a response from the server.</param>
        /// <remarks>
        ///Returns the number of messages purged during queue deletion.
        /// </remarks>
        Task<uint> QueueDeleteAsync(string queue, bool ifUnused, bool ifEmpty, bool noWait = false);

        /// <summary>Asynchronously purge a queue of messages.</summary>
        /// <param name="queue">The queue.</param>
        /// <returns>Returns the number of messages purged.</returns>
        Task<uint> QueuePurgeAsync(string queue);

        /// <summary>
        /// Asynchronously unbind a queue from an exchange.
        /// </summary>
        /// <param name="queue">The queue.</param>
        /// <param name="exchange">The exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="arguments">The arguments.</param>
        /// <remarks>
        /// Routing key must be shorter than 255 bytes.
        /// </remarks>
        Task QueueUnbindAsync(string queue, string exchange, string routingKey, IDictionary<string, object> arguments);

        /// <summary>Asynchronously commit this session's active TX transaction.</summary>
        Task TxCommitAsync();

        /// <summary>Asynchronously roll back this session's active TX transaction.</summary>
        Task TxRollbackAsync();

        /// <summary>Asynchronously enable TX mode for this session.</summary>
        Task TxSelectAsync();

        /// <summary>
        /// Asynchronously wait until all published messages on this channel have been confirmed.
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
