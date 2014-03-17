// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2013 GoPivotal, Inc.
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

using System;

namespace RabbitMQ.Client.Events
{
    ///<summary>Contains all the information about a message returned
    ///from an AMQP broker within the Basic content-class.</summary>
    public class BasicReturnEventArgs : EventArgs
    {
        private ushort m_replyCode;
        private string m_replyText;
        private string m_exchange;
        private string m_routingKey;
        private IBasicProperties m_basicProperties;
        private byte[] m_body;

        ///<summary>Default constructor.</summary>
        public BasicReturnEventArgs() { }

        ///<summary>The AMQP reason code for the return. See
        ///RabbitMQ.Client.Framing.*.Constants.</summary>
        public ushort ReplyCode
        {
            get { return m_replyCode; }
            set { m_replyCode = value; }
        }

        ///<summary>Human-readable text from the broker describing the
        ///reason for the return.</summary>
        public string ReplyText
        {
            get { return m_replyText; }
            set { m_replyText = value; }
        }

        ///<summary>The exchange the returned message was originally
        ///published to.</summary>
        public string Exchange
        {
            get { return m_exchange; }
            set { m_exchange = value; }
        }

        ///<summary>The routing key used when the message was
        ///originally published.</summary>
        public string RoutingKey
        {
            get { return m_routingKey; }
            set { m_routingKey = value; }
        }

        ///<summary>The content header of the message.</summary>
        public IBasicProperties BasicProperties
        {
            get { return m_basicProperties; }
            set { m_basicProperties = value; }
        }

        ///<summary>The message body.</summary>
        public byte[] Body
        {
            get { return m_body; }
            set { m_body = value; }
        }
    }
}
