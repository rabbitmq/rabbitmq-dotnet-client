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

using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Threading.Tasks;

namespace RabbitMQ.Client
{
    /// <summary>Main entry point to the RabbitMQ .NET AMQP client
    ///API. Constructs <see cref="IConnection"/> instances.</summary>
    /// <remarks>
    /// <para>
    /// A simple example of connecting to a broker:
    /// </para>
    /// <example><code>
    ///     IConnectionFactory factory = new ConnectionFactory();
    ///     //
    ///     // The next six lines are optional:
    ///     factory.UserName = ConnectionFactory.DefaultUser;
    ///     factory.Password = ConnectionFactory.DefaultPass;
    ///     factory.VirtualHost = ConnectionFactory.DefaultVHost;
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
    /// </code></example>
    /// <para>
    ///The same example, written more compactly with AMQP URIs:
    /// </para>
    /// <example><code>
    ///     ConnectionFactory factory = new ConnectionFactory();
    ///     factory.Uri = "amqp://localhost";
    ///     IConnection conn = factory.CreateConnection();
    ///     ...
    /// </code></example>
    /// <para>
    /// Please see also the API overview and tutorial in the User Guide.
    /// </para>
    /// <para>
    ///Note that the Uri property takes a string representation of an
    ///AMQP URI.  Omitted URI parts will take default values.  The
    ///host part of the URI cannot be omitted and URIs of the form
    ///"amqp://foo/" (note the trailling slash) also represent the
    ///default virtual host.  The latter issue means that virtual
    ///hosts with an empty name are not addressable. </para></remarks>
    public class ConnectionFactory : ConnectionFactoryBase, IConnectionFactory
    {
        /// <summary>
        /// Default value for the desired maximum channel number, with zero meaning unlimited (value: 0).
        /// </summary>
        /// <remarks>PLEASE KEEP THIS MATCHING THE DOC ABOVE.</remarks>
        public const ushort DefaultChannelMax = 0;

        /// <summary>
        /// Default value for connection attempt timeout, in milliseconds.
        /// </summary>
        public const int DefaultConnectionTimeout = 30 * 1000;

        /// <summary>
        /// Default value for the desired maximum frame size, with zero meaning unlimited (value: 0).
        /// </summary>
        /// <remarks>PLEASE KEEP THIS MATCHING THE DOC ABOVE.</remarks>
        public const uint DefaultFrameMax = 0;

        /// <summary>
        /// Default value for desired heartbeat interval, in seconds, with zero meaning none (value: 0).
        /// </summary>
        /// <remarks>PLEASE KEEP THIS MATCHING THE DOC ABOVE.</remarks>
        public const ushort DefaultHeartbeat = 0; //

        /// <summary>
        /// Default password (value: "guest").
        /// </summary>
        /// <remarks>PLEASE KEEP THIS MATCHING THE DOC ABOVE.</remarks>
        public const string DefaultPass = "guest";

        /// <summary>
        /// Default user name (value: "guest").
        /// </summary>
        /// <remarks>PLEASE KEEP THIS MATCHING THE DOC ABOVE.</remarks>
        public const string DefaultUser = "guest";

        /// <summary>
        /// Default virtual host (value: "/").
        /// </summary>
        /// <remarks> PLEASE KEEP THIS MATCHING THE DOC ABOVE.</remarks>
        public const string DefaultVHost = "/";

        /// <summary>
        ///  Default SASL auth mechanisms to use.
        /// </summary>
        public static AuthMechanismFactory[] DefaultAuthMechanisms = { new PlainMechanismFactory() };

        /// <summary>
        ///  SASL auth mechanisms to use.
        /// </summary>
        public AuthMechanismFactory[] AuthMechanisms = DefaultAuthMechanisms;

        /// <summary>
        /// Set to true to enable automatic connection recovery.
        /// </summary>
        public bool AutomaticRecoveryEnabled;

        /// <summary>The host to connect to.</summary>
        public String HostName = "localhost";

        /// <summary>
        /// Amount of time client will wait for before re-trying  to recover connection.
        /// </summary>
        public TimeSpan NetworkRecoveryInterval = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The port to connect on. <see cref="AmqpTcpEndpoint.UseDefaultPort"/>
        ///  indicates the default for the protocol should be used.
        /// </summary>
        public int Port = AmqpTcpEndpoint.UseDefaultPort;

        /// <summary>
        /// The AMQP protocol to be used. Currently 0-9-1.
        /// </summary>
        public IProtocol Protocol = Protocols.DefaultProtocol;

        /// <summary>
        /// Timeout setting for connection attempts (in milliseconds).
        /// </summary>
        public int RequestedConnectionTimeout = DefaultConnectionTimeout;

        /// <summary>
        /// Ssl options setting.
        /// </summary>
        public SslOption Ssl = new SslOption();

        /// <summary>
        /// Set to true to make automatic connection recovery also recover topology (exchanges, queues, bindings, etc).
        /// </summary>
        public bool TopologyRecoveryEnabled = true;

        /// <summary>
        /// Task scheduler connections created by this factory will use when
        /// dispatching consumer operations, such as message deliveries.
        /// </summary>
        public TaskScheduler TaskScheduler { get; set; }

