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
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Consumer interface. Used to receive messages from a queue by subscription.
    /// </summary>
    public interface IAsyncBasicConsumer
    {
        /// <summary>
        /// Retrieve the <see cref="IChannel"/> this consumer is associated with,
        /// for use in acknowledging received messages, for instance.
        /// </summary>
        IChannel? Channel { get; }

        /// <summary>
        /// Called when the consumer is cancelled for reasons other than by a basicCancel:
        /// e.g. the queue has been deleted (either by this channel or by any other channel).
        /// See <see cref="HandleBasicCancelOkAsync"/> for notification of consumer cancellation due to basicCancel
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task HandleBasicCancelAsync(string consumerTag, CancellationToken cancellationToken = default);

        /// <summary>
        /// Called upon successful deregistration of the consumer from the broker.
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task HandleBasicCancelOkAsync(string consumerTag, CancellationToken cancellationToken = default);

        /// <summary>
        /// Called upon successful registration of the consumer with the broker.
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task HandleBasicConsumeOkAsync(string consumerTag, CancellationToken cancellationToken = default);

        /// <summary>
        /// Called each time a message arrives for this consumer.
        /// <remarks>
        ///  <para>
        ///   Does nothing with the passed in information.
        ///   Note that in particular, some delivered messages may require acknowledgement via <see cref="IChannel.BasicAckAsync"/>.
        ///   The implementation of this method in this class does NOT acknowledge such messages.
        ///  </para>
        ///  <para>
        ///    NOTE: Using the <c>body</c> outside of
        ///    <c><seealso cref="IAsyncBasicConsumer.HandleBasicDeliverAsync(string, ulong, bool, string, string, IReadOnlyBasicProperties, ReadOnlyMemory{byte}, CancellationToken)"/></c>
        ///    requires that it be copied!
        ///  </para>
        /// </remarks>
        ///</summary>
        Task HandleBasicDeliverAsync(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IReadOnlyBasicProperties properties,
            ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Called when the channel shuts down.
        /// </summary>
        /// <param name="channel">Common AMQP channel.</param>
        /// <param name="reason">Information about the reason why a particular channel, session, or connection was destroyed.</param>
        Task HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason);
    }
}
