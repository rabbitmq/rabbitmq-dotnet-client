// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2012 VMware, Inc.
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
//  The Initial Developer of the Original Code is VMware, Inc.
//  Copyright (c) 2007-2012 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

namespace RabbitMQ.Client
{
    public class QueueDeclareOk
    {

        private string m_queueName;
        public string QueueName
        {
            get { return m_queueName; }
            private set { m_queueName = value; }
        }

        private uint m_messageCount;
        public uint MessageCount
        {
            get { return m_messageCount; }
            private set { m_messageCount = value; }
        }

        private uint m_consumerCount;
        public uint ConsumerCount
        {
            get { return m_consumerCount; }
            private set { m_consumerCount = value; }
        }

        public QueueDeclareOk(string queueName, uint messageCount, uint consumerCount)
        {
            QueueName = queueName;
            MessageCount = messageCount;
            ConsumerCount = consumerCount;
        }

        public static implicit operator string(QueueDeclareOk declareOk)
        {
            return declareOk.QueueName;
        }
    }
}
