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
using System.Net.Security;
using System.Reflection;
using System.Security.Authentication;
using System.Text;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client
{
    /// <summary>Main entry point to the RabbitMQ .NET AMQP client
    ///API. Constructs <see cref="IConnection"/> instances.</summary>
    /// <remarks>
    /// <para>
    /// A simple example of connecting to a broker:
    /// </para>
    /// <example><code>
    ///     ConnectionFactory factory = new ConnectionFactory();
    ///     //
    ///     // The next five lines are optional:
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
    ///     factory.Uri = new Uri("amqp://localhost");
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
    ///"amqp://foo/" (note the trailing slash) also represent the
    ///default virtual host.  The latter issue means that virtual
    ///hosts with an empty name are not addressable. </para></remarks>
    public sealed class ConnectionFactory : ConnectionFactoryBase, IConnectionFactory
    {
        /// <summary>
        /// Default value for the desired maximum channel number. Default: 2047.
        /// </summary>
        public const ushort DefaultChannelMax = 2047;

        /// <summary>
        /// Default value for connection attempt timeout.
        /// </summary>
        public static readonly TimeSpan DefaultConnectionTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Default value for the desired maximum frame size. Default is 0 ("no limit").
        /// </summary>
        public const uint DefaultFrameMax = 0;

        /// <summary>
        /// Default value for desired heartbeat interval. Default is 60 seconds,
        /// TimeSpan.Zero means "heartbeats are disabled".
        /// </summary>
        public static readonly TimeSpan DefaultHeartbeat = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Default password (value: "guest").
        /// </summary>
        public const string DefaultPass = "guest";

        /// <summary>
        /// Default user name (value: "guest").
        /// </summary>
        public const string DefaultUser = "guest";

        /// <summary>
        /// Default virtual host (value: "/").
        /// </summary>
        public const string DefaultVHost = "/";

        /// <summary>
        /// TLS versions enabled by default: TLSv1.2, v1.1, v1.0.
        /// </summary>
        public static SslProtocols DefaultAmqpUriSslProtocols { get; set; } = SslProtocols.None;

        /// <summary>
        /// The AMQP URI SSL protocols.
        /// </summary>
        public SslProtocols AmqpUriSslProtocols { get; set; } = DefaultAmqpUriSslProtocols;

        /// <summary>
        ///  Default SASL auth mechanisms to use.
        /// </summary>
        public static readonly IList<IAuthMechanismFactory> DefaultAuthMechanisms = new List<IAuthMechanismFactory>(1) { new PlainMechanismFactory() };

        /// <summary>
        ///  SASL auth mechanisms to use.
        /// </summary>
        public IList<IAuthMechanismFactory> AuthMechanisms { get; set; } = DefaultAuthMechanisms;

        /// <summary>
        /// Address family used by default.
        /// Use <see cref="System.Net.Sockets.AddressFamily.InterNetwork" /> to force to IPv4.
        /// Use <see cref="System.Net.Sockets.AddressFamily.InterNetworkV6" /> to force to IPv6.
        /// Or use <see cref="System.Net.Sockets.AddressFamily.Unknown" /> to attempt both IPv6 and IPv4.
        /// </summary>
        public static System.Net.Sockets.AddressFamily DefaultAddressFamily { get; set; }

        /// <summary>
        /// Set to false to disable automatic connection recovery.
        /// Defaults to true.
        /// </summary>
        public bool AutomaticRecoveryEnabled { get; set; } = true;

        /// <summary>
        /// Set to a value greater than one to enable concurrent processing. For a concurrency greater than one <see cref="IBasicConsumer"/>
        /// will be offloaded to the worker thread pool so it is important to choose the value for the concurrency wisely to avoid thread pool overloading.
        /// <see cref="IAsyncBasicConsumer"/> can handle concurrency much more efficiently due to the non-blocking nature of the consumer.
        /// Defaults to 1.
        /// </summary>
        /// <remarks>For concurrency greater than one this removes the guarantee that consumers handle messages in the order they receive them.
        /// In addition to that consumers need to be thread/concurrency safe.</remarks>
        public int ConsumerDispatchConcurrency { get; set; } = 1;

        /// <summary>The host to connect to.</summary>
        public string HostName { get; set; } = "localhost";

        /// <summary>
        /// Amount of time client will wait for before re-trying  to recover connection.
        /// </summary>
        public TimeSpan NetworkRecoveryInterval { get; set; } = TimeSpan.FromSeconds(5);

        private TimeSpan _handshakeContinuationTimeout = TimeSpan.FromSeconds(10);
        private TimeSpan _continuationTimeout = TimeSpan.FromSeconds(20);

        // just here to hold the value that was set through the setter
        private Uri _uri;

        /// <summary>
        /// Amount of time protocol handshake operations are allowed to take before
        /// timing out.
        /// </summary>
        public TimeSpan HandshakeContinuationTimeout
        {
            get { return _handshakeContinuationTimeout; }
            set { _handshakeContinuationTimeout = value; }
        }

        /// <summary>
        /// Amount of time protocol operations (e.g. <code>queue.declare</code>) are allowed to take before
        /// timing out.
        /// </summary>
        public TimeSpan ContinuationTimeout
        {
            get { return _continuationTimeout; }
            set { _continuationTimeout = value; }
        }

        /// <summary>
        /// Factory function for creating the <see cref="IEndpointResolver"/>
        /// used to generate a list of endpoints for the ConnectionFactory
        /// to try in order.
        /// The default value creates an instance of the <see cref="DefaultEndpointResolver"/>
        /// using the list of endpoints passed in. The DefaultEndpointResolver shuffles the
        /// provided list each time it is requested.
        /// </summary>
        public Func<IEnumerable<AmqpTcpEndpoint>, IEndpointResolver> EndpointResolverFactory { get; set; } =
            endpoints => new DefaultEndpointResolver(endpoints);

        /// <summary>
        /// The port to connect on. <see cref="AmqpTcpEndpoint.UseDefaultPort"/>
        ///  indicates the default for the protocol should be used.
        /// </summary>
        public int Port { get; set; } = AmqpTcpEndpoint.UseDefaultPort;

        /// <summary>
        /// Timeout setting for connection attempts.
        /// </summary>
        public TimeSpan RequestedConnectionTimeout { get; set; } = DefaultConnectionTimeout;

        /// <summary>
        /// Timeout setting for socket read operations.
        /// </summary>
        public TimeSpan SocketReadTimeout { get; set; } = DefaultConnectionTimeout;

        /// <summary>
        /// Timeout setting for socket write operations.
        /// </summary>
        public TimeSpan SocketWriteTimeout { get; set; } = DefaultConnectionTimeout;

        /// <summary>
        /// TLS options setting.
        /// </summary>
        public SslOption Ssl { get; set; } = new SslOption();

        /// <summary>
        /// Set to false to make automatic connection recovery not recover topology (exchanges, queues, bindings, etc).
        /// Defaults to true.
        /// </summary>
        public bool TopologyRecoveryEnabled { get; set; } = true;

        /// <summary>
        /// Construct a fresh instance, with all fields set to their respective defaults.
        /// </summary>
        public ConnectionFactory()
        {
            ClientProperties = new Dictionary<string, object>(DefaultClientProperties);
        }

        /// <summary>
        /// Connection endpoint.
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
        /// Dictionary of client properties to be sent to the server.
        /// </summary>
        public IDictionary<string, object> ClientProperties { get; set; }

        private static readonly Dictionary<string, object> DefaultClientProperties = new Dictionary<string, object>(5)
        {
            ["product"] = Encoding.UTF8.GetBytes("RabbitMQ"),
            ["version"] = Encoding.UTF8.GetBytes(typeof(ConnectionFactory).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion),
            ["platform"] = Encoding.UTF8.GetBytes(".NET"),
            ["copyright"] = Encoding.UTF8.GetBytes("Copyright (c) 2007-2020 VMware, Inc."),
            ["information"] = Encoding.UTF8.GetBytes("Licensed under the MPL. See https://www.rabbitmq.com/")
        };

        /// <summary>
        /// Password to use when authenticating to the server.
        /// </summary>
        public string Password { get; set; } = DefaultPass;

        /// <summary>
        /// Maximum channel number to ask for.
        /// </summary>
        public ushort RequestedChannelMax { get; set; } = DefaultChannelMax;

        /// <summary>
        /// Frame-max parameter to ask for (in bytes).
        /// </summary>
        public uint RequestedFrameMax { get; set; } = DefaultFrameMax;

        /// <summary>
        /// Heartbeat timeout to use when negotiating with the server.
        /// </summary>
        public TimeSpan RequestedHeartbeat { get; set; } = DefaultHeartbeat;

        /// <summary>
        /// Username to use when authenticating to the server.
        /// </summary>
        public string UserName { get; set; } = DefaultUser;

        /// <summary>
        /// Virtual host to access during this connection.
        /// </summary>
        public string VirtualHost { get; set; } = DefaultVHost;

        /// <summary>
        /// The uri to use for the connection.
        /// </summary>
        public Uri Uri
        {
            get { return _uri; }
            set { SetUri(value); }
        }

        /// <summary>
        /// Default client provided name to be used for connections.
        /// </summary>
        public string ClientProvidedName { get; set; }

        /// <summary>
        /// Given a list of mechanism names supported by the server, select a preferred mechanism,
        ///  or null if we have none in common.
        /// </summary>
        public IAuthMechanismFactory AuthMechanismFactory(IList<string> mechanismNames)
        {
            // Our list is in order of preference, the server one is not.
            for (int index = 0; index < AuthMechanisms.Count; index++)
            {
                IAuthMechanismFactory factory = AuthMechanisms[index];
                string factoryName = factory.Name;

                for (int i = 0; i < mechanismNames.Count; i++)
                {
                    if (string.Equals(mechanismNames[i], factoryName, StringComparison.OrdinalIgnoreCase))
                    {
                        return factory;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Create a connection to one of the endpoints provided by the IEndpointResolver
        /// returned by the EndpointResolverFactory. By default the configured
        /// hostname and port are used.
        /// </summary>
        /// <exception cref="BrokerUnreachableException">
        /// When the configured hostname was not reachable.
        /// </exception>
        public IConnection CreateConnection()
        {
            return CreateConnection(ClientProvidedName);
        }

        /// <summary>
        /// Create a connection to one of the endpoints provided by the IEndpointResolver
        /// returned by the EndpointResolverFactory. By default the configured
        /// hostname and port are used.
        /// </summary>
        /// <param name="clientProvidedName">
        /// Application-specific connection name, will be displayed in the management UI
        /// if RabbitMQ server supports it. This value doesn't have to be unique and cannot
        /// be used as a connection identifier, e.g. in HTTP API requests.
        /// This value is supposed to be human-readable.
        /// </param>
        /// <exception cref="BrokerUnreachableException">
        /// When the configured hostname was not reachable.
        /// </exception>
        public IConnection CreateConnection(string clientProvidedName)
        {
            return CreateConnection(EndpointResolverFactory(LocalEndpoints()), clientProvidedName);
        }

        /// <summary>
        /// Create a connection using a list of hostnames using the configured port.
        /// By default each hostname is tried in a random order until a successful connection is
        /// found or the list is exhausted using the DefaultEndpointResolver.
        /// The selection behaviour can be overridden by configuring the EndpointResolverFactory.
        /// </summary>
        /// <param name="hostnames">
        /// List of hostnames to use for the initial
        /// connection and recovery.
        /// </param>
        /// <returns>Open connection</returns>
        /// <exception cref="BrokerUnreachableException">
        /// When no hostname was reachable.
        /// </exception>
        public IConnection CreateConnection(IList<string> hostnames)
        {
            return CreateConnection(hostnames, ClientProvidedName);
        }

        /// <summary>
        /// Create a connection using a list of hostnames using the configured port.
        /// By default each endpoint is tried in a random order until a successful connection is
        /// found or the list is exhausted.
        /// The selection behaviour can be overridden by configuring the EndpointResolverFactory.
        /// </summary>
        /// <param name="hostnames">
        /// List of hostnames to use for the initial
        /// connection and recovery.
        /// </param>
        /// <param name="clientProvidedName">
        /// Application-specific connection name, will be displayed in the management UI
        /// if RabbitMQ server supports it. This value doesn't have to be unique and cannot
        /// be used as a connection identifier, e.g. in HTTP API requests.
        /// This value is supposed to be human-readable.
        /// </param>
        /// <returns>Open connection</returns>
        /// <exception cref="BrokerUnreachableException">
        /// When no hostname was reachable.
        /// </exception>
        public IConnection CreateConnection(IList<string> hostnames, string clientProvidedName)
        {
            IEnumerable<AmqpTcpEndpoint> endpoints = hostnames.Select(h => new AmqpTcpEndpoint(h, Port, Ssl));
            return CreateConnection(EndpointResolverFactory(endpoints), clientProvidedName);
        }

        /// <summary>
        /// Create a connection using a list of endpoints. By default each endpoint will be tried
        /// in a random order until a successful connection is found or the list is exhausted.
        /// The selection behaviour can be overridden by configuring the EndpointResolverFactory.
        /// </summary>
        /// <param name="endpoints">
        /// List of endpoints to use for the initial
        /// connection and recovery.
        /// </param>
        /// <returns>Open connection</returns>
        /// <exception cref="BrokerUnreachableException">
        /// When no hostname was reachable.
        /// </exception>
        public IConnection CreateConnection(IList<AmqpTcpEndpoint> endpoints)
        {
            return CreateConnection(endpoints, ClientProvidedName);
        }

        /// <summary>
        /// Create a connection using a list of endpoints. By default each endpoint will be tried
        /// in a random order until a successful connection is found or the list is exhausted.
        /// The selection behaviour can be overridden by configuring the EndpointResolverFactory.
        /// </summary>
        /// <param name="endpoints">
        /// List of endpoints to use for the initial
        /// connection and recovery.
        /// </param>
        /// <param name="clientProvidedName">
        /// Application-specific connection name, will be displayed in the management UI
        /// if RabbitMQ server supports it. This value doesn't have to be unique and cannot
        /// be used as a connection identifier, e.g. in HTTP API requests.
        /// This value is supposed to be human-readable.
        /// </param>
        /// <returns>Open connection</returns>
        /// <exception cref="BrokerUnreachableException">
        /// When no hostname was reachable.
        /// </exception>
        public IConnection CreateConnection(IList<AmqpTcpEndpoint> endpoints, string clientProvidedName)
        {
            return CreateConnection(EndpointResolverFactory(endpoints), clientProvidedName);
        }

        /// <summary>
        /// Create a connection using an IEndpointResolver.
        /// </summary>
        /// <param name="endpointResolver">
        /// The endpointResolver that returns the endpoints to use for the connection attempt.
        /// </param>
        /// <param name="clientProvidedName">
        /// Application-specific connection name, will be displayed in the management UI
        /// if RabbitMQ server supports it. This value doesn't have to be unique and cannot
        /// be used as a connection identifier, e.g. in HTTP API requests.
        /// This value is supposed to be human-readable.
        /// </param>
        /// <returns>Open connection</returns>
        /// <exception cref="BrokerUnreachableException">
        /// When no hostname was reachable.
        /// </exception>
        public IConnection CreateConnection(IEndpointResolver endpointResolver, string clientProvidedName)
        {
            ConnectionConfig config = CreateConfig(clientProvidedName);
            try
            {
                if (AutomaticRecoveryEnabled)
                {
                    return new AutorecoveringConnection(config, endpointResolver);
                }

                return new Connection(config, endpointResolver.SelectOne(CreateFrameHandler));
            }
            catch (Exception e)
            {
                throw new BrokerUnreachableException(e);
            }
        }

        private ConnectionConfig CreateConfig(string clientProvidedName)
        {
            return new ConnectionConfig(
                VirtualHost,
                UserName,
                Password,
                AuthMechanisms,
                ClientProperties,
                clientProvidedName,
                RequestedChannelMax,
                RequestedFrameMax,
                TopologyRecoveryEnabled,
                NetworkRecoveryInterval,
                RequestedHeartbeat,
                ContinuationTimeout,
                HandshakeContinuationTimeout,
                RequestedConnectionTimeout,
                ConsumerDispatchConcurrency,
                CreateFrameHandler);
        }

        internal IFrameHandler CreateFrameHandler(AmqpTcpEndpoint endpoint)
        {
            IFrameHandler fh = new SocketFrameHandler(endpoint, SocketFactory, RequestedConnectionTimeout, SocketReadTimeout, SocketWriteTimeout);
            return ConfigureFrameHandler(fh);
        }

        private IFrameHandler ConfigureFrameHandler(IFrameHandler fh)
        {
            // TODO: add user-provided configurator, like in the Java client
            fh.ReadTimeout = RequestedHeartbeat;
            fh.WriteTimeout = RequestedHeartbeat;

            if (SocketReadTimeout > RequestedHeartbeat)
            {
                fh.ReadTimeout = SocketReadTimeout;
            }

            if (SocketWriteTimeout > RequestedHeartbeat)
            {
                fh.WriteTimeout = SocketWriteTimeout;
            }

            return fh;
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
                Ssl.Version = AmqpUriSslProtocols;
                Ssl.AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateNameMismatch;
                Port = AmqpTcpEndpoint.DefaultAmqpSslPort;
            }
            else
            {
                throw new ArgumentException($"Wrong scheme in AMQP URI: {uri.Scheme}");
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
                    throw new ArgumentException($"Bad user info in AMQP URI: {userInfo}");
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
                throw new ArgumentException($"Multiple segments in path of AMQP URI: {string.Join(", ", uri.Segments)}");
            }
            if (uri.Segments.Length == 2)
            {
                VirtualHost = UriDecode(uri.Segments[1]);
            }

            _uri = uri;
        }

        ///<summary>
        /// Unescape a string, protecting '+'.
        /// </summary>
        private static string UriDecode(string uri)
        {
            return System.Uri.UnescapeDataString(uri.Replace("+", "%2B"));
        }

        private List<AmqpTcpEndpoint> LocalEndpoints()
        {
            return new List<AmqpTcpEndpoint> { Endpoint };
        }
    }
}
