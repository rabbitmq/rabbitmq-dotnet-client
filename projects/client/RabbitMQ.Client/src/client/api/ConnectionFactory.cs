// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2013 GoPivotal, Inc.
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
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Collections.Generic;

using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Exceptions;

namespace RabbitMQ.Client
{
    ///<summary>Main entry point to the RabbitMQ .NET AMQP client
    ///API. Constructs IConnection instances.</summary>
    ///<remarks>
    ///<para>
    /// A simple example of connecting to a broker:
    ///</para>
    ///<example><code>
    ///     ConnectionFactory factory = new ConnectionFactory();
    ///     //
    ///     // The next six lines are optional:
    ///     factory.UserName = ConnectionFactory.DefaultUser;
    ///     factory.Password = ConnectionFactory.DefaultPass;
    ///     factory.VirtualHost = ConnectionFactory.DefaultVHost;
    ///     factory.Protocol = Protocols.FromEnvironment();
    ///     factory.HostName = hostName;
    ///     factory.Port     = AmqpTcpEndpoint.UseDefaultPort;
    ///     //
    ///     IConnection conn = factory.CreateConnection();
    ///     //
    ///     IModel ch = conn.CreateModel();
    ///     //
    ///     // ... use ch's IModel methods ...
    ///     //
    ///     ch.Close(Constants.ReplySuccess, "Closing the channel");
    ///     conn.Close(Constants.ReplySuccess, "Closing the connection");
    ///</code></example>
    ///<para>
    ///The same example, written more compactly with AMQP URIs:
    ///</para>
    ///<example><code>
    ///     ConnectionFactory factory = new ConnectionFactory();
    ///     factory.Uri = "amqp://localhost";
    ///     IConnection conn = factory.CreateConnection();
    ///     ...
    ///</code></example>
    ///<para>
    /// Please see also the API overview and tutorial in the User Guide.
    ///</para>
    ///<para>
    ///Note that the Uri property takes a string representation of an
    ///AMQP URI.  Omitted URI parts will take default values.  The
    ///host part of the URI cannot be omitted and URIs of the form
    ///"amqp://foo/" (note the trailling slash) also represent the
    ///default virtual host.  The latter issue means that virtual
    ///hosts with an empty name are not addressable. </para></remarks>
    public class ConnectionFactory
    {
        /// <summary>Default user name (value: "guest")</summary>
        public const string DefaultUser = "guest"; // PLEASE KEEP THIS MATCHING THE DOC ABOVE

        /// <summary>Default password (value: "guest")</summary>
        public const string DefaultPass = "guest"; // PLEASE KEEP THIS MATCHING THE DOC ABOVE

        /// <summary>Default virtual host (value: "/")</summary>
        public const string DefaultVHost = "/"; // PLEASE KEEP THIS MATCHING THE DOC ABOVE

        /// <summary> Default value for the desired maximum channel
        /// number, with zero meaning unlimited (value: 0)</summary>
        public const ushort DefaultChannelMax = 0; // PLEASE KEEP THIS MATCHING THE DOC ABOVE

        /// <summary>Default value for the desired maximum frame size,
        /// with zero meaning unlimited (value: 0)</summary>
        public const uint DefaultFrameMax = 0; // PLEASE KEEP THIS MATCHING THE DOC ABOVE

        /// <summary>Default value for desired heartbeat interval, in
        /// seconds, with zero meaning none (value: 0)</summary>
        public const ushort DefaultHeartbeat = 0; // PLEASE KEEP THIS MATCHING THE DOC ABOVE

        /// <summary> Default value for connection attempt timeout,
        /// in milliseconds</summary>
        public const int DefaultConnectionTimeout = 30 * 1000;

        ///<summary> Default SASL auth mechanisms to use.</summary>
        public static AuthMechanismFactory[] DefaultAuthMechanisms =
            new AuthMechanismFactory[] { new PlainMechanismFactory() };

        /// <summary>Username to use when authenticating to the server</summary>
        public string UserName = DefaultUser;

        /// <summary>Password to use when authenticating to the server</summary>
        public string Password = DefaultPass;

        /// <summary>Virtual host to access during this connection</summary>
        public string VirtualHost = DefaultVHost;

        /// <summary>Maximum channel number to ask for</summary>
        public ushort RequestedChannelMax = DefaultChannelMax;

        /// <summary>Frame-max parameter to ask for (in bytes)</summary>
        public uint RequestedFrameMax = DefaultFrameMax;

        /// <summary>Heartbeat setting to request (in seconds)</summary>
        public ushort RequestedHeartbeat = DefaultHeartbeat;

        /// <summary>Timeout setting for connection attempts (in milliseconds)</summary>
        public int RequestedConnectionTimeout = DefaultConnectionTimeout;

