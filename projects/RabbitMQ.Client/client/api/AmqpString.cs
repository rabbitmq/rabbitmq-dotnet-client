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
    // TODO lazy string value
    public abstract class AmqpString : IEquatable<AmqpString>, IComparable<AmqpString>
    {
        private readonly string _value;
        private readonly ReadOnlyMemory<byte> _stringBytes;
        private readonly int _byteCount;

        protected AmqpString()
        {
            _value = string.Empty;
            _stringBytes = ReadOnlyMemory<byte>.Empty;
        }

        public AmqpString(string value, ushort maxLen, Encoding encoding)
            : this(value, maxLen, encoding, null)
        {
        }

        public AmqpString(string value, ushort maxLen, Encoding encoding, string validatorRegex)
        {
            if (value.Length > maxLen)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (false == string.IsNullOrWhiteSpace(validatorRegex))
            {
                var re = new Regex(validatorRegex);
                if (false == re.IsMatch(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
            }

            if (encoding == Encoding.ASCII)
            {
                if (false == isAscii(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
            }

            _value = value;
            _stringBytes = new ReadOnlyMemory<byte>(encoding.GetBytes(value));
            _byteCount = _stringBytes.Length;
        }

        public int ByteCount => _byteCount;

        public bool IsEmpty
        {
            get
            {
                return _value == string.Empty;
            }
        }

        public bool Contains(string value)
        {
            return _value.Contains(value);
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

        public static implicit operator ReadOnlySpan<byte>(AmqpString amqpString)
        {
            return amqpString._stringBytes.Span;
        }

        private bool isAscii(string value)
        {
            return Encoding.UTF8.GetByteCount(value) == value.Length;
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

            AmqpString amqpStringObj = obj as AmqpString;
            if (amqpStringObj is null)
            {
                return false;
            }

            return Equals(amqpStringObj);
        }

        public bool Equals(AmqpString other)
        {
            return _value == other._value;
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public int CompareTo(AmqpString other)
        {
            if (_value is null)
            {
                throw new InvalidOperationException("[CRITICAL] should not see this");
            }
            else
            {
                return _value.CompareTo(other._value);
            }
        }

        public static bool operator ==(AmqpString amqpString1, AmqpString amqpString2)
        {
            if (amqpString1 is null || amqpString2 is null)
            {
                return Object.Equals(amqpString1, amqpString2);
            }

            return amqpString1.Equals(amqpString2);
        }

        public static bool operator !=(AmqpString amqpString1, AmqpString amqpString2)
        {
            if (amqpString1 is null || amqpString2 is null)
            {
                return false == Object.Equals(amqpString1, amqpString2);
            }

            return false == amqpString1.Equals(amqpString2);
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
        public static readonly ExchangeName Empty = new ExchangeName();

        private ExchangeName() : base()
        {
        }

        public ExchangeName(string exchangeName)
            : base(exchangeName, 127, Encoding.ASCII, "^[a-zA-Z0-9-_.:]*$")
        {
        }

        // TODO explicit
        public static implicit operator ExchangeName(string value)
        {
            return new ExchangeName(value);
        }
    }

    /*
     * From the spec:
     *  <domain name="queue-name" type="shortstr" label="queue name">
     *    <doc> The queue name identifies the queue within the vhost. In methods where the queue name may be blank, and that has no specific significance, this refers to the 'current' queue for the channel, meaning the last queue that the client declared on the channel. If the client did not declare a queue, and the method needs a queue name, this will result in a 502 (syntax error) channel exception. </doc>
     *    <assert check="length" value="127"/>
     *    <assert check="regexp" value="^[a-zA-Z0-9-_.:]*$"/>
     *  </domain>
     */
    public class QueueName : AmqpString
    {
        public static readonly QueueName Empty = new QueueName();

        private QueueName() : base()
        {
        }

        public QueueName(string queueName)
            : base(queueName, 127, Encoding.ASCII, "^[a-zA-Z0-9-_.:]*$")
        {
        }

        // TODO explicit
        public static implicit operator QueueName(string value)
        {
            return new QueueName(value);
        }

        public static explicit operator RoutingKey(QueueName value)
        {
            return new RoutingKey(value);
        }
    }

    /*
     * From the spec:
     *  <field name="routing-key" domain="shortstr" label="Message routing key">
     *    <doc> Specifies the routing key for the message. The routing key is used for routing messages depending on the exchange configuration. </doc>
     *  </field>
     *  <domain name = "shortstr" type="shortstr" label="short string (max. 256 characters)"/>
     */
    public class RoutingKey : AmqpString
    {
        public static readonly RoutingKey Empty = new RoutingKey();

        private RoutingKey() : base()
        {
        }

        public RoutingKey(string exchangeName)
            : base(exchangeName, 256, Encoding.ASCII)
        {
        }

        // TODO explicit
        public static implicit operator RoutingKey(string value)
        {
            return new RoutingKey(value);
        }
    }

    /*
     * From the spec:
     *  <domain name="consumer-tag" type="shortstr" label="consumer tag">
     *    <doc> Identifier for the consumer, valid within the current channel. </doc>
     *  </domain>
     */
    public class ConsumerTag : AmqpString
    {
        public static readonly ConsumerTag Empty = new ConsumerTag();

        private ConsumerTag() : base()
        {
        }

        public ConsumerTag(string exchangeName)
            : base(exchangeName, 256, Encoding.ASCII)
        {
        }

        // TODO explicit
        public static implicit operator ConsumerTag(string value)
        {
            return new ConsumerTag(value);
        }
    }
}
