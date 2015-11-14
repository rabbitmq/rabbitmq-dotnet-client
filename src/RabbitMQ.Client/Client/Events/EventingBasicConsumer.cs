// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2015 Pivotal Software, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
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
//  at http://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;

namespace RabbitMQ.Client.Events
{
    ///<summary>Experimental class exposing an IBasicConsumer's
    ///methods as separate events.</summary>
    public class EventingBasicConsumer : DefaultBasicConsumer
    {
        ///<summary>Constructor which sets the Model property to the
        ///given value.</summary>
        public EventingBasicConsumer(IModel model) : base(model)
        {
        }

        ///<summary>Event fired on HandleBasicDeliver.</summary>
        public event EventHandler<BasicDeliverEventArgs> Received;

        ///<summary>Event fired on HandleBasicConsumeOk.</summary>
        public event EventHandler<ConsumerEventArgs> Registered;

        ///<summary>Event fired on HandleModelShutdown.</summary>
        public event EventHandler<ShutdownEventArgs> Shutdown;

        ///<summary>Event fired on HandleBasicCancelOk.</summary>
        public event EventHandler<ConsumerEventArgs> Unregistered;

        ///<summary>Fires the Unregistered event.</summary>
        public override void HandleBasicCancelOk(string consumerTag)
        {
            base.HandleBasicCancelOk(consumerTag);

            if (Unregistered != null)
            {
                Unregistered(this, new ConsumerEventArgs(consumerTag));
            }
        }

        ///<summary>Fires the Registered event.</summary>
        public override void HandleBasicConsumeOk(string consumerTag)
        {
            base.HandleBasicConsumeOk(consumerTag);

            if (Registered != null)
            {
                Registered(this, new ConsumerEventArgs(consumerTag));
            }
        }

        ///<summary>Fires the Received event.</summary>
        public override void HandleBasicDeliver(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties properties,
            byte[] body)
        {
            base.HandleBasicDeliver(consumerTag,
                deliveryTag,
                redelivered,
                exchange,
                routingKey,
                properties,
                body);
            if (Received != null)
            {
                Received(this, new BasicDeliverEventArgs(consumerTag,
                    deliveryTag,
                    redelivered,
                    exchange,
                    routingKey,
                    properties,
                    body));
            }
        }

        ///<summary>Fires the Shutdown event.</summary>
        public override void HandleModelShutdown(object model, ShutdownEventArgs reason)
        {
            base.HandleModelShutdown(model, reason);

            if (Shutdown != null)
            {
                Shutdown(this, reason);
            }
        }
    }
}
