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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.client.impl;

namespace RabbitMQ.Client
{
    public static class IChannelExtensions
    {
        /// <summary>Asynchronously start a Basic content-class consumer.</summary>
        public static Task<string> BasicConsumeAsync(this IChannel channel,
            IBasicConsumer consumer,
            string queue,
            bool autoAck = false,
            string consumerTag = "",
            bool noLocal = false,
            bool exclusive = false,
            IDictionary<string, object> arguments = null)
        {
            return channel.BasicConsumeAsync(queue, autoAck, consumerTag, noLocal, exclusive, arguments, consumer);
        }

        /// <summary>Asynchronously start a Basic content-class consumer.</summary>
        public static Task<string> BasicConsumeAsync(this IChannel channel, string queue,
            bool autoAck,
            IBasicConsumer consumer)
        {
            return channel.BasicConsumeAsync(queue, autoAck, string.Empty, false, false, null, consumer);
        }

        /// <summary>Asynchronously start a Basic content-class consumer.</summary>
        public static Task<string> BasicConsumeAsync(this IChannel channel, string queue,
            bool autoAck,
            string consumerTag,
            IBasicConsumer consumer)
        {
            return channel.BasicConsumeAsync(queue, autoAck, consumerTag, false, false, null, consumer);
        }

        /// <summary>Asynchronously start a Basic content-class consumer.</summary>
        public static Task<string> BasicConsumeAsync(this IChannel channel, string queue,
            bool autoAck,
            string consumerTag,
            IDictionary<string, object> arguments,
            IBasicConsumer consumer)
        {
            return channel.BasicConsumeAsync(queue, autoAck, consumerTag, false, false, arguments, consumer);
        }

#nullable enable

        /// <summary>
        /// (Extension method) Convenience overload of BasicPublish.
        /// </summary>
        /// <remarks>
        /// The publication occurs with mandatory=false and immediate=false.
        /// </remarks>
        public static ValueTask BasicPublishAsync<T>(this IChannel channel, PublicationAddress addr, T basicProperties,
            ReadOnlyMemory<byte> body)
            where T : IReadOnlyBasicProperties, IAmqpHeader
        {
            return channel.BasicPublishAsync(addr.ExchangeName, addr.RoutingKey, basicProperties, body);
        }

        public static ValueTask BasicPublishAsync(this IChannel channel, string exchange, string routingKey,
            ReadOnlyMemory<byte> body = default, bool mandatory = false) =>
            channel.BasicPublishAsync(exchange, routingKey, EmptyBasicProperty.Empty, body, mandatory);

        public static ValueTask BasicPublishAsync(this IChannel channel, CachedString exchange,
            CachedString routingKey, ReadOnlyMemory<byte> body = default, bool mandatory = false) =>
            channel.BasicPublishAsync(exchange, routingKey, EmptyBasicProperty.Empty, body, mandatory);

#nullable disable

        /// <summary>
        /// Asynchronously declare a queue.
        /// </summary>
        public static Task<QueueDeclareOk> QueueDeclareAsync(this IChannel channel, string queue = "", bool durable = false, bool exclusive = true,
            bool autoDelete = true, IDictionary<string, object> arguments = null, bool noWait = false)
        {
            return channel.QueueDeclareAsync(queue: queue, passive: false,
                durable: durable, exclusive: exclusive, autoDelete: autoDelete,
                arguments: arguments, noWait: noWait);
        }

        /// <summary>
        /// Asynchronously declare an exchange.
        /// </summary>
        public static Task ExchangeDeclareAsync(this IChannel channel, string exchange, string type, bool durable = false, bool autoDelete = false,
            IDictionary<string, object> arguments = null, bool noWait = false)
        {
            return channel.ExchangeDeclareAsync(exchange, type, durable, autoDelete,
                arguments: arguments, passive: false, noWait: noWait);
        }

        /// <summary>
        /// Asynchronously deletes a queue.
        /// </summary>
        public static Task<uint> QueueDeleteAsync(this IChannel channel, string queue, bool ifUnused = false, bool ifEmpty = false)
        {
            return channel.QueueDeleteAsync(queue, ifUnused, ifEmpty);
        }

        /// <summary>
        /// Asynchronously unbinds a queue.
        /// </summary>
        public static Task QueueUnbindAsync(this IChannel channel, string queue, string exchange, string routingKey, IDictionary<string, object> arguments = null)
        {
            return channel.QueueUnbindAsync(queue, exchange, routingKey, arguments);
        }

        /// <summary>
        /// Asynchronously abort this session.
        /// </summary>
        /// <remarks>
        /// If the session is already closed (or closing), then this
        /// method does nothing but wait for the in-progress close
        /// operation to complete. This method will not return to the
        /// caller until the shutdown is complete.
        /// In comparison to normal <see cref="CloseAsync(IChannel, CancellationToken)"/> method, <see cref="AbortAsync(IChannel, CancellationToken)"/> will not throw
        /// <see cref="Exceptions.AlreadyClosedException"/> or <see cref="System.IO.IOException"/> or any other <see cref="Exception"/> during closing channel.
        /// </remarks>
        public static Task AbortAsync(this IChannel channel, CancellationToken cancellationToken = default)
        {
            return channel.CloseAsync(Constants.ReplySuccess, "Goodbye", true,
                cancellationToken);
        }

        /// <summary>Asynchronously close this session.</summary>
        /// <remarks>
        /// If the session is already closed (or closing), then this
        /// method does nothing but wait for the in-progress close
        /// operation to complete. This method will not return to the
        /// caller until the shutdown is complete.
        /// </remarks>
        public static Task CloseAsync(this IChannel channel, CancellationToken cancellationToken = default)
        {
            return channel.CloseAsync(Constants.ReplySuccess, "Goodbye", false,
                cancellationToken);
        }

        /// <summary>
        /// Asynchronously close this channel.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="replyCode">The reply code.</param>
        /// <param name="replyText">The reply text.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <remarks>
        /// The method behaves in the same way as Close(), with the only
        /// difference that the channel is closed with the given channel
        /// close code and message.
        /// <para>
        /// The close code (See under "Reply Codes" in the AMQP specification)
        /// </para><para>
        /// A message indicating the reason for closing the channel
        /// </para>
        /// </remarks>
        public static Task CloseAsync(this IChannel channel, ushort replyCode, string replyText,
            CancellationToken cancellationToken = default)
        {
            return channel.CloseAsync(replyCode, replyText, false, cancellationToken);
        }
    }
}
