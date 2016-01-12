// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2016 Pivotal Software, Inc.
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
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Threading.Tasks;

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
            Received = (s, e) => _doneTask;
            Registered = (s, e) => _doneTask;
            Shutdown = (s, e) => _doneTask;
            Unregistered = (s, e) => _doneTask;
        }

        ///<summary>Event fired on HandleBasicDeliver.</summary>
        public event AsyncEventHandler<BasicDeliverEventArgs> Received;

        ///<summary>Event fired on HandleBasicConsumeOk.</summary>
        public event AsyncEventHandler<ConsumerEventArgs> Registered;

        ///<summary>Event fired on HandleModelShutdown.</summary>
        public event AsyncEventHandler<ShutdownEventArgs> Shutdown;

        ///<summary>Event fired on HandleBasicCancelOk.</summary>
        public event AsyncEventHandler<ConsumerEventArgs> Unregistered;

        ///<summary>Fires the Unregistered event.</summary>
        public override async Task HandleBasicCancelOk(string consumerTag)
        {
            await base.HandleBasicCancelOk(consumerTag).ConfigureAwait(false);

            await Unregistered(this, new ConsumerEventArgs(consumerTag)).ConfigureAwait(false);
        }

        ///<summary>Fires the Registered event.</summary>
        public override async Task HandleBasicConsumeOk(string consumerTag)
        {
            await base.HandleBasicConsumeOk(consumerTag).ConfigureAwait(false);

            await Registered(this, new ConsumerEventArgs(consumerTag)).ConfigureAwait(false);
        }

        ///<summary>Fires the Received event.</summary>
        public override async Task HandleBasicDeliver(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties properties,
            byte[] body)
        {
            await base.HandleBasicDeliver(consumerTag,
                deliveryTag,
                redelivered,
                exchange,
                routingKey,
                properties,
                body).ConfigureAwait(false);
            await Received(this, new BasicDeliverEventArgs(consumerTag,
                deliveryTag,
                redelivered,
                exchange,
                routingKey,
                properties,
                body)).ConfigureAwait(false);
        }

        ///<summary>Fires the Shutdown event.</summary>
        public override async Task HandleModelShutdown(object model, ShutdownEventArgs reason)
        {
            await base.HandleModelShutdown(model, reason).ConfigureAwait(false);

            await Shutdown(this, reason).ConfigureAwait(false);
        }
    }
}
