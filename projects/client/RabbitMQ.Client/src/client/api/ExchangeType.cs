// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2011 VMware, Inc.
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
//  Copyright (c) 2007-2011 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System.Collections;

namespace RabbitMQ.Client
{
    ///<summary>
    /// Convenience class providing compile-time names for standard exchange types.
    ///</summary>
    ///<remarks>
    /// Use the static members of this class as values for the
    /// "exchangeType" arguments for IModel methods such as
    /// ExchangeDeclare. The broker may be extended with additional
    /// exchange types that do not appear in this class.
    ///</remarks>
    public class ExchangeType
    {
        ///<summary>Exchange type used for AMQP fanout exchanges.</summary>
        public const string Fanout = "fanout";
        ///<summary>Exchange type used for AMQP direct exchanges.</summary>
        public const string Direct = "direct";
        ///<summary>Exchange type used for AMQP topic exchanges.</summary>
        public const string Topic = "topic";
        ///<summary>Exchange type used for AMQP headers exchanges.</summary>
        public const string Headers = "headers";

        ///<summary>Private constructor - this class has no instances</summary>
        private ExchangeType() {}

        ///<summary>Retrieve a collection containing all standard exchange types.</summary>
        public static ICollection All()
        {
            return new string[] {
                Fanout,
                Direct,
                Topic,
                Headers
            };
        }
    }
}
