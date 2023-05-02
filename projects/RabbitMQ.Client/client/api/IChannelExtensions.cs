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
using System.Threading.Tasks;
using RabbitMQ.Client.client.impl;

namespace RabbitMQ.Client
{
    public static class IChannelExtensions
    {
        /// <summary>Start a Basic content-class consumer.</summary>
        public static string BasicConsume(this IChannel channel,
            IBasicConsumer consumer,
            string queue,
            bool autoAck = false,
            string consumerTag = "",
            bool noLocal = false,
            bool exclusive = false,
            IDictionary<string, object> arguments = null)
        {
            return channel.BasicConsume(queue, autoAck, consumerTag, noLocal, exclusive, arguments, consumer);
        }

        /// <summary>Start a Basic content-class consumer.</summary>
        public static string BasicConsume(this IChannel channel, string queue, bool autoAck, IBasicConsumer consumer)
        {
            return channel.BasicConsume(queue, autoAck, "", false, false, null, consumer);
        }

        /// <summary>Start a Basic content-class consumer.</summary>
        public static string BasicConsume(this IChannel channel, string queue,
            bool autoAck,
            string consumerTag,
            IBasicConsumer consumer)
        {
            return channel.BasicConsume(queue, autoAck, consumerTag, false, false, null, consumer);
        }

        /// <summary>Start a Basic content-class consumer.</summary>
        public static string BasicConsume(this IChannel channel, string queue,
            bool autoAck,
            string consumerTag,
            IDictionary<string, object> arguments,
            IBasicConsumer consumer)
        {
            return channel.BasicConsume(queue, autoAck, consumerTag, false, false, arguments, consumer);
        }

#nullable enable
        /// <summary>
        /// (Extension method) Convenience overload of BasicPublish.
        /// </summary>
        /// <remarks>
        /// The publication occurs with mandatory=false and immediate=false.
        /// </remarks>
        public static void BasicPublish<T>(this IChannel channel, PublicationAddress addr, in T basicProperties, ReadOnlyMemory<byte> body)
            where T : IReadOnlyBasicProperties, IAmqpHeader
        {
            channel.BasicPublish(addr.ExchangeName, addr.RoutingKey, in basicProperties, body);
        }

        public static void BasicPublish(this IChannel channel, string exchange, string routingKey, ReadOnlyMemory<byte> body = default, bool mandatory = false)
            => channel.BasicPublish(exchange, routingKey, in EmptyBasicProperty.Empty, body, mandatory);

        public static void BasicPublish(this IChannel channel, CachedString exchange, CachedString routingKey, ReadOnlyMemory<byte> body = default, bool mandatory = false)
            => channel.BasicPublish(exchange, routingKey, in EmptyBasicProperty.Empty, body, mandatory);

        public static ValueTask BasicPublishAsync(this IChannel channel, string exchange, string routingKey, ReadOnlyMemory<byte> body = default, bool mandatory = false)
            => channel.BasicPublishAsync(exchange, routingKey, in EmptyBasicProperty.Empty, body, mandatory);

        public static ValueTask BasicPublishAsync(this IChannel channel, CachedString exchange, CachedString routingKey, ReadOnlyMemory<byte> body = default, bool mandatory = false)
            => channel.BasicPublishAsync(exchange, routingKey, in EmptyBasicProperty.Empty, body, mandatory);
#nullable disable

        /// <summary>
        /// (Spec method) Declare a queue.
        /// </summary>
        public static QueueDeclareOk QueueDeclare(this IChannel channel, string queue = "", bool durable = false, bool exclusive = true,
            bool autoDelete = true, IDictionary<string, object> arguments = null)
        {
            return channel.QueueDeclare(queue, durable, exclusive, autoDelete, arguments);
        }

        /// <summary>
        /// (Extension method) Bind an exchange to an exchange.
        /// </summary>
        public static void ExchangeBind(this IChannel channel, string destination, string source, string routingKey, IDictionary<string, object> arguments = null)
        {
            channel.ExchangeBind(destination, source, routingKey, arguments);
        }

        /// <summary>
        /// (Extension method) Like exchange bind but sets nowait to true.
        /// </summary>
        public static void ExchangeBindNoWait(this IChannel channel, string destination, string source, string routingKey, IDictionary<string, object> arguments = null)
        {
            channel.ExchangeBindNoWait(destination, source, routingKey, arguments);
        }

        /// <summary>
        /// (Spec method) Declare an exchange.
        /// </summary>
        public static void ExchangeDeclare(this IChannel channel, string exchange, string type, bool durable = false, bool autoDelete = false,
            IDictionary<string, object> arguments = null)
        {
            channel.ExchangeDeclare(exchange, type, durable, autoDelete, arguments);
        }

