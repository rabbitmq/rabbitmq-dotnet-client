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

using System.Text.RegularExpressions;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Container for an exchange name, exchange type and
    /// routing key, usable as the target address of a message to be published.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The syntax used for the external representation of instances
    /// of this class is compatible with QPid's "Reply-To" field
    /// pseudo-URI format. The pseudo-URI format is
    /// (exchange-type)://(exchange-name)/(routing-key), where
    /// exchange-type is one of the permitted exchange type names (see
    /// class ExchangeType), exchange-name must be present but may be
    /// empty, and routing-key must be present but may be empty.
    /// </para>
    /// <para>
    /// The syntax is as it is solely for compatibility with QPid's
    /// existing usage of the ReplyTo field; the AMQP specifications
    /// 0-8 and 0-9 do not define the format of the field, and do not
    /// define any format for the triple (exchange name, exchange
    /// type, routing key) that could be used instead.
    /// </para>
    /// </remarks>
    public class PublicationAddress
    {
        /// <summary>
        /// Regular expression used to extract the exchange-type,
        /// exchange-name and routing-key from a string.
        /// </summary>
        public static readonly Regex PSEUDO_URI_PARSER = new Regex("^([^:]+)://([^/]*)/(.*)$");

        /// <summary>
        ///  Creates a new instance of the <see cref="PublicationAddress"/>.
        /// </summary>
        /// <param name="exchangeType">Exchange type.</param>
        /// <param name="exchangeName">Exchange name.</param>
        /// <param name="routingKey">Routing key.</param>
        public PublicationAddress(string exchangeType, string exchangeName, string routingKey)
        {
            ExchangeType = exchangeType;
            ExchangeName = exchangeName;
            RoutingKey = routingKey;
        }

        /// <summary>
        /// Retrieve the exchange name.
        /// </summary>
        public readonly string ExchangeName;

        /// <summary>
        /// Retrieve the exchange type string.
        /// </summary>
        public readonly string ExchangeType;

        /// <summary>
        ///Retrieve the routing key.
        /// </summary>
        public readonly string RoutingKey;

        /// <summary>
        /// Parse a <see cref="PublicationAddress"/> out of the given string,
        ///  using the <see cref="PSEUDO_URI_PARSER"/> regex.
        /// </summary>
        public static PublicationAddress Parse(string uriLikeString)
        {
            Match match = PSEUDO_URI_PARSER.Match(uriLikeString);
            if (match.Success)
            {
                return new PublicationAddress(match.Groups[1].Value,
                    match.Groups[2].Value,
                    match.Groups[3].Value);
            }
            return null;
        }

        public static bool TryParse(string uriLikeString, out PublicationAddress result)
        {
            // Callers such as IBasicProperties.ReplyToAddress
            // expect null result for invalid input.
            // The regex.Match() throws on null arguments so we perform explicit check here
            if (uriLikeString is null)
            {
                result = null;
                return false;
            }
            else
            {
                try
                {
                    PublicationAddress res = Parse(uriLikeString);
                    result = res;
                    return true;
                }
                catch
                {
                    result = null;
                    return false;
                }
            }
        }

        /// <summary>
        /// Reconstruct the "uri" from its constituents.
        /// </summary>
        public override string ToString()
        {
            return $"{ExchangeType}://{ExchangeName}/{RoutingKey}";
        }
    }
}
