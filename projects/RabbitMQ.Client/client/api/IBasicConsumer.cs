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

using RabbitMQ.Client.Events;

namespace RabbitMQ.Client
{
    /// <summary>Consumer interface. Used to
    ///receive messages from a queue by subscription.</summary>
    /// <remarks>
    /// <para>
    /// See IModel.BasicConsume, IModel.BasicCancel.
    /// </para>
    /// <para>
    /// Note that the "Handle*" methods run in the connection's
    /// thread! Consider using <see cref="EventingBasicConsumer"/>, which uses a
    /// SharedQueue instance to safely pass received messages across
    /// to user threads.
    /// </para>
    /// </remarks>
    public interface IBasicConsumer
    {
        /// <summary>
        /// Retrieve the <see cref="IChannel"/> this consumer is associated with,
        ///  for use in acknowledging received messages, for instance.
        /// </summary>
        IChannel Model { get; }

        /// <summary>
        /// Signalled when the consumer gets cancelled.
        /// </summary>
        event EventHandler<ConsumerEventArgs> ConsumerCancelled;

        /// <summary>
        ///  Called when the consumer is cancelled for reasons other than by a basicCancel:
        ///  e.g. the queue has been deleted (either by this channel or  by any other channel).
        ///  See <see cref="HandleBasicCancelOk"/> for notification of consumer cancellation due to basicCancel
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        void HandleBasicCancel(string consumerTag);

        /// <summary>
        /// Called upon successful deregistration of the consumer from the broker.
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        void HandleBasicCancelOk(string consumerTag);

        /// <summary>
        /// Called upon successful registration of the consumer with the broker.
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        void HandleBasicConsumeOk(string consumerTag);

        /// <summary>
        /// Called each time a message arrives for this consumer.
        /// </summary>
        /// <remarks>
        /// Does nothing with the passed in information.
        /// Note that in particular, some delivered messages may require acknowledgement via <see cref="IChannel.BasicAck"/>.
        /// The implementation of this method in this class does NOT acknowledge such messages.
        /// </remarks>
        void HandleBasicDeliver(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            in ReadOnlyBasicProperties properties,
            ReadOnlyMemory<byte> body);

        /// <summary>
        ///  Called when the channel shuts down.
        ///  </summary>
        ///  <param name="channel"> Common AMQP channel.</param>
        /// <param name="reason"> Information about the reason why a particular channel, session, or connection was destroyed.</param>
        void HandleModelShutdown(object channel, ShutdownEventArgs reason);
    }
}