        /// <summary>Dictionary of client properties to be sent to the
        /// server</summary>
        public IDictionary<string, object> ClientProperties = ConnectionBase.DefaultClientProperties();

        ///<summary>Ssl options setting</summary>
        public SslOption Ssl = new SslOption();

        ///<summary>The host to connect to</summary>
        public String HostName = "localhost";

        ///<summary>The port to connect on.
        /// AmqpTcpEndpoint.UseDefaultPort indicates the default for
        /// the protocol should be used.</summary>
        public int Port = AmqpTcpEndpoint.UseDefaultPort;

        ///<summary> SASL auth mechanisms to use.</summary>
        public AuthMechanismFactory[] AuthMechanisms = DefaultAuthMechanisms;

        ///<summary>The AMQP protocol to be used</summary>
        public IProtocol Protocol = Protocols.FromEnvironment();

        ///<summary>The AMQP connection target</summary>
        public AmqpTcpEndpoint Endpoint
        {
          get
          {
              return new AmqpTcpEndpoint(Protocol, HostName, Port, Ssl);
          }
          set
          {
              Protocol = value.Protocol;
              Port = value.Port;
              HostName = value.HostName;
              Ssl = value.Ssl;
          }
        }

        ///<summary>Set connection parameters using the amqp or amqps scheme</summary>
        public Uri uri
        {
          set { SetUri(value); }
        }

        ///<summary>Set connection parameters using the amqp or amqps scheme</summary>
        public String Uri
        {
          set { SetUri(new Uri(value, UriKind.Absolute)); }
        }

        public delegate TcpClient ObtainSocket(AddressFamily addressFamily);

        ///<summary>Set custom socket options by providing a SocketFactory</summary>
        public ObtainSocket SocketFactory = DefaultSocketFactory;

        ///<summary>Construct a fresh instance, with all fields set to
        ///their respective defaults.</summary>
        public ConnectionFactory() { }

        protected virtual IConnection FollowRedirectChain
            (int maxRedirects,
             IDictionary<AmqpTcpEndpoint, int> connectionAttempts,
             IDictionary<AmqpTcpEndpoint, Exception> connectionErrors,
             ref AmqpTcpEndpoint[] mostRecentKnownHosts,
             AmqpTcpEndpoint endpoint)
        {
            AmqpTcpEndpoint candidate = endpoint;
            try {
                while (true) {
                    int attemptCount =
                        connectionAttempts.ContainsKey(candidate)
                        ? (int) connectionAttempts[candidate]
                        : 0;
                    connectionAttempts[candidate] = attemptCount + 1;
                    bool insist = attemptCount >= maxRedirects;

                    try {
                        IProtocol p = candidate.Protocol;
                        IFrameHandler fh = p.CreateFrameHandler(candidate,
                                                                SocketFactory,
                                                                RequestedConnectionTimeout);

                        // At this point, we may be able to create
                        // and fully open a successful connection,
                        // in which case we're done, and the
                        // connection should be returned.
                        return p.CreateConnection(this, insist, fh);
                    } catch (RedirectException re) {
                        if (insist) {
                            // We've been redirected, but we insisted that
                            // we shouldn't be redirected! Well-behaved
                            // brokers should never do this.
                            string message = string.Format("Server {0} ignored 'insist' flag, redirecting us to {1}",
                                                           candidate,
                                                           re.Host);
                            throw new ProtocolViolationException(message);
                        } else {
                            // We've been redirected. Follow this new link
                            // in the chain, by setting
                            // mostRecentKnownHosts (in case the chain
                            // runs out), and updating candidate for the
                            // next time round the loop.
                            connectionErrors[candidate] = re;
                            mostRecentKnownHosts = re.KnownHosts;
                            candidate = re.Host;
                        }
                    }
                }
            } catch (Exception e) {
                connectionErrors[candidate] = e;
                return null;
            }
        }

