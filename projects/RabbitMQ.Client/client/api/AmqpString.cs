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
//  Copyright (c) 2011-2020 VMware, Inc. or its affiliates.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Text;
using System.Text.RegularExpressions;

namespace RabbitMQ.Client
{
    public abstract class AmqpString
    {
        private readonly string _value;
        private readonly ReadOnlyMemory<byte> _stringBytes;

        public AmqpString(string value, ushort maxLen, Encoding validEncoding, string validatorRegex)
        {
            if (value.Length > maxLen)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            var re = new Regex(validatorRegex);
            if (false == re.IsMatch(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _value = value;
            _stringBytes = new ReadOnlyMemory<byte>(validEncoding.GetBytes(value));
        }

        public override string ToString()
        {
            return _value;
        }

        public static implicit operator string(AmqpString amqpString)
        {
            return amqpString._value;
        }

        public static implicit operator ReadOnlyMemory<byte>(AmqpString amqpString)
        {
            return amqpString._stringBytes;
        }
    }

    /*
     * From the spec:
     *  <domain name="exchange-name" type="shortstr" label="exchange name">
     *    <doc> The exchange name is a client-selected string that identifies the exchange for publish methods. </doc>
     *    <assert check="length" value="127"/>
     *    <assert check="regexp" value="^[a-zA-Z0-9-_.:]*$"/>
     *  </domain>
     */
    public class ExchangeName : AmqpString
    {
        public ExchangeName(string exchangeName)
            : base(exchangeName, 127, Encoding.ASCII, "^[a-zA-Z0-9-_.:]*$")
        {
        }

        public static explicit operator ExchangeName(string value)
        {
            return new ExchangeName(value);
        }
    }
}
