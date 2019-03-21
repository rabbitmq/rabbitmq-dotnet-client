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
//       https://www.apache.org/licenses/LICENSE-2.0
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

namespace RabbitMQ.Client
{
    // time representations in mainstream languages: the horror, the horror
    // see in particular the difference between .NET 1.x and .NET 2.0's versions of DateTime

    /// <summary>
    /// Structure holding an AMQP timestamp, a posix 64-bit time_t.</summary>
    /// <remarks>
    /// <para>
    /// When converting between an AmqpTimestamp and a System.DateTime,
    /// be aware of the effect of your local timezone. In particular,
    /// different versions of the .NET framework assume different
    /// defaults.
    /// </para>
    /// <para>
    /// We have chosen a signed 64-bit time_t here, since the AMQP
    /// specification through versions 0-9 is silent on whether
    /// timestamps are signed or unsigned.
    /// </para>
    /// </remarks>
    public struct AmqpTimestamp
    {
        /// <summary>
        /// Construct an <see cref="AmqpTimestamp"/>.
        /// </summary>
        /// <param name="unixTime">Unix time.</param>
        public AmqpTimestamp(long unixTime) : this()
        {
            UnixTime = unixTime;
        }

        /// <summary>
        /// Unix time.
        /// </summary>
        public long UnixTime { get; private set; }

        /// <summary>
        /// Provides a debugger-friendly display.
        /// </summary>
        public override string ToString()
        {
            return "((time_t)" + UnixTime + ")";
        }
    }
}