        /// <summary>
        /// (Extension method) Like ExchangeDeclare but sets nowait to true.
        /// </summary>
        public static void ExchangeDeclareNoWait(this IChannel channel, string exchange, string type, bool durable = false, bool autoDelete = false,
            IDictionary<string, object> arguments = null)
        {
            channel.ExchangeDeclareNoWait(exchange, type, durable, autoDelete, arguments);
        }

        /// <summary>
        /// (Spec method) Unbinds an exchange.
        /// </summary>
        public static void ExchangeUnbind(this IChannel channel, string destination,
            string source,
            string routingKey,
            IDictionary<string, object> arguments = null)
        {
            channel.ExchangeUnbind(destination, source, routingKey, arguments);
        }

        /// <summary>
        /// (Spec method) Deletes an exchange.
        /// </summary>
        public static void ExchangeDelete(this IChannel channel, string exchange, bool ifUnused = false)
        {
            channel.ExchangeDelete(exchange, ifUnused);
        }

        /// <summary>
        /// (Extension method) Like ExchangeDelete but sets nowait to true.
        /// </summary>
        public static void ExchangeDeleteNoWait(this IChannel channel, string exchange, bool ifUnused = false)
        {
            channel.ExchangeDeleteNoWait(exchange, ifUnused);
        }

        /// <summary>
        /// (Spec method) Binds a queue.
        /// </summary>
        public static void QueueBind(this IChannel channel, string queue, string exchange, string routingKey, IDictionary<string, object> arguments = null)
        {
            channel.QueueBind(queue, exchange, routingKey, arguments);
        }

        /// <summary>
        /// (Spec method) Deletes a queue.
        /// </summary>
        public static uint QueueDelete(this IChannel channel, string queue, bool ifUnused = false, bool ifEmpty = false)
        {
            return channel.QueueDelete(queue, ifUnused, ifEmpty);
        }

        /// <summary>
        /// (Extension method) Like QueueDelete but sets nowait to true.
        /// </summary>
        public static void QueueDeleteNoWait(this IChannel channel, string queue, bool ifUnused = false, bool ifEmpty = false)
        {
            channel.QueueDeleteNoWait(queue, ifUnused, ifEmpty);
        }

        /// <summary>
        /// (Spec method) Unbinds a queue.
        /// </summary>
        public static void QueueUnbind(this IChannel channel, string queue, string exchange, string routingKey, IDictionary<string, object> arguments = null)
        {
            channel.QueueUnbind(queue, exchange, routingKey, arguments);
        }

        /// <summary>
        /// Abort this session.
        /// </summary>
        /// <remarks>
        /// If the session is already closed (or closing), then this
        /// method does nothing but wait for the in-progress close
        /// operation to complete. This method will not return to the
        /// caller until the shutdown is complete.
        /// In comparison to normal <see cref="Close(IChannel)"/> method, <see cref="Abort(IChannel)"/> will not throw
        /// <see cref="Exceptions.AlreadyClosedException"/> or <see cref="System.IO.IOException"/> or any other <see cref="Exception"/> during closing channel.
        /// </remarks>
        public static void Abort(this IChannel channel)
        {
            channel.Close(Constants.ReplySuccess, "Goodbye", true);
        }

        /// <summary>
        /// Abort this session.
        /// </summary>
        /// <remarks>
        /// The method behaves in the same way as <see cref="Abort(IChannel)"/>, with the only
        /// difference that the channel is closed with the given channel close code and message.
        /// <para>
        /// The close code (See under "Reply Codes" in the AMQP specification)
        /// </para>
        /// <para>
        /// A message indicating the reason for closing the channel
        /// </para>
        /// </remarks>
        public static void Abort(this IChannel channel, ushort replyCode, string replyText)
        {
            channel.Close(replyCode, replyText, true);
        }

        /// <summary>Close this session.</summary>
        /// <remarks>
        /// If the session is already closed (or closing), then this
        /// method does nothing but wait for the in-progress close
        /// operation to complete. This method will not return to the
        /// caller until the shutdown is complete.
        /// </remarks>
        public static void Close(this IChannel channel)
        {
            channel.Close(Constants.ReplySuccess, "Goodbye", false);
        }

        /// <summary>Close this session.</summary>
        /// <remarks>
        /// The method behaves in the same way as Close(), with the only
        /// difference that the channel is closed with the given channel
        /// close code and message.
        /// <para>
        /// The close code (See under "Reply Codes" in the AMQP specification)
        /// </para>
        /// <para>
        /// A message indicating the reason for closing the channel
        /// </para>
        /// </remarks>
        public static void Close(this IChannel channel, ushort replyCode, string replyText)
        {
            channel.Close(replyCode, replyText, false);
        }
    }
}
