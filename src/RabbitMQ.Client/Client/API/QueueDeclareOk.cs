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

namespace RabbitMQ.Client
{
    /// <summary>
    /// Represents Queue info.
    /// </summary>
    public class QueueDeclareOk
    {
        /// <summary>
        /// Creates a new instance of the <see cref="QueueDeclareOk"/>.
        /// </summary>
        /// <param name="queueName">Queue name.</param>
        /// <param name="messageCount">Message count.</param>
        /// <param name="consumerCount">Consumer count.</param>
        public QueueDeclareOk(string queueName, uint messageCount, uint consumerCount)
        {
            QueueName = queueName;
            MessageCount = messageCount;
            ConsumerCount = consumerCount;
        }

        /// <summary>
        /// Consumer count.
        /// </summary>
        public uint ConsumerCount { get; private set; }

        /// <summary>
        /// Message count.
        /// </summary>
        public uint MessageCount { get; private set; }

        /// <summary>
        /// Queue name.
        /// </summary>
        public string QueueName { get; private set; }

        public static implicit operator string(QueueDeclareOk declareOk)
        {
            return declareOk.QueueName;
        }
    }
}
