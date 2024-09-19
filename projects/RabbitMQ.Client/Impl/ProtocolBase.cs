// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
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
// The MPL v2.0:
//
//---------------------------------------------------------------------------
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
//  Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing
{
    internal abstract class ProtocolBase : IProtocol
    {
        public Dictionary<string, object> Capabilities;

        protected ProtocolBase()
        {
            Capabilities = new Dictionary<string, object>(6)
            {
                ["publisher_confirms"] = WireFormatting.TrueBoolean,
                ["exchange_exchange_bindings"] = WireFormatting.TrueBoolean,
                ["basic.nack"] = WireFormatting.TrueBoolean,
                ["consumer_cancel_notify"] = WireFormatting.TrueBoolean,
                ["connection.blocked"] = WireFormatting.TrueBoolean,
                ["authentication_failure_close"] = WireFormatting.TrueBoolean
            };
        }

        public abstract string ApiName { get; }
        public abstract int DefaultPort { get; }

        public abstract int MajorVersion { get; }
        public abstract int MinorVersion { get; }
        public abstract int Revision { get; }

        public AmqpVersion Version
        {
            get { return new AmqpVersion(MajorVersion, MinorVersion); }
        }

        internal abstract ProtocolCommandId DecodeCommandIdFrom(ReadOnlySpan<byte> span);

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return GetType() == obj.GetType();
        }

        public override int GetHashCode()
        {
            return GetType().GetHashCode();
        }

        public override string ToString()
        {
            return Version.ToString();
        }
    }
}
