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

using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client
{
#nullable enable
    /// <summary>
    /// The configuration of a connection.
    /// </summary>
    public sealed class ConnectionConfig
    {
        /// <summary>
        /// Virtual host to access during this connection.
        /// </summary>
        public string VirtualHost { get; }

        /// <summary>
        /// Username to use when authenticating to the server.
        /// </summary>
        public string UserName { get; }

        /// <summary>
        /// Password to use when authenticating to the server.
        /// </summary>
        public string Password { get; }

        /// <summary>
        /// Default CredentialsProvider implementation. If set, this
        /// overrides UserName / Password
        /// </summary>
        public ICredentialsProvider CredentialsProvider;
        public ICredentialsRefresher CredentialsRefresher;

        /// <summary>
        ///  SASL auth mechanisms to use.
        /// </summary>
        public IList<IAuthMechanismFactory> AuthMechanisms { get; }

        /// <summary>
        /// Dictionary of client properties to be sent to the server.
        /// </summary>
        public IDictionary<string, object?> ClientProperties { get; }

        /// <summary>
        /// Default client provided name to be used for connections.
        /// </summary>
        public string? ClientProvidedName { get; }

        /// <summary>
        /// Maximum channel number to ask for.
        /// </summary>
        public ushort MaxChannelCount { get; }

        /// <summary>
        /// Frame-max parameter to ask for (in bytes).
        /// </summary>
        public uint MaxFrameSize { get; }

        /// <summary>
        /// Set to false to make automatic connection recovery not recover topology (exchanges, queues, bindings, etc).
        /// </summary>
        public bool TopologyRecoveryEnabled { get; }

        /// <summary>
        /// Filter to include/exclude entities from topology recovery.
        /// Default filter includes all entities in topology recovery.
        /// </summary>
        public TopologyRecoveryFilter TopologyRecoveryFilter { get; }

        /// <summary>
        /// Custom logic for handling topology recovery exceptions that match the specified filters.
        /// </summary>
        public TopologyRecoveryExceptionHandler TopologyRecoveryExceptionHandler { get; }

        /// <summary>
        /// Amount of time client will wait for before re-trying  to recover connection.
        /// </summary>
        public TimeSpan NetworkRecoveryInterval { get; }

        /// <summary>
        /// Heartbeat timeout to use when negotiating with the server.
        /// </summary>
        public TimeSpan HeartbeatInterval { get; }

        /// <summary>
        /// Amount of time protocol operations (e.g. <code>queue.declare</code>) are allowed to take before timing out.
        /// </summary>
        public TimeSpan ContinuationTimeout { get; }

        /// <summary>
        /// Amount of time protocol handshake operations are allowed to take before timing out.
        /// </summary>

        public TimeSpan HandshakeContinuationTimeout { get; }
        /// <summary>
        /// Timeout setting for connection attempts.
        /// </summary>
        public TimeSpan RequestedConnectionTimeout { get; }

        /// <summary>
        /// Set to true will enable an asynchronous consumer dispatcher which is compatible with <see cref="IAsyncBasicConsumer"/>.
        /// </summary>
        public bool DispatchConsumersAsync { get; }

        /// <summary>
        /// Set to a value greater than one to enable concurrent processing. For a concurrency greater than one <see cref="IBasicConsumer"/>
        /// will be offloaded to the worker thread pool so it is important to choose the value for the concurrency wisely to avoid thread pool overloading.
        /// <see cref="IAsyncBasicConsumer"/> can handle concurrency much more efficiently due to the non-blocking nature of the consumer.
        /// </summary>
        public int DispatchConsumerConcurrency { get; }

        internal Func<AmqpTcpEndpoint, IFrameHandler> FrameHandlerFactory { get; }

        internal ConnectionConfig(string virtualHost, string userName, string password,
            ICredentialsProvider credentialsProvider, ICredentialsRefresher credentialsRefresher,
            IList<IAuthMechanismFactory> authMechanisms,
            IDictionary<string, object?> clientProperties, string? clientProvidedName,
            ushort maxChannelCount, uint maxFrameSize, bool topologyRecoveryEnabled,
            TopologyRecoveryFilter topologyRecoveryFilter, TopologyRecoveryExceptionHandler topologyRecoveryExceptionHandler,
            TimeSpan networkRecoveryInterval, TimeSpan heartbeatInterval, TimeSpan continuationTimeout, TimeSpan handshakeContinuationTimeout, TimeSpan requestedConnectionTimeout,
            bool dispatchConsumersAsync, int dispatchConsumerConcurrency,
            Func<AmqpTcpEndpoint, IFrameHandler> frameHandlerFactory)
        {
            VirtualHost = virtualHost;
            UserName = userName;
            Password = password;
            CredentialsProvider = credentialsProvider ?? new BasicCredentialsProvider(clientProvidedName, userName, password);
            CredentialsRefresher = credentialsRefresher;
            AuthMechanisms = authMechanisms;
            ClientProperties = clientProperties;
            ClientProvidedName = clientProvidedName;
            MaxChannelCount = maxChannelCount;
            MaxFrameSize = maxFrameSize;
            TopologyRecoveryEnabled = topologyRecoveryEnabled;
            TopologyRecoveryFilter = topologyRecoveryFilter;
            TopologyRecoveryExceptionHandler = topologyRecoveryExceptionHandler;
            NetworkRecoveryInterval = networkRecoveryInterval;
            HeartbeatInterval = heartbeatInterval;
            ContinuationTimeout = continuationTimeout;
            HandshakeContinuationTimeout = handshakeContinuationTimeout;
            RequestedConnectionTimeout = requestedConnectionTimeout;
            DispatchConsumersAsync = dispatchConsumersAsync;
            DispatchConsumerConcurrency = dispatchConsumerConcurrency;
            FrameHandlerFactory = frameHandlerFactory;
        }
    }
}
