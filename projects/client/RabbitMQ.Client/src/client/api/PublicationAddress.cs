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

using System;
using System.Text.RegularExpressions;

namespace RabbitMQ.Client
{
    ///<summary>Container for an exchange name, exchange type and
    ///routing key, usable as the target address of a message to be
    ///published.</summary>
    ///<remarks>
    ///<para>
    /// The syntax used for the external representation of instances
    /// of this class is compatible with QPid's "Reply-To" field
    /// pseudo-URI format. The pseudo-URI format is
    /// (exchange-type)://(exchange-name)/(routing-key), where
    /// exchange-type is one of the permitted exchange type names (see
    /// class ExchangeType), exchange-name must be present but may be
    /// empty, and routing-key must be present but may be empty.
    ///</para>
    ///<para>
    /// The syntax is as it is solely for compatibility with QPid's
    /// existing usage of the ReplyTo field; the AMQP specifications
    /// 0-8 and 0-9 do not define the format of the field, and do not
    /// define any format for the triple (exchange name, exchange
    /// type, routing key) that could be used instead. Please see also
    /// the way class RabbitMQ.Client.MessagePatterns.SimpleRpcServer
    /// uses the ReplyTo field.
    ///</para>
    ///</remarks>
    public class PublicationAddress
    {
        ///<summary>Regular expression used to extract the
        ///exchange-type, exchange-name and routing-key from a
        ///string.</summary>
        public static readonly Regex PSEUDO_URI_PARSER = new Regex("^([^:]+)://([^/]*)/(.*)$");

        ///<summary>Construct a PublicationAddress with the given exchange
        ///type, exchange name and routing key.</summary>
        public PublicationAddress(string exchangeType, string exchangeName, string routingKey)
        {
            ExchangeType = exchangeType;
            ExchangeName = exchangeName;
            RoutingKey = routingKey;
        }

        ///<summary>Retrieve the exchange name.</summary>
        public string ExchangeName { get; private set; }

        ///<summary>Retrieve the exchange type string.</summary>
        public string ExchangeType { get; private set; }

        ///<summary>Retrieve the routing key.</summary>
        public string RoutingKey { get; private set; }

        ///<summary>Parse a PublicationAddress out of the given string,
        ///using the PSEUDO_URI_PARSER regex.</summary>
        public static PublicationAddress Parse(string uriLikeString)
        {
            Match m = PSEUDO_URI_PARSER.Match(uriLikeString);
            if (m.Success)
            {
                return new PublicationAddress(m.Groups[1].Value,
                    m.Groups[2].Value,
                    m.Groups[3].Value);
            }
            else
            {
                return null;
            }
        }

        ///<summary>Reconstruct the "uri" from its constituents.</summary>
        public override string ToString()
        {
            return ExchangeType + "://" + ExchangeName + "/" + RoutingKey;
        }
    }
}
