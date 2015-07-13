// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2015 Pivotal Software, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at http://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using RabbitMQ.Client;
using RabbitMQ.Client.Apigen.Attributes;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;
using System;
using System.Collections.Generic;
using System.IO;

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
    public interface IModel : IDisposable
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
        /// Returns true if the model is no longer in a state where it can be used.
        /// </summary>
        bool IsClosed { get; }

        /// <summary>
        /// Returns true if the model is still in a state where it can be used.
        /// Identical to checking if <see cref="CloseReason"/> equals null.</summary>
        bool IsOpen { get; }

        /// <summary>
        /// When in confirm mode, return the sequence number of the next message to be published.
        /// </summary>
        ulong NextPublishSeqNo { get; }

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
        /// Signalled when an exception occurs in a callback invoked by the model.
        ///
        /// Examples of cases where this event will be signalled
        /// include exceptions thrown in <see cref="IBasicConsumer"/> methods, or
        /// exceptions thrown in <see cref="ModelShutdown"/> delegates etc.
        /// </summary>
        event EventHandler<CallbackExceptionEventArgs> CallbackException;

        event EventHandler<FlowControlEventArgs> FlowControl;

        /// <summary>
        /// Notifies the destruction of the model.
        /// </summary>
        /// <remarks>
        /// If the model is already destroyed at the time an event
        /// handler is added to this event, the event handler will be fired immediately.
        /// </remarks>
        event EventHandler<ShutdownEventArgs> ModelShutdown;

        /// <summary>
        /// Abort this session.
        /// </summary>
        /// <remarks>
        /// If the session is already closed (or closing), then this
        /// method does nothing but wait for the in-progress close
        /// operation to complete. This method will not return to the
        /// caller until the shutdown is complete.
        /// In comparison to normal <see cref="Close()"/> method, <see cref="Abort()"/> will not throw
        /// <see cref="Exceptions.AlreadyClosedException"/> or <see cref="IOException"/> during closing model.
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        void Abort();

        /// <summary>
        /// Abort this session.
        /// </summary>
        /// <remarks>
        /// The method behaves in the same way as <see cref="Abort()"/>, with the only
        /// difference that the model is closed with the given model close code and message.
        /// <para>
        /// The close code (See under "Reply Codes" in the AMQP specification)
        /// </para>
        /// <para>
        /// A message indicating the reason for closing the model
        /// </para>
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        void Abort(ushort replyCode, string replyText);

        /// <summary>
        /// (Spec method) Acknowledge one or more delivered message(s).
        /// </summary>
        void BasicAck(ulong deliveryTag, bool multiple);

        /// <summary>
        /// Delete a Basic content-class consumer.
        /// </summary>
        [AmqpMethodDoNotImplement(null)]
        void BasicCancel(string consumerTag);

        /// <summary>Start a Basic content-class consumer.</summary>
        /// <remarks>
        /// The consumer is started with noAck=false (i.e. BasicAck is required),
        /// an empty consumer tag (i.e. the server creates and returns a fresh consumer tag),
        /// noLocal=false and exclusive=false.
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        string BasicConsume(string queue, bool noAck, IBasicConsumer consumer);

        /// <summary>Start a Basic content-class consumer.</summary>
        /// <remarks>
        /// The consumer is started with
        /// an empty consumer tag (i.e. the server creates and returns a fresh consumer tag),
        /// noLocal=false and exclusive=false.
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        string BasicConsume(string queue, bool noAck, string consumerTag, IBasicConsumer consumer);

        /// <summary>Start a Basic content-class consumer.</summary>
        /// <remarks>
        /// The consumer is started with noLocal=false and exclusive=false.
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        string BasicConsume(string queue,
            bool noAck,
            string consumerTag,
            IDictionary<string, object> arguments,
            IBasicConsumer consumer);

        /// <summary>Start a Basic content-class consumer.</summary>
        [AmqpMethodDoNotImplement(null)]
        string BasicConsume(string queue,
            bool noAck,
            string consumerTag,
            bool noLocal,
            bool exclusive,
            IDictionary<string, object> arguments,
            IBasicConsumer consumer);

        /// <summary>
        /// (Spec method) Retrieve an individual message, if
        /// one is available; returns null if the server answers that
        /// no messages are currently available. See also <see cref="IModel.BasicAck"/>.
        /// </summary>
        [AmqpMethodDoNotImplement(null)]
        BasicGetResult BasicGet(string queue, bool noAck);

        /// <summary>Reject one or more delivered message(s).</summary>
        void BasicNack(ulong deliveryTag, bool multiple, bool requeue);

        /// <summary>
        /// (Spec method) Convenience overload of BasicPublish.
        /// </summary>
        /// <remarks>
        /// The publication occurs with mandatory=false and immediate=false.
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        void BasicPublish(PublicationAddress addr, IBasicProperties basicProperties, byte[] body);

        /// <summary>
        /// (Spec method) Convenience overload of BasicPublish.
        /// </summary>
        /// <remarks>
        /// The publication occurs with mandatory=false
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        void BasicPublish(string exchange, string routingKey, IBasicProperties basicProperties, byte[] body);

        /// <summary>
        /// (Spec method) Convenience overload of BasicPublish.
        /// </summary>
        [AmqpMethodDoNotImplement(null)]
        void BasicPublish(string exchange, string routingKey, bool mandatory,
            IBasicProperties basicProperties, byte[] body);

        /// <summary>
        /// (Spec method) Configures QoS parameters of the Basic content-class.
        /// </summary>
        void BasicQos(uint prefetchSize, ushort prefetchCount, bool global);

        /// <summary>
        /// (Spec method).
        /// </summary>
        [AmqpMethodDoNotImplement(null)]
        void BasicRecover(bool requeue);

        /// <summary>
        /// (Spec method).
        /// </summary>
        void BasicRecoverAsync(bool requeue);

        /// <summary>(Spec method) Reject a delivered message.</summary>
        void BasicReject(ulong deliveryTag, bool requeue);

        /// <summary>Close this session.</summary>
        /// <remarks>
        /// If the session is already closed (or closing), then this
        /// method does nothing but wait for the in-progress close
        /// operation to complete. This method will not return to the
        /// caller until the shutdown is complete.
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        void Close();

        /// <summary>Close this session.</summary>
        /// <remarks>
        /// The method behaves in the same way as Close(), with the only
        /// difference that the model is closed with the given model
        /// close code and message.
        /// <para>
        /// The close code (See under "Reply Codes" in the AMQP specification)
        /// </para>
        /// <para>
        /// A message indicating the reason for closing the model
        /// </para>
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        void Close(ushort replyCode, string replyText);

        /// <summary>
        /// Enable publisher acknowledgements.
        /// </summary>
        [AmqpMethodDoNotImplement(null)]
        void ConfirmSelect();

        /// <summary>
        /// Construct a completely empty content header for use with the Basic content class.
        /// </summary>
        [AmqpContentHeaderFactory("basic")]
        IBasicProperties CreateBasicProperties();

        /// <summary>
        /// (Extension method) Bind an exchange to an exchange.
        /// </summary>
        [AmqpMethodDoNotImplement(null)]
        void ExchangeBind(string destination, string source, string routingKey, IDictionary<string, object> arguments);

        /// <summary>
        /// (Extension method) Bind an exchange to an exchange.
        /// </summary>
        [AmqpMethodDoNotImplement(null)]
        void ExchangeBind(string destination, string source, string routingKey);

        /// <summary>
        ///Like ExchangeBind but sets nowait to true.
        /// </summary>
        void ExchangeBindNoWait(string destination, string source, string routingKey,
            IDictionary<string, object> arguments);

        /// <summary>(Spec method) Declare an exchange.</summary>
        /// <remarks>
        /// The exchange is declared non-passive and non-internal.
        /// The "nowait" option is not exercised.
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        void ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete,
            IDictionary<string, object> arguments);

        /// <summary>
        /// (Spec method) Declare an exchange.
        /// </summary>
        /// <remarks>
        /// The exchange is declared non-passive, non-autodelete, and
        /// non-internal, with no arguments. The "nowait" option is not exercised.
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        void ExchangeDeclare(string exchange, string type, bool durable);

        /// <summary>
        /// (Spec method) Declare an exchange.
        /// </summary>
        /// <remarks>
        /// The exchange is declared non-passive, non-durable, non-autodelete, and
        /// non-internal, with no arguments. The "nowait" option is not exercised.
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        void ExchangeDeclare(string exchange, string type);

        /// <summary>
        /// Same as ExchangeDeclare but sets nowait to true and returns void (as there
        /// will be no response from the server).
        /// </summary>
        void ExchangeDeclareNoWait(string exchange, string type, bool durable, bool autoDelete,
            IDictionary<string, object> arguments);

        /// <summary>
        /// (Spec method) Declare an exchange.
        /// </summary>
        /// <remarks>
        /// The exchange is declared passive.
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        void ExchangeDeclarePassive(string exchange);

        /// <summary>
        /// (Spec method) Delete an exchange.
        /// </summary>
        [AmqpMethodDoNotImplement(null)]
        void ExchangeDelete(string exchange, bool ifUnused);

        /// <summary>(Spec method) Delete an exchange.</summary>
        /// <remarks>
        /// The exchange is deleted regardless of any queue bindings.
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        void ExchangeDelete(string exchange);

        /// <summary>
        ///Like ExchangeDelete but sets nowait to true.
        /// </summary>
        void ExchangeDeleteNoWait(string exchange, bool ifUnused);

        /// <summary>
        /// (Extension method) Unbind an exchange from an exchange.
        /// </summary>
        [AmqpMethodDoNotImplement(null)]
        void ExchangeUnbind(string destination,
            string source,
            string routingKey,
            IDictionary<string, object> arguments);

        /// <summary>
        /// (Extension method) Unbind an exchange from an exchange.
        /// </summary>
        [AmqpMethodDoNotImplement(null)]
        void ExchangeUnbind(string destination, string source, string routingKey);

        /// <summary>
        /// Like ExchangeUnbind but sets nowait to true.
        /// </summary>
        [AmqpMethodDoNotImplement(null)]
        void ExchangeUnbindNoWait(string destination, string source, string routingKey,
            IDictionary<string, object> arguments);

        /// <summary>
        /// (Spec method) Bind a queue to an exchange.
        /// </summary>
        [AmqpMethodDoNotImplement(null)]
        void QueueBind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments);

        /// <summary>(Spec method) Bind a queue to an exchange.</summary>
        [AmqpMethodDoNotImplement(null)]
        void QueueBind(string queue, string exchange, string routingKey);

        /// <summary>Same as QueueBind but sets nowait parameter to true.</summary>
        void QueueBindNoWait(string queue, string exchange, string routingKey, IDictionary<string, object> arguments);

        /// <summary>(Spec method) Declare a queue.</summary>
        /// <remarks>
        /// The queue is declared non-passive, non-durable,
        /// but exclusive and autodelete, with no arguments. The
        /// server autogenerates a name for the queue - the generated name is the return value of this method.
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        QueueDeclareOk QueueDeclare();

        /// <summary>(Spec method) Declare a queue.</summary>
        [AmqpMethodDoNotImplement(null)]
        QueueDeclareOk QueueDeclare(string queue, bool durable, bool exclusive,
            bool autoDelete, IDictionary<string, object> arguments);

        /// <summary>
        /// Same as QueueDeclare but sets nowait to true and returns void (as there
        /// will be no response from the server).
        /// </summary>
        void QueueDeclareNoWait(string queue, bool durable, bool exclusive,
            bool autoDelete, IDictionary<string, object> arguments);

        /// <summary>Declare a queue passively.</summary>
        /// <remarks>
        ///The queue is declared passive, non-durable,
        ///non-exclusive, and non-autodelete, with no arguments.
        ///The queue is declared passively; i.e. only check if it exists.
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        QueueDeclareOk QueueDeclarePassive(string queue);

        /// <summary>
        /// Returns the number of messages in a queue ready to be delivered
        /// to consumers. This method assumes the queue exists. If it doesn't,
        /// an exception will be closed with an exception.
        /// </summary>
        /// <param name="queue">The name of the queue</param>
        [AmqpMethodDoNotImplement(null)]
        uint MessageCount(string queue);

        /// <summary>
        /// Returns the number of consumers on a queue.
        /// This method assumes the queue exists. If it doesn't,
        /// an exception will be closed with an exception.
        /// </summary>
        /// <param name="queue">The name of the queue</param>
        [AmqpMethodDoNotImplement(null)]
        uint ConsumerCount(string queue);

        /// <summary>
        /// (Spec method) Delete a queue.
        /// </summary>
        /// <remarks>
        ///Returns the number of messages purged during queue deletion.
        /// <code>uint.MaxValue</code>.
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        uint QueueDelete(string queue, bool ifUnused, bool ifEmpty);

        /// <summary>
        /// (Spec method) Delete a queue.
        /// </summary>
        /// <remarks>
        ///Returns the number of messages purged during queue deletion.
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        uint QueueDelete(string queue);

        /// <summary>
        ///Same as QueueDelete but sets nowait parameter to true
        ///and returns void (as there will be no response from the server)
        /// </summary>
        void QueueDeleteNoWait(string queue, bool ifUnused, bool ifEmpty);

        /// <summary>
        /// (Spec method) Purge a queue of messages.
        /// </summary>
        /// <remarks>
        /// Returns the number of messages purged.
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        uint QueuePurge(string queue);

        /// <summary>
        /// (Spec method) Unbind a queue from an exchange.
        /// </summary>
        void QueueUnbind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments);

        /// <summary>
        /// (Spec method) Commit this session's active TX transaction.
        /// </summary>
        void TxCommit();

        /// <summary>
        /// (Spec method) Roll back this session's active TX transaction.
        /// </summary>
        void TxRollback();

        /// <summary>
        /// (Spec method) Enable TX mode for this session.
        /// </summary>
        void TxSelect();

        /// <summary>Wait until all published messages have been confirmed.
        /// </summary>
        /// <remarks>
        /// Waits until all messages published since the last call have
        /// been either ack'd or nack'd by the broker.  Returns whether
        /// all the messages were ack'd (and none were nack'd). Note,
        /// throws an exception when called on a non-Confirm channel.
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        bool WaitForConfirms();

        /// <summary>
        /// Wait until all published messages have been confirmed.
        /// </summary>
        /// <returns>True if no nacks were received within the timeout, otherwise false.</returns>
        /// <param name="timeout">How long to wait (at most) before returning
        ///whether or not any nacks were returned.
        /// </param>
        /// <remarks>
        /// Waits until all messages published since the last call have
        /// been either ack'd or nack'd by the broker.  Returns whether
        /// all the messages were ack'd (and none were nack'd). Note,
        /// throws an exception when called on a non-Confirm channel.
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        bool WaitForConfirms(TimeSpan timeout);

        /// <summary>
        /// Wait until all published messages have been confirmed.
        /// </summary>
        /// <returns>True if no nacks were received within the timeout, otherwise false.</returns>
        /// <param name="timeout">How long to wait (at most) before returning
        /// whether or not any nacks were returned.
        /// </param>
        /// <param name="timedOut">True if the method returned because
        /// the timeout elapsed, not because all messages were ack'd or at least one nack'd.
        /// </param>
        /// <remarks>
        /// Waits until all messages published since the last call have
        /// been either ack'd or nack'd by the broker.  Returns whether
        /// all the messages were ack'd (and none were nack'd). Note,
        /// throws an exception when called on a non-Confirm channel.
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        bool WaitForConfirms(TimeSpan timeout, out bool timedOut);

        /// <summary>
        /// Wait until all published messages have been confirmed.
        /// </summary>
        /// <remarks>
        /// Waits until all messages published since the last call have
        /// been ack'd by the broker.  If a nack is received, throws an
        /// OperationInterrupedException exception immediately.
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        void WaitForConfirmsOrDie();

        /// <summary>
        /// Wait until all published messages have been confirmed.
        /// </summary>
        /// <remarks>
        /// Waits until all messages published since the last call have
        /// been ack'd by the broker.  If a nack is received or the timeout
        /// elapses, throws an OperationInterrupedException exception immediately.
        /// </remarks>
        [AmqpMethodDoNotImplement(null)]
        void WaitForConfirmsOrDie(TimeSpan timeout);
    }
}
