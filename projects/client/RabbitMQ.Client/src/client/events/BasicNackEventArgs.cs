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
    ///<summary>Contains all the information about a message nack'd
    ///from an AMQP broker within the Basic content-class.</summary>
    public class BasicNackEventArgs : EventArgs
    {
        private ulong m_deliveryTag;
        private bool m_multiple;
        private bool m_requeue;

        ///<summary>Default constructor.</summary>
        public BasicNackEventArgs() { }

        ///<summary>The sequence number of the nack'd message, or the
        ///closed upper bound of nack'd messages if multiple is
        ///true.</summary>
        public ulong DeliveryTag
        {
            get { return m_deliveryTag; }
            set { m_deliveryTag = value; }
        }

        ///<summary>Whether this nack applies to one message or
        ///multiple messages.</summary>
        public bool Multiple
        {
            get { return m_multiple; }
            set { m_multiple = value; }
        }

        ///<summary>Ignore</summary>
        ///<remarks>Clients should ignore this field.</remarks>
        public bool Requeue
        {
            get { return m_requeue; }
            set { m_requeue = value; }
        }
    }
}
