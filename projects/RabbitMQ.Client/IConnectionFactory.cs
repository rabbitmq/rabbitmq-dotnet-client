// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Exceptions;

namespace RabbitMQ.Client
{
    public interface IConnectionFactory
    {
        /// <summary>
        /// Dictionary of client properties to be sent to the server.
        /// </summary>
        IDictionary<string, object?> ClientProperties { get; set; }

        /// <summary>
        /// Password to use when authenticating to the server.
        /// </summary>
        string Password { get; set; }

        /// <summary>
        /// Maximum channel number to ask for.
        /// </summary>
        ushort RequestedChannelMax { get; set; }

        /// <summary>
        /// Frame-max parameter to ask for (in bytes).
        /// </summary>
        uint RequestedFrameMax { get; set; }

        /// <summary>
        /// Heartbeat setting to request.
        /// </summary>
        TimeSpan RequestedHeartbeat { get; set; }

        /// <summary>
        /// Username to use when authenticating to the server.
        /// </summary>
        string UserName { get; set; }

        /// <summary>
        /// Virtual host to access during this connection.
        /// </summary>
        string VirtualHost { get; set; }

        /// <summary>
        /// ICredentialsProvider used to obtain username and password.
        /// </summary>
        public ICredentialsProvider? CredentialsProvider { get; set; }

        /// <summary>
        /// Sets or gets the AMQP Uri to be used for connections.
        /// </summary>
        Uri Uri { get; set; }

        /// <summary>
        /// Default client provided name to be used for connections.
        /// </summary>
        string? ClientProvidedName { get; set; }

        /// <summary>
        /// Given a list of mechanism names supported by the server, select a preferred mechanism,
        /// or null if we have none in common.
        /// </summary>
        IAuthMechanismFactory? AuthMechanismFactory(IEnumerable<string> mechanismNames);

