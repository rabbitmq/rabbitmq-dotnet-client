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
using RabbitMQ.Client.client.impl;

namespace RabbitMQ.Client
{
    public static class IModelExtensions
    {
        /// <summary>Start a Basic content-class consumer.</summary>
        public static string BasicConsume(this IModel model,
            IBasicConsumer consumer,
            string queue,
            bool autoAck = false,
            string consumerTag = "",
            bool noLocal = false,
            bool exclusive = false,
            IDictionary<string, object> arguments = null)
            {
                return model.BasicConsume(queue, autoAck, consumerTag, noLocal, exclusive, arguments, consumer);
            }

        /// <summary>Start a Basic content-class consumer.</summary>
        public static string BasicConsume(this IModel model, string queue, bool autoAck, IBasicConsumer consumer)
        {
            return model.BasicConsume(queue, autoAck, "", false, false, null, consumer);
        }

        /// <summary>Start a Basic content-class consumer.</summary>
        public static string BasicConsume(this IModel model, string queue,
            bool autoAck,
            string consumerTag,
            IBasicConsumer consumer)
        {
            return model.BasicConsume(queue, autoAck, consumerTag, false, false, null, consumer);
        }

        /// <summary>Start a Basic content-class consumer.</summary>
        public static string BasicConsume(this IModel model, string queue,
            bool autoAck,
            string consumerTag,
            IDictionary<string, object> arguments,
            IBasicConsumer consumer)
        {
            return model.BasicConsume(queue, autoAck, consumerTag, false, false, arguments, consumer);
        }

#nullable enable
        /// <summary>
        /// (Extension method) Convenience overload of BasicPublish.
        /// </summary>
        /// <remarks>
        /// The publication occurs with mandatory=false and immediate=false.
        /// </remarks>
        public static void BasicPublish<T>(this IModel model, PublicationAddress addr, ref T basicProperties, ReadOnlyMemory<byte> body)
            where T : IReadOnlyBasicProperties, IAmqpHeader
        {
            model.BasicPublish(addr.ExchangeName, addr.RoutingKey, ref basicProperties, body);
        }

        public static void BasicPublish(this IModel model, string exchange, string routingKey, ReadOnlyMemory<byte> body = default, bool mandatory = false)
            => model.BasicPublish(exchange, routingKey, ref EmptyBasicProperty.Empty, body, mandatory);

        public static void BasicPublish(this IModel model, CachedString exchange, CachedString routingKey, ReadOnlyMemory<byte> body = default, bool mandatory = false)
            => model.BasicPublish(exchange, routingKey, ref EmptyBasicProperty.Empty, body, mandatory);
#nullable disable

        /// <summary>
        /// (Spec method) Declare a queue.
        /// </summary>
        public static QueueDeclareOk QueueDeclare(this IModel model, string queue = "", bool durable = false, bool exclusive = true,
            bool autoDelete = true, IDictionary<string, object> arguments = null)
            {
                return model.QueueDeclare(queue, durable, exclusive, autoDelete, arguments);
            }

        /// <summary>
        /// (Extension method) Bind an exchange to an exchange.
        /// </summary>
        public static void ExchangeBind(this IModel model, string destination, string source, string routingKey, IDictionary<string, object> arguments = null)
        {
            model.ExchangeBind(destination, source, routingKey, arguments);
        }

        /// <summary>
        /// (Extension method) Like exchange bind but sets nowait to true. 
        /// </summary>
        public static void ExchangeBindNoWait(this IModel model, string destination, string source, string routingKey, IDictionary<string, object> arguments = null)
        {
            model.ExchangeBindNoWait(destination, source, routingKey, arguments);
        }

        /// <summary>
        /// (Spec method) Declare an exchange.
        /// </summary>
        public static void ExchangeDeclare(this IModel model, string exchange, string type, bool durable = false, bool autoDelete = false,
            IDictionary<string, object> arguments = null)
            {
                model.ExchangeDeclare(exchange, type, durable, autoDelete, arguments);
            }