        /// <summary>
        /// Construct a fresh instance, with all fields set to their respective defaults.
        /// </summary>
        public ConnectionFactory()
        {
            this.TaskScheduler = TaskScheduler.Default;
            VirtualHost = DefaultVHost;
            UserName = DefaultUser;
            RequestedHeartbeat = DefaultHeartbeat;
            RequestedFrameMax = DefaultFrameMax;
            RequestedChannelMax = DefaultChannelMax;
            Password = DefaultPass;
            ClientProperties = Connection.DefaultClientProperties();
            UseBackgroundThreadsForIO = false;
        }

        /// <summary>
        /// The AMQP connection target.
        /// </summary>
        public AmqpTcpEndpoint Endpoint
        {
            get { return new AmqpTcpEndpoint(HostName, Port, Ssl); }
            set
            {
                Port = value.Port;
                HostName = value.HostName;
                Ssl = value.Ssl;
            }
        }

        /// <summary>
        /// Set connection parameters using the amqp or amqps scheme.
        /// </summary>
        public String Uri
        {
            set { SetUri(new Uri(value, UriKind.Absolute)); }
        }

        /// <summary>
        /// Set connection parameters using the amqp or amqps scheme.
        /// </summary>
        public Uri uri
        {
            set { SetUri(value); }
        }

        /// <summary>
        /// Dictionary of client properties to be sent to the server.
        /// </summary>
        public IDictionary<string, object> ClientProperties { get; set; }

        /// <summary>
        /// Password to use when authenticating to the server.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Maximum channel number to ask for.
        /// </summary>
        public ushort RequestedChannelMax { get; set; }

        /// <summary>
        /// Frame-max parameter to ask for (in bytes).
        /// </summary>
        public uint RequestedFrameMax { get; set; }

        /// <summary>
        /// Heartbeat timeout to use when negotiating with the server (in seconds).
        /// </summary>
        public ushort RequestedHeartbeat { get; set; }

        /// <summary>
        /// When set to true, background thread will be used for the I/O loop.
        /// </summary>
        public bool UseBackgroundThreadsForIO { get; set; }

        /// <summary>
        /// Username to use when authenticating to the server.
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Virtual host to access during this connection.
        /// </summary>
        public string VirtualHost { get; set; }

        /// <summary>
        /// Given a list of mechanism names supported by the server, select a preferred mechanism,
        ///  or null if we have none in common.
        /// </summary>
        public AuthMechanismFactory AuthMechanismFactory(string[] mechanismNames)
        {
            // Our list is in order of preference, the server one is not.
            foreach (AuthMechanismFactory factory in AuthMechanisms)
            {
                if (Array.Exists(mechanismNames, x => string.Equals(x, factory.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return factory;
                }
            }
            return null;
        }

        /// <summary>
        /// Create a connection to the specified endpoint.
        /// </summary>
        public virtual IConnection CreateConnection()
        {
            IConnection connection;
            try
            {
                if (AutomaticRecoveryEnabled)
                {
                    var autorecoveringConnection = new AutorecoveringConnection(this);
                    autorecoveringConnection.init();
                    connection = autorecoveringConnection;
                }
                else
                {
                    IProtocol protocol = Protocols.DefaultProtocol;
                    connection = protocol.CreateConnection(this, false, CreateFrameHandler());
                }
            }
            catch (Exception e)
            {
                throw new BrokerUnreachableException(e);
            }

            return connection;
        }

        public IFrameHandler CreateFrameHandler()
        {
            IProtocol protocol = Protocols.DefaultProtocol;
            return protocol.CreateFrameHandler(Endpoint, SocketFactory, RequestedConnectionTimeout);
        }

        private void SetUri(Uri uri)
        {
            Endpoint = new AmqpTcpEndpoint();

            if (string.Equals("amqp", uri.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                // nothing special to do
            }
            else if (string.Equals("amqps", uri.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                Ssl.Enabled = true;
                Ssl.AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateNameMismatch;
                Port = AmqpTcpEndpoint.DefaultAmqpSslPort;
            }
            else
            {
                throw new ArgumentException("Wrong scheme in AMQP URI: " + uri.Scheme);
            }
            string host = uri.Host;
            if (!string.IsNullOrEmpty(host))
            {
                HostName = host;
            }
            Ssl.ServerName = HostName;

            int port = uri.Port;
            if (port != -1)
            {
                Port = port;
            }

            string userInfo = uri.UserInfo;
            if (!string.IsNullOrEmpty(userInfo))
            {
                string[] userPass = userInfo.Split(':');
                if (userPass.Length > 2)
                {
                    throw new ArgumentException("Bad user info in AMQP " + "URI: " + userInfo);
                }
                UserName = UriDecode(userPass[0]);
                if (userPass.Length == 2)
                {
                    Password = UriDecode(userPass[1]);
                }
            }

            /* C# automatically changes URIs into a canonical form
               that has at least the path segment "/". */
            if (uri.Segments.Length > 2)
            {
                throw new ArgumentException("Multiple segments in " +
                                            "path of AMQP URI: " +
                                            string.Join(", ", uri.Segments));
            }
            if (uri.Segments.Length == 2)
            {
                VirtualHost = UriDecode(uri.Segments[1]);
            }
        }

        ///<summary>
        /// Unescape a string, protecting '+'.
        /// </summary>
        private string UriDecode(string uri)
        {
            return System.Uri.UnescapeDataString(uri.Replace("+", "%2B"));
        }
    }
}