        /// <summary>
        /// Asynchronously create a connection to the specified endpoint.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for this connection</param>
        Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously create a connection to the specified endpoint.
        /// </summary>
        /// <param name="clientProvidedName">
        /// Application-specific connection name, will be displayed in the management UI
        /// if RabbitMQ server supports it. This value doesn't have to be unique and cannot
        /// be used as a connection identifier, e.g. in HTTP API requests.
        /// This value is supposed to be human-readable.
        /// </param>
        /// <param name="cancellationToken">Cancellation token for this connection</param>
        /// <returns>Open connection</returns>
        Task<IConnection> CreateConnectionAsync(string clientProvidedName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously connects to the first reachable hostname from the list.
        /// </summary>
        /// <param name="hostnames">List of host names to use</param>
        /// <param name="cancellationToken">Cancellation token for this connection</param>
        /// <returns>Open connection</returns>
        Task<IConnection> CreateConnectionAsync(IEnumerable<string> hostnames, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously connects to the first reachable hostname from the list.
        /// </summary>
        /// <param name="hostnames">List of host names to use</param>
        /// <param name="clientProvidedName">
        /// Application-specific connection name, will be displayed in the management UI
        /// if RabbitMQ server supports it. This value doesn't have to be unique and cannot
        /// be used as a connection identifier, e.g. in HTTP API requests.
        /// This value is supposed to be human-readable.
        /// </param>
        /// <param name="cancellationToken">Cancellation token for this connection</param>
        /// <returns>Open connection</returns>
        Task<IConnection> CreateConnectionAsync(IEnumerable<string> hostnames, string clientProvidedName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously create a connection using a list of endpoints.
        /// The selection behaviour can be overridden by configuring the EndpointResolverFactory.
        /// </summary>
        /// <param name="endpoints">
        /// List of endpoints to use for the initial
        /// connection and recovery.
        /// </param>
        /// <param name="cancellationToken">Cancellation token for this connection</param>
        /// <returns>Open connection</returns>
        /// <exception cref="BrokerUnreachableException">
        /// When no hostname was reachable.
        /// </exception>
        Task<IConnection> CreateConnectionAsync(IEnumerable<AmqpTcpEndpoint> endpoints, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously create a connection using a list of endpoints.
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
        /// <param name="cancellationToken">Cancellation token for this connection</param>
        /// <returns>Open connection</returns>
        /// <exception cref="BrokerUnreachableException">
        /// When no hostname was reachable.
        /// </exception>
        Task<IConnection> CreateConnectionAsync(IEnumerable<AmqpTcpEndpoint> endpoints, string clientProvidedName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Amount of time protocol handshake operations are allowed to take before
        /// timing out.
        /// </summary>
        TimeSpan HandshakeContinuationTimeout { get; set; }

        /// <summary>
        /// Amount of time protocol  operations (e.g. <code>queue.declare</code>) are allowed to take before
        /// timing out.
        /// </summary>
        /// <remarks>
        /// An operation that reaches this limit completes as <b>cancelled</b>: the awaiter sees an
        /// <see cref="System.OperationCanceledException"/> (in practice a
        /// <see cref="System.Threading.Tasks.TaskCanceledException"/>), not a
        /// <see cref="System.TimeoutException"/>. Note that 6.x threw
        /// <see cref="System.TimeoutException"/> here.
        /// <para>
        /// <b>Nothing on the exception tells a timeout from the caller cancelling.</b> The
        /// <see cref="System.OperationCanceledException.CancellationToken"/> is an internal token in
        /// both cases - the timeout's own token on a timeout, a linked token on a caller cancel - so
        /// comparing it against your own reports a difference either way and tells you nothing.
        /// </para>
        /// <para>
        /// Your own token answers in one direction, which is the best available today. If it is
        /// <b>not</b> cancelled, the operation timed out: the client never cancels a token it does
        /// not own, so nothing else could have produced the cancellation.
        /// </para>
        /// <code>
        /// catch (OperationCanceledException) when (false == myToken.IsCancellationRequested)
        /// {
        ///     // the operation outran ContinuationTimeout
        /// }
        /// </code>
        /// <para>
        /// The converse does not hold, so treat a cancelled token of your own as "cannot tell"
        /// rather than as "not a timeout". Cancelling it does not abort the wait for the reply:
        /// nothing registers your token against the continuation, so once the request is on the wire
        /// the operation runs its full budget and then completes as a timeout with your token
        /// cancelled as well.
        /// </para>
        /// <para>
        /// A close on an open channel or connection is a further exception, because those
        /// deliberately ignore the caller's token so that a close already under way is not
        /// truncated: a cancelled token of yours there does not even mean the request was never
        /// sent. Whether a timeout should be positively identifiable rather than inferred this way
        /// is rabbitmq/rabbitmq-dotnet-client#2019.
        /// </para>
        /// <para>
        /// Some paths do not surface it as cancellation at all. Establishing a connection wraps it in
        /// <see cref="Exceptions.BrokerUnreachableException"/>; an abort swallows it, so
        /// <c>AbortAsync</c> can return successfully after waiting this long; and topology recovery
        /// wraps it in a <c>TopologyRecoveryException</c>, which is logged and fails the recovery
        /// attempt rather than being raised to any event handler: <c>ConnectionRecoveryErrorAsync</c>
        /// covers reconnection, not the topology phase. Note also that waiting for a publisher
        /// confirmation is not bounded by this timeout at all. See
        /// rabbitmq/rabbitmq-dotnet-client#1996.
        /// </para>
        /// </remarks>
        TimeSpan ContinuationTimeout { get; set; }

        /// <summary>
        /// Set to a value greater than one to enable concurrent processing. For a concurrency greater than one <see cref="IAsyncBasicConsumer"/>
        /// will be offloaded to the worker thread pool so it is important to choose the value for the concurrency wisely to avoid thread pool overloading.
        /// <see cref="IAsyncBasicConsumer"/> can handle concurrency much more efficiently due to the non-blocking nature of the consumer.
        /// Defaults to 1.
        /// </summary>
        /// <remarks>For concurrency greater than one this removes the guarantee that consumers handle messages in the order they receive them.
        /// In addition to that consumers need to be thread/concurrency safe.
        /// <para>
        /// A value of 0 is treated as 1. Zero would leave a channel's consumer dispatcher with no
        /// worker at all, so consumers would register successfully and never receive anything.
        /// </para></remarks>
        ushort ConsumerDispatchConcurrency { get; set; }
    }
}
