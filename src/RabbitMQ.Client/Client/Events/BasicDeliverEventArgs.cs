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
    ///<summary>Contains all the information about a message delivered
    ///from an AMQP broker within the Basic content-class.</summary>
    public class BasicDeliverEventArgs : EventArgs
    {
        ///<summary>Default constructor.</summary>
        public BasicDeliverEventArgs()
        {
        }

        ///<summary>Constructor that fills the event's properties from
        ///its arguments.</summary>
        public BasicDeliverEventArgs(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties properties,
            byte[] body)
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
        public IBasicProperties BasicProperties { get; set; }

        ///<summary>The message body.</summary>
        public byte[] Body { get; set; }

        ///<summary>The consumer tag of the consumer that the message
        ///was delivered to.</summary>
        public string ConsumerTag { get; set; }

        ///<summary>The delivery tag for this delivery. See
        ///IModel.BasicAck.</summary>
        public ulong DeliveryTag { get; set; }

        ///<summary>The exchange the message was originally published
        ///to.</summary>
        public string Exchange { get; set; }

        ///<summary>The AMQP "redelivered" flag.</summary>
        public bool Redelivered { get; set; }

        ///<summary>The routing key used when the message was
        ///originally published.</summary>
        public string RoutingKey { get; set; }
    }
}