        protected virtual IConnection CreateConnection(int maxRedirects,
                                                       IDictionary<AmqpTcpEndpoint, int> connectionAttempts,
                                                       IDictionary<AmqpTcpEndpoint, Exception> connectionErrors,
                                                       params AmqpTcpEndpoint[] endpoints)
        {
            foreach (AmqpTcpEndpoint endpoint in endpoints)
            {
                AmqpTcpEndpoint[] mostRecentKnownHosts = new AmqpTcpEndpoint[0];
                // ^^ holds a list of known-hosts that came back with
                // a connection.redirect. If, once we reach the end of
                // a chain of redirects, we still haven't managed to
                // get a usable connection, we recurse on
                // mostRecentKnownHosts, trying each of those in
                // turn. Finally, if neither the initial
                // chain-of-redirects for the current endpoint, nor
                // the chains-of-redirects for each of the
                // mostRecentKnownHosts gives us a usable connection,
                // we give up on this particular endpoint, and
                // continue with the foreach loop, trying the
                // remainder of the array we were given.
                IConnection conn = FollowRedirectChain(maxRedirects,
                                                       connectionAttempts,
                                                       connectionErrors,
                                                       ref mostRecentKnownHosts,
                                                       endpoint);
                if (conn != null) {
                    return conn;
                }

                // Connection to this endpoint failed at some point
                // down the redirection chain - either the first
                // entry, or one of the re.Host values from subsequent
                // RedirectExceptions. We recurse into
                // mostRecentKnownHosts, to see if one of those is
                // suitable.
                if (mostRecentKnownHosts.Length > 0) {
                    // Only bother recursing if we know of some
                    // hosts. If we were to recurse with no endpoints
                    // in the array, we'd stomp on
                    // mostRecentException, which makes debugging
                    // connectivity problems needlessly more
                    // difficult.
                    conn = CreateConnection(maxRedirects,
                                            connectionAttempts,
                                            connectionErrors,
                                            mostRecentKnownHosts);
                    if (conn != null) {
                        return conn;
                    }
                }
            }
            return null;
        }

        ///<summary>Create a connection to the first available
        ///endpoint in the list provided. Up to a maximum of
        ///maxRedirects broker-originated redirects are permitted for
        ///each endpoint tried.</summary>
        public virtual IConnection CreateConnection(int maxRedirects)
        {
            IDictionary<AmqpTcpEndpoint, int> connectionAttempts = new Dictionary<AmqpTcpEndpoint, int>();
            Dictionary<AmqpTcpEndpoint, Exception> connectionErrors = new Dictionary<AmqpTcpEndpoint, Exception>();
            IConnection conn = CreateConnection(maxRedirects,
                                                connectionAttempts,
                                                connectionErrors,
                                                new AmqpTcpEndpoint[]{Endpoint});
            if (conn != null) {
                return conn;
            }
            Exception Inner = connectionErrors[Endpoint] as Exception;
            throw new BrokerUnreachableException(connectionAttempts, connectionErrors, Inner);
        }

        ///<summary>Create a connection to the specified endpoint
        ///No broker-originated redirects are permitted.</summary>
        public virtual IConnection CreateConnection()
        {
            return CreateConnection(0);
        }

        ///<summary>Given a list of mechanism names supported by the
        ///server, select a preferred mechanism, or null if we have
        ///none in common.</summary>
        public AuthMechanismFactory AuthMechanismFactory(string[] mechs) {
            // Our list is in order of preference, the server one is not.
            foreach (AuthMechanismFactory f in AuthMechanisms) {
                if (((IList<string>)mechs).Contains(f.Name)) {
                    return f;
                }
            }

            return null;
        }

        public static TcpClient DefaultSocketFactory(AddressFamily addressFamily)
        {
            TcpClient tcpClient = new TcpClient(addressFamily);
            tcpClient.NoDelay = true;
            return tcpClient;
        }

        private void SetUri(Uri uri)
        {
            Endpoint = new AmqpTcpEndpoint();

            if ("amqp".CompareTo(uri.Scheme.ToLower()) == 0) {
                // nothing special to do
            } else if ("amqps".CompareTo(uri.Scheme.ToLower()) == 0) {
                Ssl.Enabled = true;
                Ssl.AcceptablePolicyErrors =
                    SslPolicyErrors.RemoteCertificateNameMismatch;
                Port = AmqpTcpEndpoint.DefaultAmqpSslPort;
            } else {
                throw new ArgumentException("Wrong scheme in AMQP URI: " +
                                            uri.Scheme);
            }
            string host = uri.Host;
            if (!String.IsNullOrEmpty(host)) {
                HostName = host;
            }
            Ssl.ServerName = HostName;

            int port = uri.Port;
            if (port != -1) {
                Port = port;
            }

            string userInfo = uri.UserInfo;
            if (!String.IsNullOrEmpty(userInfo)) {
                string[] userPass = userInfo.Split(':');
                if (userPass.Length > 2) {
                    throw new ArgumentException("Bad user info in AMQP " +
                                                "URI: " + userInfo);
                }
                UserName = UriDecode(userPass[0]);
                if (userPass.Length == 2) {
                    Password = UriDecode(userPass[1]);
                }
            }

            /* C# automatically changes URIs into a canonical form
               that has at least the path segment "/". */
            if (uri.Segments.Length > 2) {
                throw new ArgumentException("Multiple segments in " +
                                            "path of AMQP URI: " +
                                            String.Join(", ", uri.Segments));
            } else if (uri.Segments.Length == 2) {
                VirtualHost = UriDecode(uri.Segments[1]);
            }
        }

        //<summary>Unescape a string, protecting '+'.</summary>
        private string UriDecode(string uri) {
            return System.Uri.UnescapeDataString(uri.Replace("+", "%2B"));
        }
    }
}
