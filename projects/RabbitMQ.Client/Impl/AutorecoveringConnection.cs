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

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing
{
    internal sealed partial class AutorecoveringConnection : IConnection
    {
        private readonly ConnectionConfig _config;
        // list of endpoints provided on initial connection.
        // on re-connection, the next host in the line is chosen using
        // IHostnameSelector
        private readonly IEndpointResolver _endpoints;

        private Connection _innerConnection;
        private bool _disposed;

        private Connection InnerConnection
        {
            get
            {
                ThrowIfDisposed();
                return _innerConnection;
            }
        }

        internal AutorecoveringConnection(ConnectionConfig config, IEndpointResolver endpoints, Connection innerConnection)
        {
            _config = config;
            _endpoints = endpoints;
            _innerConnection = innerConnection;

            ConnectionShutdownAsync += HandleConnectionShutdownAsync;

            _recoverySucceededAsyncWrapper =
                new AsyncEventingWrapper<AsyncEventArgs>("OnConnectionRecovery", onExceptionAsync);

            _connectionRecoveryErrorAsyncWrapper =
                new AsyncEventingWrapper<ConnectionRecoveryErrorEventArgs>("OnConnectionRecoveryError", onExceptionAsync);

            _consumerTagChangeAfterRecoveryAsyncWrapper =
                new AsyncEventingWrapper<ConsumerTagChangedAfterRecoveryEventArgs>("OnConsumerRecovery", onExceptionAsync);

            _queueNameChangedAfterRecoveryAsyncWrapper =
                new AsyncEventingWrapper<QueueNameChangedAfterRecoveryEventArgs>("OnQueueRecovery", onExceptionAsync);

            _recoveringConsumerAsyncWrapper =
                new AsyncEventingWrapper<RecoveringConsumerEventArgs>("OnRecoveringConsumer", onExceptionAsync);

            Task onExceptionAsync(Exception exception, string context, CancellationToken cancellationToken) =>
                _innerConnection.OnCallbackExceptionAsync(CallbackExceptionEventArgs.Build(exception, context, cancellationToken));
        }

        internal static async ValueTask<AutorecoveringConnection> CreateAsync(ConnectionConfig config, IEndpointResolver endpoints,
            CancellationToken cancellationToken)
        {
            IFrameHandler fh = await endpoints.SelectOneAsync(config.FrameHandlerFactoryAsync, cancellationToken)
                .ConfigureAwait(false);
            Connection innerConnection = new(config, fh);
            AutorecoveringConnection connection = new(config, endpoints, innerConnection);
            await innerConnection.OpenAsync(cancellationToken)
                .ConfigureAwait(false);
            return connection;
        }

        public event AsyncEventHandler<AsyncEventArgs> RecoverySucceededAsync
        {
            add => _recoverySucceededAsyncWrapper.AddHandler(value);
            remove => _recoverySucceededAsyncWrapper.RemoveHandler(value);
        }
        private AsyncEventingWrapper<AsyncEventArgs> _recoverySucceededAsyncWrapper;

        public event AsyncEventHandler<ConnectionRecoveryErrorEventArgs> ConnectionRecoveryErrorAsync
        {
            add => _connectionRecoveryErrorAsyncWrapper.AddHandler(value);
            remove => _connectionRecoveryErrorAsyncWrapper.RemoveHandler(value);
        }
        private AsyncEventingWrapper<ConnectionRecoveryErrorEventArgs> _connectionRecoveryErrorAsyncWrapper;

        public event AsyncEventHandler<CallbackExceptionEventArgs> CallbackExceptionAsync
        {
            add => InnerConnection.CallbackExceptionAsync += value;
            remove => InnerConnection.CallbackExceptionAsync -= value;
        }

        public event AsyncEventHandler<ConnectionBlockedEventArgs> ConnectionBlockedAsync
        {
            add => InnerConnection.ConnectionBlockedAsync += value;
            remove => InnerConnection.ConnectionBlockedAsync -= value;
        }

        public event AsyncEventHandler<ShutdownEventArgs> ConnectionShutdownAsync
        {
            add => InnerConnection.ConnectionShutdownAsync += value;
            remove => InnerConnection.ConnectionShutdownAsync -= value;
        }

        public event AsyncEventHandler<AsyncEventArgs> ConnectionUnblockedAsync
        {
            add => InnerConnection.ConnectionUnblockedAsync += value;
            remove => InnerConnection.ConnectionUnblockedAsync -= value;
        }

        public event AsyncEventHandler<ConsumerTagChangedAfterRecoveryEventArgs> ConsumerTagChangeAfterRecoveryAsync
        {
            add => _consumerTagChangeAfterRecoveryAsyncWrapper.AddHandler(value);
            remove => _consumerTagChangeAfterRecoveryAsyncWrapper.RemoveHandler(value);
        }
        private AsyncEventingWrapper<ConsumerTagChangedAfterRecoveryEventArgs> _consumerTagChangeAfterRecoveryAsyncWrapper;

        public event AsyncEventHandler<QueueNameChangedAfterRecoveryEventArgs> QueueNameChangedAfterRecoveryAsync
        {
            add => _queueNameChangedAfterRecoveryAsyncWrapper.AddHandler(value);
            remove => _queueNameChangedAfterRecoveryAsyncWrapper.RemoveHandler(value);
        }
        private AsyncEventingWrapper<QueueNameChangedAfterRecoveryEventArgs> _queueNameChangedAfterRecoveryAsyncWrapper;

        public event AsyncEventHandler<RecoveringConsumerEventArgs> RecoveringConsumerAsync
        {
            add => _recoveringConsumerAsyncWrapper.AddHandler(value);
            remove => _recoveringConsumerAsyncWrapper.RemoveHandler(value);
        }
        private AsyncEventingWrapper<RecoveringConsumerEventArgs> _recoveringConsumerAsyncWrapper;

        public string? ClientProvidedName => _config.ClientProvidedName;

        public ushort ChannelMax => InnerConnection.ChannelMax;

        public IDictionary<string, object?> ClientProperties => InnerConnection.ClientProperties;

        public ShutdownEventArgs? CloseReason => InnerConnection.CloseReason;

        public AmqpTcpEndpoint Endpoint => InnerConnection.Endpoint;

        public uint FrameMax => InnerConnection.FrameMax;

        public TimeSpan Heartbeat => InnerConnection.Heartbeat;

        public bool IsOpen => _innerConnection.IsOpen;

        public int LocalPort => InnerConnection.LocalPort;

        public int RemotePort => InnerConnection.RemotePort;

        public IDictionary<string, object?>? ServerProperties => InnerConnection.ServerProperties;

        public IEnumerable<ShutdownReportEntry> ShutdownReport => InnerConnection.ShutdownReport;

        public IProtocol Protocol => Endpoint.Protocol;

        public async ValueTask<RecoveryAwareChannel> CreateNonRecoveringChannelAsync(
            bool publisherConfirmationsEnabled = false,
            bool publisherConfirmationTrackingEnabled = false,
            ushort? consumerDispatchConcurrency = null,
            CancellationToken cancellationToken = default)
        {
            ISession session = InnerConnection.CreateSession();
            var result = new RecoveryAwareChannel(_config, session, consumerDispatchConcurrency);
            return (RecoveryAwareChannel)await result.OpenAsync(
                publisherConfirmationsEnabled, publisherConfirmationTrackingEnabled, cancellationToken)
                .ConfigureAwait(false);
        }

        public override string ToString()
            => $"AutorecoveringConnection({InnerConnection.Id},{Endpoint},{GetHashCode()})";

        internal ValueTask CloseFrameHandlerAsync()
        {
            return InnerConnection.FrameHandler.CloseAsync(CancellationToken.None);
        }

        ///<summary>API-side invocation of updating the secret.</summary>
        public Task UpdateSecretAsync(string newSecret, string reason,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            EnsureIsOpen();
            return _innerConnection.UpdateSecretAsync(newSecret, reason, cancellationToken);
        }

        ///<summary>Asynchronous API-side invocation of connection.close with timeout.</summary>
        public async Task CloseAsync(ushort reasonCode, string reasonText, TimeSpan timeout, bool abort,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            Task CloseInnerConnectionAsync()
            {
                if (_innerConnection.IsOpen)
                {
                    return _innerConnection.CloseAsync(reasonCode, reasonText, timeout, abort, cancellationToken);
                }
                else
                {
                    return Task.CompletedTask;
                }
            }

            try
            {
                await StopRecoveryLoopAsync(cancellationToken)
                    .ConfigureAwait(false);

                await CloseInnerConnectionAsync()
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                try
                {
                    await CloseInnerConnectionAsync()
                        .ConfigureAwait(false);
                }
                catch (Exception innerConnectionException)
                {
                    throw new AggregateException(ex, innerConnectionException);
                }

                throw;
            }
        }

        public async Task<IChannel> CreateChannelAsync(CreateChannelOptions? options = default,
            CancellationToken cancellationToken = default)
        {
            EnsureIsOpen();

            options ??= CreateChannelOptions.Default;

            ushort cdc = options.ConsumerDispatchConcurrency.GetValueOrDefault(_config.ConsumerDispatchConcurrency);

            RecoveryAwareChannel recoveryAwareChannel = await CreateNonRecoveringChannelAsync(
                    options.PublisherConfirmationsEnabled, options.PublisherConfirmationTrackingEnabled, cdc, cancellationToken)
                .ConfigureAwait(false);

            var autorecoveringChannel = new AutorecoveringChannel(this, recoveryAwareChannel, cdc,
                options.PublisherConfirmationsEnabled, options.PublisherConfirmationTrackingEnabled);
            await RecordChannelAsync(autorecoveringChannel, channelsSemaphoreHeld: false, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return autorecoveringChannel;
        }

        public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                await _innerConnection.DisposeAsync()
                    .ConfigureAwait(false);
            }
            catch (OperationInterruptedException)
            {
                // ignored, see rabbitmq/rabbitmq-dotnet-client#133
            }
            finally
            {
                _channels.Clear();
                _recordedEntitiesSemaphore.Dispose();
                _channelsSemaphore.Dispose();
                _recoveryCancellationTokenSource.Dispose();
                _disposed = true;
            }
        }

        private void EnsureIsOpen()
            => InnerConnection.EnsureIsOpen();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                ThrowDisposed();
            }

            static void ThrowDisposed() => throw new ObjectDisposedException(typeof(AutorecoveringConnection).FullName);
        }
    }
}
