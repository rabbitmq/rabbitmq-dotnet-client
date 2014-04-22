// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2014 GoPivotal, Inc.
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
//  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

namespace RabbitMQ.Client
{
    ///<summary>Represents Basic.GetOk responses from the server.</summary>
    ///<remarks>
    /// Basic.Get either returns an instance of this class, or null if
    /// a Basic.GetEmpty was received.
    ///</remarks>
    public class BasicGetResult
    {
        private ulong m_deliveryTag;
        private bool m_redelivered;
        private string m_exchange;
        private string m_routingKey;
        private uint m_messageCount;
        private IBasicProperties m_basicProperties;
        private byte[] m_body;

        ///<summary>Sets the new instance's properties from the
        ///arguments passed in.</summary>
        public BasicGetResult(ulong deliveryTag,
                              bool redelivered,
                              string exchange,
                              string routingKey,
                              uint messageCount,
                              IBasicProperties basicProperties,
                              byte[] body)
        {
            m_deliveryTag = deliveryTag;
            m_redelivered = redelivered;
            m_exchange = exchange;
            m_routingKey = routingKey;
            m_messageCount = messageCount;
            m_basicProperties = basicProperties;
            m_body = body;
        }

        ///<summary>Retrieve the delivery tag for this message. See also IModel.BasicAck.</summary>
        public ulong DeliveryTag { get { return m_deliveryTag; } }
        ///<summary>Retrieve the redelivered flag for this message.</summary>
        public bool Redelivered { get { return m_redelivered; } }
        ///<summary>Retrieve the exchange this message was published to.</summary>
        public string Exchange { get { return m_exchange; } }
        ///<summary>Retrieve the routing key with which this message was published.</summary>
        public string RoutingKey { get { return m_routingKey; } }

        ///<summary>Retrieve the number of messages pending on the
        ///queue, excluding the message being delivered.</summary>
        ///<remarks>
        /// Note that this figure is indicative, not reliable, and can
        /// change arbitrarily as messages are added to the queue and
        /// removed by other clients.
        ///</remarks>
        public uint MessageCount { get { return m_messageCount; } }

        ///<summary>Retrieves the Basic-class content header properties for this message.</summary>
        public IBasicProperties BasicProperties { get { return m_basicProperties; } }
        ///<summary>Retrieves the body of this message.</summary>
        public byte[] Body { get { return m_body; } }
    }
}
