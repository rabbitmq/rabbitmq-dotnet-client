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
    public abstract class AmqpString : IEquatable<AmqpString>, IComparable<AmqpString>
    {
        private static readonly Encoding s_encoding = Encoding.UTF8;
        private string _value;
        private readonly ReadOnlyMemory<byte> _stringBytes;

        protected AmqpString()
        {
            _value = string.Empty;
            _stringBytes = ReadOnlyMemory<byte>.Empty;
        }

        public AmqpString(ReadOnlyMemory<byte> stringBytes)
        {
            _value = null;
            _stringBytes = stringBytes;
        }

        public AmqpString(string value, ushort maxLen,
            bool strictValidation = false)
            : this(value, maxLen, null, strictValidation)
        {
        }

        public AmqpString(string value, ushort maxLen, string validatorRegex,
            bool strictValidation = false)
        {
            /*
             * Note:
             * RabbitMQ does hardly any validation for names, only stripping off CR/LF
             * characters if present. There are no other checks.
             */
            if (strictValidation)
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
            }

            _value = FixUp(value);
            _stringBytes = new ReadOnlyMemory<byte>(s_encoding.GetBytes(value));
        }

        public int Length => _stringBytes.Length;

        internal bool HasString => _value != null;

        public bool IsEmpty
        {
            get
            {
                if (_value is null)
                {
                    return _stringBytes.Length == 0;
                }
                else
                {
                    return _value == string.Empty;
                }
            }
        }

        public bool Contains(string value)
        {
            return Value.Contains(value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static implicit operator string(AmqpString amqpString)
        {
            return amqpString.ToString();
        }

        public static implicit operator ReadOnlyMemory<byte>(AmqpString amqpString)
        {
            return amqpString._stringBytes;
        }

        public static implicit operator ReadOnlySpan<byte>(AmqpString amqpString)
        {
            return amqpString._stringBytes.Span;
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
            if (_value == null)
            {
                return _stringBytes.Equals(other._stringBytes);
            }
            else
            {
                return _value.Equals(other._value);
            }
        }

        public override int GetHashCode()
        {
            if (_value == null)
            {
                return _stringBytes.GetHashCode();
            }
            else
            {
                return _value.GetHashCode();
            }
        }

        public int CompareTo(AmqpString other)
        {
            return Value.CompareTo(other.Value);
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

        protected virtual string FixUp(string value)
        {
            return value;
        }

        private string Value
        {
            get
            {
                if (_value == null)
                {
                    _value = s_encoding.GetString(_stringBytes.ToArray());
                }
                return _value;
            }
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
        public static readonly ExchangeName AmqDirect = new ExchangeName("amq.direct");
        public static readonly ExchangeName AmqFanout = new ExchangeName("amq.fanout");
        public static readonly ExchangeName AmqTopic = new ExchangeName("amq.topic");
        public static readonly ExchangeName AmqHeaders = new ExchangeName("amq.headers");

        private ExchangeName() : base()
        {
        }

        public ExchangeName(ReadOnlyMemory<byte> exchangeNameBytes)
            : base(exchangeNameBytes)
        {
        }

        public ExchangeName(string exchangeName)
            : this(exchangeName, false)
        {
        }

        public ExchangeName(string exchangeName, bool strictValidation)
            : base(exchangeName, 127, "^[a-zA-Z0-9-_.:]*$", strictValidation)
        {
        }

        public static explicit operator ExchangeName(string value)
        {
            return new ExchangeName(value);
        }

        protected override string FixUp(string value)
        {
            // Note: this is the only modification RabbitMQ makes
            return value.Replace("\r", string.Empty).Replace("\n", string.Empty);
        }
    }

    /*
     * From the spec:
     *  <domain name="queue-name" type="shortstr" label="queue name">
     *    <doc> The queue name identifies the queue within the vhost.
     *    In methods where the queue name may be blank, and that has no
     *    specific significance, this refers to the 'current' queue for
     *    the channel, meaning the last queue that the client declared
     *    on the channel. If the client did not declare a queue, and the
     *    method needs a queue name, this will result in a 502
     *    (syntax error) channel exception.</doc>
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

        public QueueName(ReadOnlyMemory<byte> queueNameBytes)
            : base(queueNameBytes)
        {
        }

        public QueueName(string queueName)
            : this(queueName, false)
        {
        }

        public QueueName(string queueName, bool strictValidation)
            : base(queueName, 127, "^[a-zA-Z0-9-_.:]*$", strictValidation)
        {
        }

        public static explicit operator QueueName(string value)
        {
            return new QueueName(value);
        }

        public static explicit operator RoutingKey(QueueName value)
        {
            return new RoutingKey((string)value);
        }

        protected override string FixUp(string value)
        {
            // Note: this is the only modification RabbitMQ makes
            return value.Replace("\r", string.Empty).Replace("\n", string.Empty);
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

        public RoutingKey(ReadOnlyMemory<byte> routingKeyBytes)
            : base(routingKeyBytes)
        {
        }

        public RoutingKey(string routingKey)
            : this(routingKey, false)
        {
        }

        public RoutingKey(string routingKey, bool strictValidation)
            : base(routingKey, 256, strictValidation)
        {
        }

        public static explicit operator RoutingKey(string value)
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

        public ConsumerTag(ReadOnlyMemory<byte> consumerTagBytes)
            : base(consumerTagBytes)
        {
        }

        public ConsumerTag(string consumerTag)
            : this(consumerTag, false)
        {
        }

        public ConsumerTag(string consumerTag, bool strictValidation)
            : base(consumerTag, 256, strictValidation)
        {
        }

        public static explicit operator ConsumerTag(string value)
        {
            return new ConsumerTag(value);
        }
    }
}
