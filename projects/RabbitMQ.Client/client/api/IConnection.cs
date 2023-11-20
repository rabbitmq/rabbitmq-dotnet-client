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
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Main interface to an AMQP connection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Instances of <see cref="IConnection"/> are used to create fresh
    /// sessions/channels. The <see cref="ConnectionFactory"/> class is used to
    /// construct <see cref="IConnection"/> instances.
    ///  Please see the documentation for ConnectionFactory for an example of usage.
    ///  Alternatively, an API tutorial can be found in the User Guide.
    /// </para>
    /// <para>
    /// Extends the <see cref="IDisposable"/> interface, so that the "using"
    /// statement can be used to scope the lifetime of a connection when
    /// appropriate.
    /// </para>
    /// </remarks>
    public interface IConnection : INetworkConnection, IDisposable
    {
        /// <summary>
        /// The maximum channel number this connection supports (0 if unlimited).
        /// Usable channel numbers range from 1 to this number, inclusive.
        /// </summary>
        ushort ChannelMax { get; }

        /// <summary>
        /// A copy of the client properties that has been sent to the server.
        /// </summary>
        IDictionary<string, object> ClientProperties { get; }

        /// <summary>
        /// Returns null if the connection is still in a state
        /// where it can be used, or the cause of its closure otherwise.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Applications should use the ConnectionShutdown event to
        /// avoid race conditions. The scenario to avoid is checking
        /// <see cref="CloseReason"/>, seeing it is null (meaning the <see cref="IConnection"/>
        /// was available for use at the time of the check), and
        /// interpreting this mistakenly as a guarantee that the
        /// <see cref="IConnection"/> will remain usable for a time. Instead, the
        /// operation of interest should simply be attempted: if the
        /// <see cref="IConnection"/> is not in a usable state, an exception will be
        /// thrown (most likely <see cref="OperationInterruptedException"/>, but may
        /// vary depending on the particular operation being attempted).
        /// </para>
        /// </remarks>
        ShutdownEventArgs CloseReason { get; }

        /// <summary>
        /// Retrieve the endpoint this connection is connected to.
        /// </summary>
        AmqpTcpEndpoint Endpoint { get; }

        /// <summary>
        /// The maximum frame size this connection supports (0 if unlimited).
        /// </summary>
        uint FrameMax { get; }

        /// <summary>
        /// The current heartbeat setting for this connection (System.TimeSpan.Zero for disabled).
        /// </summary>
        TimeSpan Heartbeat { get; }

        /// <summary>
        /// Returns true if the connection is still in a state where it can be used.
        /// Identical to checking if <see cref="CloseReason"/> equal null.
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// The <see cref="IProtocol"/> this connection is using to communicate with its peer.
        /// </summary>
        IProtocol Protocol { get; }

        /// <summary>
        /// A dictionary of the server properties sent by the server while establishing the connection.
        /// This typically includes the product name and version of the server.
        /// </summary>
        IDictionary<string, object> ServerProperties { get; }

        /// <summary>
        /// Returns the list of <see cref="ShutdownReportEntry"/> objects that contain information
        /// about any errors reported while closing the connection in the order they appeared
        /// </summary>
        IEnumerable<ShutdownReportEntry> ShutdownReport { get; }

        /// <summary>
        /// Application-specific connection name, will be displayed in the management UI
        /// if RabbitMQ server supports it. This value doesn't have to be unique and cannot
        /// be used as a connection identifier, e.g. in HTTP API requests.
        /// This value is supposed to be human-readable.
        /// </summary>
        string ClientProvidedName { get; }

        /// <summary>
        /// Signalled when an exception occurs in a callback invoked by the connection.
        /// </summary>
        /// <remarks>
        /// This event is signalled when a ConnectionShutdown handler
        /// throws an exception. If, in future, more events appear on
        /// <see cref="IConnection"/>, then this event will be signalled whenever one
        /// of those event handlers throws an exception, as well.
        /// </remarks>
        event EventHandler<CallbackExceptionEventArgs> CallbackException;

        event EventHandler<ConnectionBlockedEventArgs> ConnectionBlocked;

        /// <summary>
        /// Raised when the connection is destroyed.
        /// </summary>
        /// <remarks>
        /// If the connection is already destroyed at the time an
        /// event handler is added to this event, the event handler
        /// will be fired immediately.
        /// </remarks>
        event EventHandler<ShutdownEventArgs> ConnectionShutdown;

        /// <summary>
        /// Raised when the connection completes recovery.
        /// </summary>
        /// <remarks>
        /// This event will never fire for connections that disable automatic recovery.
        /// </remarks>
        event EventHandler<EventArgs> RecoverySucceeded;

        /// <summary>
        /// Raised when the connection recovery fails, e.g. because reconnection or topology
        /// recovery failed.
        /// </summary>
        /// <remarks>
        /// This event will never fire for connections that disable automatic recovery.
        /// </remarks>
        event EventHandler<ConnectionRecoveryErrorEventArgs> ConnectionRecoveryError;

        /// <summary>
        /// Raised when the server-generated tag of a consumer registered on this connection changes during
        /// connection recovery. This allows applications that need to be aware of server-generated
        /// consumer tag values to keep track of the changes.
        /// </summary>
        /// <remarks>
        /// This event will never fire for connections that disable automatic recovery.
        /// </remarks>
        event EventHandler<ConsumerTagChangedAfterRecoveryEventArgs> ConsumerTagChangeAfterRecovery;

        /// <summary>
        /// Raised when the name of a server-named queue declared on this connection changes during
        /// connection recovery. This allows applications that need to be aware of server-named
        /// queue names to keep track of the changes.
        /// </summary>
        /// <remarks>
        /// This event will never fire for connections that disable automatic recovery.
        /// </remarks>
        event EventHandler<QueueNameChangedAfterRecoveryEventArgs> QueueNameChangedAfterRecovery;

        /// <summary>
        /// Raised when a consumer is about to be recovered. This event raises when topology recovery
        /// is enabled, and just before the consumer is recovered. This allows applications to update
        /// the consumer arguments before the consumer is recovered. It could be particularly useful
        /// when consuming from a stream queue, as it allows to update the consumer offset argument
        /// just before the consumer is recovered.
        /// </summary>
        /// <remarks>
        /// This event will never fire for connections that disable automatic recovery.
        /// </remarks>
        public event EventHandler<RecoveringConsumerEventArgs> RecoveringConsumer;

        event EventHandler<EventArgs> ConnectionUnblocked;

        /// <summary>
        /// This method updates the secret used to authenticate this connection.
        /// It is used when secrets have an expiration date and need to be renewed,
        /// like OAuth 2 tokens.
        /// </summary>
        /// <param name="newSecret">The new secret.</param>
        /// <param name="reason">The reason for the secret update.</param>
        void UpdateSecret(string newSecret, string reason);

        /// <summary>
        /// Close this connection and all its channels
        /// and wait with a timeout for all the in-progress close operations to complete.
        /// </summary>
        /// <param name="reasonCode">The close code (See under "Reply Codes" in the AMQP 0-9-1 specification).</param>
        /// <param name="reasonText">A message indicating the reason for closing the connection.</param>
        /// <param name="timeout">Operation timeout.</param>
        /// <param name="abort">Whether or not this close is an abort (ignores certain exceptions).</param>
        void Close(ushort reasonCode, string reasonText, TimeSpan timeout, bool abort);

        /// <summary>
        /// Asynchronously close this connection and all its channels
        /// and wait with a timeout for all the in-progress close operations to complete.
        /// </summary>
        /// <param name="reasonCode">The close code (See under "Reply Codes" in the AMQP 0-9-1 specification).</param>
        /// <param name="reasonText">A message indicating the reason for closing the connection.</param>
        /// <param name="timeout">Operation timeout.</param>
        /// <param name="abort">Whether or not this close is an abort (ignores certain exceptions).</param>
        ValueTask CloseAsync(ushort reasonCode, string reasonText, TimeSpan timeout, bool abort);

        /// <summary>
        /// Create and return a fresh channel, session, and channel.
        /// </summary>
        IChannel CreateChannel();

        /// <summary>
        /// Asynchronously create and return a fresh channel, session, and channel.
        /// </summary>
        ValueTask<IChannel> CreateChannelAsync();
    }
}
