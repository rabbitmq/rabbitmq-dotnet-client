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
using System.Threading;

namespace RabbitMQ.Client.Events
{
    ///<summary>Contains all the information about a message delivered
    ///from an AMQP broker within the Basic content-class.</summary>
    public class BasicDeliverEventArgs : AsyncEventArgs
    {
        ///<summary>Constructor that fills the event's properties from
        ///its arguments.</summary>
        public BasicDeliverEventArgs(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IReadOnlyBasicProperties properties,
            ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken = default) : base(cancellationToken)
        {
            ConsumerTag = consumerTag;
            DeliveryTag = deliveryTag;
            Redelivered = redelivered;
            Exchange = exchange;
            RoutingKey = routingKey;
            BasicProperties = properties;
            Body = body;
        }

        ///<summary>The content header of the message.</summary>
        public readonly IReadOnlyBasicProperties BasicProperties;

        ///<summary>
        /// <para>
        ///   The message body as a sequence of bytes.
        /// </para>
        /// <para>
        ///   NOTE: Using this memory outside of
        ///   <c><seealso cref="AsyncEventingBasicConsumer.ReceivedAsync"/></c>
        ///   requires that it be copied!
        ///   <example>
        ///   This shows how to copy the data for use:
        ///   <code>
        ///   byte[] bodyCopy = eventArgs.Body.ToArray();
        ///   // bodyCopy is now safe to use elsewhere
        ///   </code>
        ///   </example>
        /// </para>
        ///</summary>
        public readonly ReadOnlyMemory<byte> Body;

        ///<summary>The consumer tag of the consumer that the message
        ///was delivered to.</summary>
        public readonly string ConsumerTag;

        ///<summary>The delivery tag for this delivery. See
        ///IChannel.BasicAck.</summary>
        public readonly ulong DeliveryTag;

        ///<summary>The exchange the message was originally published
        ///to.</summary>
        public readonly string Exchange;

        ///<summary>The AMQP "redelivered" flag.</summary>
        public readonly bool Redelivered;

        ///<summary>The routing key used when the message was
        ///originally published.</summary>
        public readonly string RoutingKey;
    }
}
