// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
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
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at https://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabbitMQ.Client
{
    public static class IModelExensions
    {
        /// <summary>Start a Basic content-class consumer.</summary>
        public static ValueTask<string> BasicConsume(this IModel model,
            IAsyncBasicConsumer consumer,
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
        public static ValueTask<string> BasicConsume(this IModel model, string queue, bool autoAck, IAsyncBasicConsumer consumer)
        {
            return model.BasicConsume(queue, autoAck, "", false, false, null, consumer);
        }

        /// <summary>Start a Basic content-class consumer.</summary>
        public static ValueTask<string> BasicConsume(this IModel model, string queue,
            bool autoAck,
            string consumerTag,
            IAsyncBasicConsumer consumer)
        {
            return model.BasicConsume(queue, autoAck, consumerTag, false, false, null, consumer);
        }

        /// <summary>Start a Basic content-class consumer.</summary>
        public static ValueTask<string> BasicConsume(this IModel model, string queue,
            bool autoAck,
            string consumerTag,
            IDictionary<string, object> arguments,
            IAsyncBasicConsumer consumer)
        {
            return model.BasicConsume(queue, autoAck, consumerTag, false, false, arguments, consumer);
        }

        /// <summary>
        /// (Extension method) Convenience overload of BasicPublish.
        /// </summary>
        /// <remarks>
        /// The publication occurs with mandatory=false and immediate=false.
        /// </remarks>
        public static ValueTask BasicPublish(this IModel model, PublicationAddress addr, IBasicProperties basicProperties, ReadOnlyMemory<byte> body)
        {
            return model.BasicPublish(addr.ExchangeName, addr.RoutingKey, basicProperties: basicProperties, body: body);
        }

        /// <summary>
        /// (Extension method) Convenience overload of BasicPublish.
        /// </summary>
        /// <remarks>
        /// The publication occurs with mandatory=false
        /// </remarks>
        public static ValueTask BasicPublish(this IModel model, string exchange, string routingKey, IBasicProperties basicProperties, ReadOnlyMemory<byte> body)
        {
            return model.BasicPublish(exchange, routingKey, false, basicProperties, body);
        }

        /// <summary>
        /// (Spec method) Convenience overload of BasicPublish.
        /// </summary>
        public static ValueTask BasicPublish(this IModel model, string exchange, string routingKey, bool mandatory = false, IBasicProperties basicProperties = null, ReadOnlyMemory<byte> body = default)
        {
            return model.BasicPublish(exchange, routingKey, mandatory, basicProperties, body);
        }

        /// <summary>
        /// (Spec method) Declare a queue.
        /// </summary>
        public static ValueTask<QueueDeclareOk> QueueDeclare(this IModel model, string queue = "", bool durable = false, bool exclusive = true,
            bool autoDelete = true, IDictionary<string, object> arguments = null)
        {
            return model.QueueDeclare(queue, durable, exclusive, autoDelete, arguments);
        }

        /// <summary>
        /// (Extension method) Bind an exchange to an exchange.
        /// </summary>
        public static ValueTask ExchangeBind(this IModel model, string destination, string source, string routingKey, IDictionary<string, object> arguments = null)
        {
            return model.ExchangeBind(destination, source, routingKey, arguments);
        }

        /// <summary>
        /// (Extension method) Like exchange bind but sets nowait to true. 
        /// </summary>
        public static ValueTask ExchangeBindNoWait(this IModel model, string destination, string source, string routingKey, IDictionary<string, object> arguments = null)
        {
            return model.ExchangeBindNoWait(destination, source, routingKey, arguments);
        }

        /// <summary>
        /// (Spec method) Declare an exchange.
        /// </summary>
        public static ValueTask ExchangeDeclare(this IModel model, string exchange, string type, bool durable = false, bool autoDelete = false,
            IDictionary<string, object> arguments = null)
        {
            return model.ExchangeDeclare(exchange, type, durable, autoDelete, arguments);
        }

        /// <summary>
        /// (Extension method) Like ExchangeDeclare but sets nowait to true. 
        /// </summary>
        public static ValueTask ExchangeDeclareNoWait(this IModel model, string exchange, string type, bool durable = false, bool autoDelete = false, IDictionary<string, object> arguments = null)
        {
            return model.ExchangeDeclareNoWait(exchange, type, durable, autoDelete, arguments);
        }

        /// <summary>
        /// (Spec method) Unbinds an exchange.
        /// </summary>
        public static ValueTask ExchangeUnbind(this IModel model, string destination,
            string source,
            string routingKey,
            IDictionary<string, object> arguments = null)
        {
            return model.ExchangeUnbind(destination, source, routingKey, arguments);
        }

        /// <summary>
        /// (Spec method) Deletes an exchange.
        /// </summary>
        public static ValueTask ExchangeDelete(this IModel model, string exchange, bool ifUnused = false)
        {
            return model.ExchangeDelete(exchange, ifUnused);
        }

        /// <summary>
        /// (Extension method) Like ExchangeDelete but sets nowait to true.
        /// </summary>
        public static ValueTask ExchangeDeleteNoWait(this IModel model, string exchange, bool ifUnused = false)
        {
            return model.ExchangeDeleteNoWait(exchange, ifUnused);
        }

        /// <summary>
        /// (Spec method) Binds a queue.
        /// </summary>
        public static ValueTask QueueBind(this IModel model, string queue, string exchange, string routingKey, IDictionary<string, object> arguments = null)
        {
            return model.QueueBind(queue, exchange, routingKey, arguments);
        }

        /// <summary>
        /// (Spec method) Deletes a queue.
        /// </summary>
        public static ValueTask<uint> QueueDelete(this IModel model, string queue, bool ifUnused = false, bool ifEmpty = false)
        {
            return model.QueueDelete(queue, ifUnused, ifEmpty);
        }

        /// <summary>
        /// (Extension method) Like QueueDelete but sets nowait to true.
        /// </summary>
        public static ValueTask QueueDeleteNoWait(this IModel model, string queue, bool ifUnused = false, bool ifEmpty = false)
        {
            return model.QueueDeleteNoWait(queue, ifUnused, ifEmpty);
        }

        /// <summary>
        /// (Spec method) Unbinds a queue.
        /// </summary>
        public static ValueTask QueueUnbind(this IModel model, string queue, string exchange, string routingKey, IDictionary<string, object> arguments = null)
        {
            return model.QueueUnbind(queue, exchange, routingKey, arguments);
        }
    }
}
