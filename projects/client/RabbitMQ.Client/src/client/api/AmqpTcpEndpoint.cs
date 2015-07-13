// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2015 Pivotal Software, Inc.
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
//  Copyright (c) 2007-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Represents a TCP-addressable AMQP peer: a host name and port number.
    /// </summary>
    /// <para>
    /// Some of the constructors take, as a convenience, a System.Uri
    /// instance representing an AMQP server address. The use of Uri
    /// here is not standardised - Uri is simply a convenient
    /// container for internet-address-like components. In particular,
    /// the Uri "Scheme" property is ignored: only the "Host" and
    /// "Port" properties are extracted.
    /// </para>
    public class AmqpTcpEndpoint
    {
        /// <summary>
        /// Default Amqp ssl port.
        /// </summary>
        public const int DefaultAmqpSslPort = 5671;

        /// <summary>
        /// Indicates that the default port for the protocol should be used.
        /// </summary>
        public const int UseDefaultPort = -1;

        private int _port;

        /// <summary>
        /// Creates a new instance of the <see cref="AmqpTcpEndpoint"/>.
        /// </summary>
        /// <param name="hostName">Hostname.</param>
        /// <param name="portOrMinusOne"> Port number. If the port number is -1, the default port number will be used.</param>
        /// <param name="ssl">Ssl option.</param>
        public AmqpTcpEndpoint(string hostName, int portOrMinusOne, SslOption ssl)
        {
            HostName = hostName;
            _port = portOrMinusOne;
            Ssl = ssl;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="AmqpTcpEndpoint"/>.
        /// </summary>
        /// <param name="hostName">Hostname.</param>
        /// <param name="portOrMinusOne"> Port number. If the port number is -1, the default port number will be used.</param>
        public AmqpTcpEndpoint(string hostName, int portOrMinusOne = -1) :
            this(hostName, portOrMinusOne, new SslOption())
        {
        }

        /// <summary>
        /// Construct an AmqpTcpEndpoint with "localhost" as the hostname, and using the default port.
        /// </summary>
        public AmqpTcpEndpoint() : this("localhost")
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="AmqpTcpEndpoint"/> with the given Uri and ssl options.
        /// </summary>
        /// <remarks>
        /// Please see the class overview documentation for information about the Uri format in use.
        /// </remarks>
        public AmqpTcpEndpoint(Uri uri, SslOption ssl) : this(uri.Host, uri.Port, ssl)
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="AmqpTcpEndpoint"/> with the given Uri.
        /// </summary>
        /// <remarks>
        /// Please see the class overview documentation for information about the Uri format in use.
        /// </remarks>
        public AmqpTcpEndpoint(Uri uri) : this(uri.Host, uri.Port)
        {
        }

        /// <summary>
        /// Retrieve or set the hostname of this <see cref="AmqpTcpEndpoint"/>.
        /// </summary>
        public string HostName { get; set; }

        /// <summary>Retrieve or set the port number of this
        ///AmqpTcpEndpoint. A port number of -1 causes the default
        ///port number.</summary>
        public int Port
        {
            get
            {
                if (_port != UseDefaultPort)
                {
                    return _port;
                }
                if (Ssl.Enabled)
                {
                    return DefaultAmqpSslPort;
                }
                return Protocol.DefaultPort;
            }
            set { _port = value; }
        }

        /// <summary>
        /// Retrieve IProtocol of this <see cref="AmqpTcpEndpoint"/>.
        /// </summary>
        public IProtocol Protocol
        {
            get { return Protocols.DefaultProtocol; }
        }

        /// <summary>
        /// Retrieve the SSL options for this AmqpTcpEndpoint. If not set, null is returned.
        /// </summary>
        public SslOption Ssl { get; set; }

        /// <summary>
        /// Construct an instance from a protocol and an address in "hostname:port" format.
        /// </summary>
        /// <remarks>
        /// If the address string passed in contains ":", it is split
        /// into a hostname and a port-number part. Otherwise, the
        /// entire string is used as the hostname, and the port-number
        /// is set to -1 (meaning the default number for the protocol
        /// variant specified).
        /// Hostnames provided as IPv6 must appear in square brackets ([]).
        /// </remarks>
        public static AmqpTcpEndpoint Parse(string address)
        {
            Match match = Regex.Match(address, @"^\s*\[([%:0-9A-Fa-f]+)\](:(.*))?\s*$");
            string port;
            int portNumber;
            if (match.Success)
            {
                GroupCollection groups = match.Groups;
                portNumber = -1;
                if (groups[2].Success)
                {
                    port = groups[3].Value;
                    portNumber = (port.Length == 0) ? -1 : int.Parse(port);
                }
                return new AmqpTcpEndpoint(match.Groups[1].Value, portNumber);
            }
            int index = address.LastIndexOf(':');
            if (index == -1)
            {
                return new AmqpTcpEndpoint(address);
            }
            port = address.Substring(index + 1).Trim();
            portNumber = (port.Length == 0) ? -1 : int.Parse(port);
            return new AmqpTcpEndpoint(address.Substring(0, index), portNumber);
        }

        /// <summary>
        /// Splits the passed-in string on ",", and passes the substrings to <see cref="Parse"/>.
        /// </summary>
        /// <remarks>
        /// Accepts a string of the form "hostname:port,
        /// hostname:port, ...", where the ":port" pieces are
        /// optional, and returns a corresponding array of <see cref="AmqpTcpEndpoint"/>s.
        /// </remarks>
        public static AmqpTcpEndpoint[] ParseMultiple(string addresses)
        {
            string[] partsArr = addresses.Split(new[] {','});
            var results = new List<AmqpTcpEndpoint>();
            foreach (string partRaw in partsArr)
            {
                string part = partRaw.Trim();
                if (part.Length > 0)
                {
                    results.Add(Parse(part));
                }
            }
            return results.ToArray();
        }

        /// <summary>
        /// Compares this instance by value (protocol, hostname, port) against another instance.
        /// </summary>
        public override bool Equals(object obj)
        {
            var other = obj as AmqpTcpEndpoint;
            if (other == null)
            {
                return false;
            }
            if (other.HostName != HostName)
            {
                return false;
            }
            if (other.Port != Port)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Implementation of hash code depending on protocol, hostname and port,
        /// to line up with the implementation of <see cref="Equals"/>.
        /// </summary>
        public override int GetHashCode()
        {
            return HostName.GetHashCode() ^ Port;
        }

        /// <summary>
        /// Returns a URI-like string of the form amqp-PROTOCOL://HOSTNAME:PORTNUMBER.
        /// </summary>
        /// <remarks>
        /// This method is intended mainly for debugging and logging use.
        /// </remarks>
        public override string ToString()
        {
            return "amqp://" + HostName + ":" + Port;
        }
    }
}