        /// <summary>
        /// (Extension method) Like ExchangeDeclare but sets nowait to true. 
        /// </summary>
        public static void ExchangeDeclareNoWait(this IModel model, string exchange, string type, bool durable = false, bool autoDelete = false,
            IDictionary<string, object> arguments = null)
            {
                model.ExchangeDeclareNoWait(exchange, type, durable, autoDelete, arguments);
            }

        /// <summary>
        /// (Spec method) Unbinds an exchange.
        /// </summary>
        public static void ExchangeUnbind(this IModel model, string destination,
            string source,
            string routingKey,
            IDictionary<string, object> arguments = null)
            {
                model.ExchangeUnbind(destination, source, routingKey, arguments);
            }

        /// <summary>
        /// (Spec method) Deletes an exchange.
        /// </summary>
        public static void ExchangeDelete(this IModel model, string exchange, bool ifUnused = false)
        {
            model.ExchangeDelete(exchange, ifUnused);
        }

        /// <summary>
        /// (Extension method) Like ExchangeDelete but sets nowait to true.
        /// </summary>
        public static void ExchangeDeleteNoWait(this IModel model, string exchange, bool ifUnused = false)
        {
            model.ExchangeDeleteNoWait(exchange, ifUnused);
        }

        /// <summary>
        /// (Spec method) Binds a queue.
        /// </summary>
        public static void QueueBind(this IModel model, string queue, string exchange, string routingKey, IDictionary<string, object> arguments = null)
        {
            model.QueueBind(queue, exchange, routingKey, arguments);
        }

        /// <summary>
        /// (Spec method) Deletes a queue.
        /// </summary>
        public static uint QueueDelete(this IModel model, string queue, bool ifUnused = false, bool ifEmpty = false)
        {
            return model.QueueDelete(queue, ifUnused, ifEmpty);
        }

        /// <summary>
        /// (Extension method) Like QueueDelete but sets nowait to true.
        /// </summary>
        public static void QueueDeleteNoWait(this IModel model, string queue, bool ifUnused = false, bool ifEmpty = false)
        {
            model.QueueDeleteNoWait(queue, ifUnused, ifEmpty);
        }

        /// <summary>
        /// (Spec method) Unbinds a queue.
        /// </summary>
        public static void QueueUnbind(this IModel model, string queue, string exchange, string routingKey, IDictionary<string, object> arguments = null)
        {
            model.QueueUnbind(queue, exchange, routingKey, arguments);
        }

        /// <summary>
        /// Abort this session.
        /// </summary>
        /// <remarks>
        /// If the session is already closed (or closing), then this
        /// method does nothing but wait for the in-progress close
        /// operation to complete. This method will not return to the
        /// caller until the shutdown is complete.
        /// In comparison to normal <see cref="Close(IModel)"/> method, <see cref="Abort(IModel)"/> will not throw
        /// <see cref="Exceptions.AlreadyClosedException"/> or <see cref="System.IO.IOException"/> or any other <see cref="Exception"/> during closing model.
        /// </remarks>
        public static void Abort(this IModel model)
        {
            model.Close(Constants.ReplySuccess, "Goodbye", true);
        }

        /// <summary>
        /// Abort this session.
        /// </summary>
        /// <remarks>
        /// The method behaves in the same way as <see cref="Abort(IModel)"/>, with the only
        /// difference that the model is closed with the given model close code and message.
        /// <para>
        /// The close code (See under "Reply Codes" in the AMQP specification)
        /// </para>
        /// <para>
        /// A message indicating the reason for closing the model
        /// </para>
        /// </remarks>
        public static void Abort(this IModel model, ushort replyCode, string replyText)
        {
            model.Close(replyCode, replyText, true);
        }

        /// <summary>Close this session.</summary>
        /// <remarks>
        /// If the session is already closed (or closing), then this
        /// method does nothing but wait for the in-progress close
        /// operation to complete. This method will not return to the
        /// caller until the shutdown is complete.
        /// </remarks>
        public static void Close(this IModel model)
        {
            model.Close(Constants.ReplySuccess, "Goodbye", false);
        }

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
        public static void Close(this IModel model, ushort replyCode, string replyText)
        {
            model.Close(replyCode, replyText, false);
        }
    }
}
