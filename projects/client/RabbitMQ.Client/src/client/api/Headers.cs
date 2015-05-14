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

using System.Collections.Generic;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Convenience class providing compile-time names for standard exchange types.
    /// </summary>
    /// <remarks>
    /// Use the static members of this class as headers for the
    /// arguments for Queue, Exchange and Consumer declaration. 
    /// The broker may be extended with additional
    /// headers that do not appear in this class.
    /// </remarks>
    public static class Headers
    {
        /// <summary>
        /// Header used to declare priority queues and set maximum priority of a queue
        /// </summary>
        public const string QueueMaxPriority = "x-max-priority";

        /// <summary>
        /// Header used to limit maximum length of a queue in messages
        /// </summary>
        public const string QueueMaxLengthInMessages = "x-max-length";

        /// <summary>
        /// Header used to limit maximum length of a queue in bytes
        /// </summary>
        public const string QueueMaxLengthInBytes = "x-max-length-bytes";
        
        /// <summary>
        /// Header used to set dead letter exchange for a queue
        /// </summary>
        public const string DeadLetterExchange = "x-dead-letter-exchange";
        
        /// <summary>
        /// Header used to set dead letter routing key for a queue
        /// </summary>
        public const string DeadLetterRoutingKey = "x-dead-letter-routing-key";

        /// <summary>
        /// Header used to set ttl globaly to all messages in a queue
        /// </summary>
        public const string PerQueueMessageTtl = "x-message-ttl";

        /// <summary>
        /// Header used to set expiration for a queue
        /// </summary>
        public const string QueueExpires = "x-expires";


        /// <summary>
        /// Header used to set alternate exchange for an exchange
        /// </summary>
        public const string AlternateExchange = "alternate-exchange";


        /// <summary>
        /// Header used to set priority of consumer
        /// </summary>
        public const string ConsumerPriority = "x-priority";
    }
}