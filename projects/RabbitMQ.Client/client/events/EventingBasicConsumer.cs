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

namespace RabbitMQ.Client.Events
{
    ///<summary>Class exposing an IBasicConsumer's
    ///methods as separate events.</summary>
    public class EventingBasicConsumer : DefaultBasicConsumer
    {
        ///<summary>Constructor which sets the Model property to the
        ///given value.</summary>
        public EventingBasicConsumer(IModel model) : base(model)
        {
        }

        ///<summary>
        /// Event fired when a delivery arrives for the consumer.
        /// </summary>
        /// <remarks>
        /// Handlers must copy or fully use delivery body before returning.
        /// Accessing the body at a later point is unsafe as its memory can
        /// be already released.
        /// </remarks>
        public event EventHandler<BasicDeliverEventArgs> Received;

        ///<summary>Fires when the server confirms successful consumer registration.</summary>
        public event EventHandler<ConsumerEventArgs> Registered;

        ///<summary>Fires on model (channel) shutdown, both client and server initiated.</summary>
        public event EventHandler<ShutdownEventArgs> Shutdown;

        ///<summary>Fires when the server confirms successful consumer cancellation.</summary>
        public event EventHandler<ConsumerEventArgs> Unregistered;

        ///<summary>Fires when the server confirms successful consumer cancellation.</summary>
        public override void HandleBasicCancelOk(string consumerTag)
        {
            base.HandleBasicCancelOk(consumerTag);
            Unregistered?.Invoke(this, new ConsumerEventArgs(new[] { consumerTag }));
        }

        ///<summary>Fires when the server confirms successful consumer cancellation.</summary>
        public override void HandleBasicConsumeOk(string consumerTag)
        {
            base.HandleBasicConsumeOk(consumerTag);
            Registered?.Invoke(this, new ConsumerEventArgs(new[] { consumerTag }));
        }

        ///<summary>
        /// Invoked when a delivery arrives for the consumer.
        /// </summary>
        /// <remarks>
        /// Handlers must copy or fully use delivery body before returning.
        /// Accessing the body at a later point is unsafe as its memory can
        /// be already released.
        /// </remarks>
        public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            base.HandleBasicDeliver(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body);
            Received?.Invoke(
                this,
                new BasicDeliverEventArgs(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body));
        }

        ///<summary>Fires the Shutdown event.</summary>
        public override void HandleModelShutdown(object model, ShutdownEventArgs reason)
        {
            base.HandleModelShutdown(model, reason);
            Shutdown?.Invoke(this, reason);
        }
    }
}
