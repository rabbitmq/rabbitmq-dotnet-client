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
using System.Buffers;

namespace RabbitMQ.Client
{
    /// <summary>Represents Basic.GetOk responses from the server.</summary>
    /// <remarks>
    /// Basic.Get either returns an instance of this class, or null if a Basic.GetEmpty was received.
    /// </remarks>
    public sealed class BasicGetResult : IDisposable
    {
        private readonly byte[] _rentedArray;

        /// <summary>
        /// Sets the new instance's properties from the arguments passed in.
        /// </summary>
        /// <param name="deliveryTag">Delivery tag for the message.</param>
        /// <param name="redelivered">Redelivered flag for the message</param>
        /// <param name="exchange">The exchange this message was published to.</param>
        /// <param name="routingKey">Routing key with which the message was published.</param>
        /// <param name="messageCount">The number of messages pending on the queue, excluding the message being delivered.</param>
        /// <param name="basicProperties">The Basic-class content header properties for the message.</param>
        /// <param name="body">The body</param>
        public BasicGetResult(ulong deliveryTag, bool redelivered, string exchange, string routingKey,
            uint messageCount, IBasicProperties basicProperties, ReadOnlyMemory<byte> body)
        {
            DeliveryTag = deliveryTag;
            Redelivered = redelivered;
            Exchange = exchange;
            RoutingKey = routingKey;
            MessageCount = messageCount;
            BasicProperties = basicProperties;
            Body = body;
        }

        /// <summary>
        /// Sets the new instance's properties from the arguments passed in.
        /// </summary>
        /// <param name="deliveryTag">Delivery tag for the message.</param>
        /// <param name="redelivered">Redelivered flag for the message</param>
        /// <param name="exchange">The exchange this message was published to.</param>
        /// <param name="routingKey">Routing key with which the message was published.</param>
        /// <param name="messageCount">The number of messages pending on the queue, excluding the message being delivered.</param>
        /// <param name="basicProperties">The Basic-class content header properties for the message.</param>
        /// <param name="body">The body</param>
        /// <param name="rentedArray">The rented array which body is part of.</param>
        public BasicGetResult(ulong deliveryTag, bool redelivered, string exchange, string routingKey,
            uint messageCount, IBasicProperties basicProperties, ReadOnlyMemory<byte> body, byte[] rentedArray)
        {
            DeliveryTag = deliveryTag;
            Redelivered = redelivered;
            Exchange = exchange;
            RoutingKey = routingKey;
            MessageCount = messageCount;
            BasicProperties = basicProperties;
            Body = body;
            _rentedArray = rentedArray;
        }

        /// <summary>
        /// Retrieves the Basic-class content header properties for this message.
        /// </summary>
        public IBasicProperties BasicProperties { get; }

        /// <summary>
        /// Retrieves the body of this message.
        /// </summary>
        public ReadOnlyMemory<byte> Body { get; }

        /// <summary>
        /// Retrieve the delivery tag for this message. See also <see cref="IModel.BasicAck"/>.
        /// </summary>
        public ulong DeliveryTag { get; }

        /// <summary>
        /// Retrieve the exchange this message was published to.
        /// </summary>
        public string Exchange { get; }

        /// <summary>
        /// Retrieve the number of messages pending on the queue, excluding the message being delivered.
        /// </summary>
        /// <remarks>
        /// Note that this figure is indicative, not reliable, and can
        /// change arbitrarily as messages are added to the queue and removed by other clients.
        /// </remarks>
        public uint MessageCount { get; }

        /// <summary>
        /// Retrieve the redelivered flag for this message.
        /// </summary>
        public bool Redelivered { get; }

        /// <summary>
        /// Retrieve the routing key with which this message was published.
        /// </summary>
        public string RoutingKey { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            if (!(_rentedArray is null))
            {
                ArrayPool<byte>.Shared.Return(_rentedArray);
            }

            (BasicProperties as Impl.BasicProperties)?.TryCleanup();
        }
    }
}
