// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2020 VMware, Inc.
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
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Convenience class providing compile-time names for standard exchange types.
    /// </summary>
    /// <remarks>
    /// Use the static members of this class as values for the
    /// "exchangeType" arguments for IChannel methods such as
    /// ExchangeDeclare. The broker may be extended with additional
    /// exchange types that do not appear in this class.
    /// </remarks>
    public class ExchangeType : IEquatable<ExchangeType>
    {
        private const string EmptyStr = "";
        private const string FanoutStr = "fanout";
        private const string DirectStr = "direct";
        private const string TopicStr = "topic";
        private const string HeadersStr = "headers";

        private static readonly string[] s_all = { EmptyStr, FanoutStr, DirectStr, TopicStr, HeadersStr };

        private readonly string _value;

        internal static readonly ExchangeType s_empty = new ExchangeType(string.Empty);

        /// <summary>
        /// Exchange type used for AMQP direct exchanges.
        /// </summary>
        public static readonly ExchangeType Direct = new ExchangeType(DirectStr);

        /// <summary>
        /// Exchange type used for AMQP fanout exchanges.
        /// </summary>
        public static readonly ExchangeType Fanout = new ExchangeType(FanoutStr);

        /// <summary>
        /// Exchange type used for AMQP headers exchanges.
        /// </summary>
        public static readonly ExchangeType Headers = new ExchangeType(HeadersStr);

        /// <summary>
        /// Exchange type used for AMQP topic exchanges.
        /// </summary>
        public static readonly ExchangeType Topic = new ExchangeType(TopicStr);

        internal ExchangeType(string value)
        {
            if (false == s_all.Contains(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            _value = value;
        }

        public override string ToString()
        {
            return _value;
        }

        public static implicit operator ExchangeType(string value)
        {
            return new ExchangeType(value);
        }

        public static implicit operator string(ExchangeType exchangeType)
        {
            return exchangeType._value;
        }

        internal int ByteCount
        {
            get
            {
                return _value.Length;
            }
        }

        /// <summary>
        /// Retrieve a collection containing all standard exchange types.
        /// </summary>
        public static ICollection<string> All()
        {
            return s_all;
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (Object.ReferenceEquals(this, obj))
            {
                return true;
            }

            ExchangeType exchangeTypeObj = obj as ExchangeType;
            if (exchangeTypeObj is null)
            {
                return false;
            }

            return Equals(exchangeTypeObj);
        }

        public bool Equals(ExchangeType other)
        {
            return _value == other._value;
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public static bool operator ==(ExchangeType exchangeType1, ExchangeType exchangeType2)
        {
            if (exchangeType1 is null || exchangeType2 is null)
            {
                return Object.Equals(exchangeType1, exchangeType2);
            }

            return exchangeType1.Equals(exchangeType2);
        }

        public static bool operator !=(ExchangeType exchangeType1, ExchangeType exchangeType2)
        {
            if (exchangeType1 is null || exchangeType2 is null)
            {
                return false == Object.Equals(exchangeType1, exchangeType2);
            }

            return false == exchangeType1.Equals(exchangeType2);
        }
    }
}
