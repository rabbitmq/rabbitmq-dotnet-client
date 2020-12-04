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
using System.Linq;
using RabbitMQ.Client.client.impl.Channel;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Useful default/base implementation of <see cref="IBasicConsumer"/>.
    /// Subclass and override <see cref="HandleBasicDeliver"/> in application code.
    /// </summary>
    /// <remarks>
    /// Note that the "Handle*" methods run in the connection's thread!
    /// Consider using <see cref="EventingBasicConsumer"/>,  which exposes
    /// events that can be subscribed to consumer messages.
    /// </remarks>
    public class DefaultBasicConsumer : IBasicConsumer
    {
        private readonly HashSet<string> _consumerTags = new HashSet<string>();

        /// <summary>
        /// Creates a new instance of an <see cref="DefaultBasicConsumer"/>.
        /// </summary>
        public DefaultBasicConsumer()
        {
            ShutdownReason = null;
            Channel = null;
            IsRunning = false;
        }

        /// <summary>
        /// Constructor which sets the Channel property to the given value.
        /// </summary>
        /// <param name="channel">The channel.</param>
        public DefaultBasicConsumer(IChannel channel)
        {
            ShutdownReason = null;
            IsRunning = false;
            Channel = channel;
        }

        /// <summary>
        /// Retrieve the consumer tags this consumer is registered as; to be used to identify
        /// this consumer, for example, when cancelling it with <see cref="IChannel.CancelConsumerAsync"/>.
        /// This value is an array because a single consumer instance can be reused to consume on
        /// multiple channels.
        /// </summary>
        public string[] ConsumerTags
        {
            get
            {
                return _consumerTags.ToArray();
            }
        }

        /// <summary>
        /// Returns true while the consumer is registered and expecting deliveries from the broker.
        /// </summary>
        public bool IsRunning { get; protected set; }

        /// <summary>
        /// If our <see cref="IChannel"/> shuts down, this property will contain a description of the reason for the
        /// shutdown. Otherwise it will contain null. See <see cref="ShutdownEventArgs"/>.
        /// </summary>
        public ShutdownEventArgs ShutdownReason { get; protected set; }

        /// <summary>
        /// Signalled when the consumer gets cancelled.
        /// </summary>
        public event EventHandler<ConsumerEventArgs> ConsumerCancelled;

        /// <summary>
        /// Retrieve the <see cref="IChannel"/> this consumer is associated with,
        ///  for use in acknowledging received messages, for instance.
        /// </summary>
        public IChannel Channel { get; set; }

        /// <summary>
        ///  Called when the consumer is cancelled for reasons other than by a basicCancel:
        ///  e.g. the queue has been deleted (either by this channel or  by any other channel).
        ///  See <see cref="HandleBasicCancelOk"/> for notification of consumer cancellation due to basicCancel
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        public virtual void HandleBasicCancel(string consumerTag)
        {
            OnCancel(consumerTag);
        }

        /// <summary>
        /// Called upon successful deregistration of the consumer from the broker.
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        public virtual void HandleBasicCancelOk(string consumerTag)
        {
            OnCancel(consumerTag);
        }

        /// <summary>
        /// Called upon successful registration of the consumer with the broker.
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        public virtual void HandleBasicConsumeOk(string consumerTag)
        {
            _consumerTags.Add(consumerTag);
            IsRunning = true;
        }

        /// <summary>
        /// Called each time a message is delivered for this consumer.
        /// </summary>
        /// <remarks>
        /// This is a no-op implementation. It will not acknowledge deliveries via <see cref="IChannel.AckMessageAsync"/>
        /// if consuming in automatic acknowledgement mode.
        /// Subclasses must copy or fully use delivery body before returning.
        /// Accessing the body at a later point is unsafe as its memory can
        /// be already released.
        /// </remarks>
        public virtual void HandleBasicDeliver(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties properties,
            ReadOnlyMemory<byte> body)
        {
            // Nothing to do here.
        }

        /// <summary>
        /// Called when the channel this consumer was registered on terminates.
        /// </summary>
        /// <param name="model">A channel this consumer was registered on.</param>
        /// <param name="reason">Shutdown context.</param>
        public virtual void HandleModelShutdown(object model, ShutdownEventArgs reason)
        {
            ShutdownReason = reason;
            OnCancel(_consumerTags.ToArray());
        }

        /// <summary>
        /// Default implementation - overridable in subclasses.</summary>
        /// <param name="consumerTags">The set of consumer tags that where cancelled</param>
        /// <remarks>
        /// This default implementation simply sets the <see cref="IsRunning"/> 
        /// property to false, and takes no further action.
        /// </remarks>
        public virtual void OnCancel(params string[] consumerTags)
        {
            IsRunning = false;
            foreach (EventHandler<ConsumerEventArgs> h in ConsumerCancelled?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                h(this, new ConsumerEventArgs(consumerTags));
            }

            foreach (string consumerTag in consumerTags)
            {
                _consumerTags.Remove(consumerTag);
            }
        }
    }
}
