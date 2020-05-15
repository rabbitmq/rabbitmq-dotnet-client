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
using System.Linq;
using System.Threading.Tasks;

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;

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
    public class DefaultBasicConsumer : IAsyncBasicConsumer
    {
        private readonly HashSet<string> _consumerTags = new HashSet<string>();

        /// <summary>
        /// Creates a new instance of an <see cref="DefaultBasicConsumer"/>.
        /// </summary>
        public DefaultBasicConsumer()
        {
            ShutdownReason = null;
            Model = null;
            IsRunning = false;
        }

        /// <summary>
        /// Constructor which sets the Model property to the given value.
        /// </summary>
        /// <param name="model">Common AMQP model.</param>
        public DefaultBasicConsumer(IModel model)
        {
            ShutdownReason = null;
            IsRunning = false;
            Model = model;
        }

        /// <summary>
        /// Retrieve the consumer tags this consumer is registered as; to be used to identify
        /// this consumer, for example, when cancelling it with <see cref="IModel.BasicCancel"/>.
        /// This value is an array because a single consumer instance can be reused to consume on
        /// multiple channels.
        /// </summary>
        public string[] ConsumerTags => _consumerTags.ToArray();

        /// <summary>
        /// Returns true while the consumer is registered and expecting deliveries from the broker.
        /// </summary>
        public bool IsRunning { get; protected set; }

        /// <summary>
        /// If our <see cref="IModel"/> shuts down, this property will contain a description of the reason for the
        /// shutdown. Otherwise it will contain null. See <see cref="ShutdownEventArgs"/>.
        /// </summary>
        public ShutdownEventArgs ShutdownReason { get; protected set; }

        /// <summary>
        /// Signalled when the consumer gets cancelled.
        /// </summary>
        public event AsyncEventHandler<ConsumerEventArgs> ConsumerCancelled;

        /// <summary>
        /// Retrieve the <see cref="IModel"/> this consumer is associated with,
        ///  for use in acknowledging received messages, for instance.
        /// </summary>
        public IModel Model { get; set; }

        /// <summary>
        ///  Called when the consumer is cancelled for reasons other than by a basicCancel:
        ///  e.g. the queue has been deleted (either by this channel or  by any other channel).
        ///  See <see cref="HandleBasicCancelOk"/> for notification of consumer cancellation due to basicCancel
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        public virtual ValueTask HandleBasicCancel(string consumerTag)
        {
            return OnCancel(consumerTag);
        }

        /// <summary>
        /// Called upon successful deregistration of the consumer from the broker.
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        public virtual ValueTask HandleBasicCancelOk(string consumerTag)
        {
            return OnCancel(consumerTag);
        }

        /// <summary>
        /// Called upon successful registration of the consumer with the broker.
        /// </summary>
        /// <param name="consumerTag">Consumer tag this consumer is registered.</param>
        public virtual ValueTask HandleBasicConsumeOk(string consumerTag)
        {
            _consumerTags.Add(consumerTag);
            IsRunning = true;
            return default;
        }

        /// <summary>
        /// Called each time a message is delivered for this consumer.
        /// </summary>
        /// <remarks>
        /// This is a no-op implementation. It will not acknowledge deliveries via <see cref="IModel.BasicAck"/>
        /// if consuming in automatic acknowledgement mode.
        /// Subclasses must copy or fully use delivery body before returning.
        /// Accessing the body at a later point is unsafe as its memory can
        /// be already released.
        /// </remarks>
        public virtual ValueTask HandleBasicDeliver(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties properties,
            ReadOnlyMemory<byte> body)
        {
            return default;
            // Nothing to do here.
        }

        /// <summary>
        /// Called when the model (channel) this consumer was registered on terminates.
        /// </summary>
        /// <param name="model">A channel this consumer was registered on.</param>
        /// <param name="reason">Shutdown context.</param>
        public virtual ValueTask HandleModelShutdown(object model, ShutdownEventArgs reason)
        {
            ShutdownReason = reason;
            return OnCancel(_consumerTags.ToArray());
        }

        /// <summary>
        /// Default implementation - overridable in subclasses.</summary>
        /// <param name="consumerTags">The set of consumer tags that where cancelled</param>
        /// <remarks>
        /// This default implementation simply sets the <see cref="IsRunning"/> 
        /// property to false, and takes no further action.
        /// </remarks>
        public virtual async ValueTask OnCancel(params string[] consumerTags)
        {
            IsRunning = false;
            foreach (AsyncEventHandler<ConsumerEventArgs> h in ConsumerCancelled?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try
                {
                    await h(this, new ConsumerEventArgs(consumerTags)).ConfigureAwait(false);
                }
                catch(Exception e)
                {
                    if (Model is ModelBase modelBase)
                    {
                        modelBase.OnCallbackException(CallbackExceptionEventArgs.Build(e));
                    }
                }
            }

            foreach (string consumerTag in consumerTags)
            {
                _consumerTags.Remove(consumerTag);
            }
        }
    }
}
