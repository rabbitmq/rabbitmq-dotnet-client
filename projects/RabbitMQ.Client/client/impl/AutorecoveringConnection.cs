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
using System.Runtime.CompilerServices;

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing.Impl
{
    internal sealed partial class AutorecoveringConnection : IConnection
    {
        private readonly ConnectionFactory _factory;

        private Connection _innerConnection;
        private bool _disposed;

        // list of endpoints provided on initial connection.
        // on re-connection, the next host in the line is chosen using
        // IHostnameSelector
        private IEndpointResolver _endpoints;

        private Connection InnerConnection
        {
            get
            {
                ThrowIfDisposed();
                return _innerConnection;
            }
        }

        public AutorecoveringConnection(ConnectionFactory factory, string clientProvidedName = null)
        {
            _factory = factory;
            ClientProvidedName = clientProvidedName;

            Action<Exception, string> onException = (exception, context) => _innerConnection.OnCallbackException(CallbackExceptionEventArgs.Build(exception, context));
            _recoverySucceededWrapper = new EventingWrapper<EventArgs>("OnConnectionRecovery", onException);
            _connectionRecoveryErrorWrapper = new EventingWrapper<ConnectionRecoveryErrorEventArgs>("OnConnectionRecoveryError", onException);
            _consumerTagChangeAfterRecoveryWrapper = new EventingWrapper<ConsumerTagChangedAfterRecoveryEventArgs>("OnConsumerRecovery", onException);
            _queueNameChangeAfterRecoveryWrapper = new EventingWrapper<QueueNameChangedAfterRecoveryEventArgs>("OnQueueRecovery", onException);
        }

        public event EventHandler<EventArgs> RecoverySucceeded
        {
            add => _recoverySucceededWrapper.AddHandler(value);
            remove => _recoverySucceededWrapper.RemoveHandler(value);
        }
        private EventingWrapper<EventArgs> _recoverySucceededWrapper;

        public event EventHandler<ConnectionRecoveryErrorEventArgs> ConnectionRecoveryError
        {
            add => _connectionRecoveryErrorWrapper.AddHandler(value);
            remove => _connectionRecoveryErrorWrapper.RemoveHandler(value);
        }
        private EventingWrapper<ConnectionRecoveryErrorEventArgs> _connectionRecoveryErrorWrapper;

        public event EventHandler<CallbackExceptionEventArgs> CallbackException
        {
            add => InnerConnection.CallbackException += value;
            remove => InnerConnection.CallbackException -= value;
        }

        public event EventHandler<ConnectionBlockedEventArgs> ConnectionBlocked
        {
            add => InnerConnection.ConnectionBlocked += value;
            remove => InnerConnection.ConnectionBlocked -= value;
        }

        public event EventHandler<ShutdownEventArgs> ConnectionShutdown
        {
            add => InnerConnection.ConnectionShutdown += value;
            remove => InnerConnection.ConnectionShutdown -= value;
        }

        public event EventHandler<EventArgs> ConnectionUnblocked
        {
            add => InnerConnection.ConnectionUnblocked += value;
            remove => InnerConnection.ConnectionUnblocked -= value;
        }

        public event EventHandler<ConsumerTagChangedAfterRecoveryEventArgs> ConsumerTagChangeAfterRecovery
        {
            add => _consumerTagChangeAfterRecoveryWrapper.AddHandler(value);
            remove => _consumerTagChangeAfterRecoveryWrapper.RemoveHandler(value);
        }
        private EventingWrapper<ConsumerTagChangedAfterRecoveryEventArgs> _consumerTagChangeAfterRecoveryWrapper;

        public event EventHandler<QueueNameChangedAfterRecoveryEventArgs> QueueNameChangeAfterRecovery
        {
            add => _queueNameChangeAfterRecoveryWrapper.AddHandler(value);
            remove => _queueNameChangeAfterRecoveryWrapper.RemoveHandler(value);
        }
        private EventingWrapper<QueueNameChangedAfterRecoveryEventArgs> _queueNameChangeAfterRecoveryWrapper;

        public string ClientProvidedName { get; }

        public ushort ChannelMax => InnerConnection.ChannelMax;

        public IDictionary<string, object> ClientProperties => InnerConnection.ClientProperties;

        public ShutdownEventArgs CloseReason => InnerConnection.CloseReason;

        public AmqpTcpEndpoint Endpoint => InnerConnection.Endpoint;

        public uint FrameMax => InnerConnection.FrameMax;

        public TimeSpan Heartbeat => InnerConnection.Heartbeat;

        public bool IsOpen => _innerConnection?.IsOpen ?? false;

        public int LocalPort => InnerConnection.LocalPort;

        public int RemotePort => InnerConnection.RemotePort;

        public IDictionary<string, object> ServerProperties => InnerConnection.ServerProperties;

        public IList<ShutdownReportEntry> ShutdownReport => InnerConnection.ShutdownReport;

        public IProtocol Protocol => Endpoint.Protocol;

        public RecoveryAwareModel CreateNonRecoveringModel()
        {
            ISession session = InnerConnection.CreateSession();
            var result = new RecoveryAwareModel(_factory.DispatchConsumersAsync, _factory.ConsumerDispatchConcurrency, session)
            {
                ContinuationTimeout = _factory.ContinuationTimeout
            };
            result._Private_ChannelOpen();
            return result;
        }

        public override string ToString()
            => $"AutorecoveringConnection({InnerConnection.Id},{Endpoint},{GetHashCode()})";

        internal void Init()
        {
            Init(_factory.EndpointResolverFactory(new List<AmqpTcpEndpoint> { _factory.Endpoint }));
        }

        internal void Init(IEndpointResolver endpoints)
        {
            ThrowIfDisposed();
            _endpoints = endpoints;
            IFrameHandler fh = endpoints.SelectOne(_factory.CreateFrameHandler);
            _innerConnection = new Connection(_factory, fh, ClientProvidedName);
            ConnectionShutdown += HandleConnectionShutdown;
        }

        ///<summary>API-side invocation of updating the secret.</summary>
        public void UpdateSecret(string newSecret, string reason)
        {
            ThrowIfDisposed();
            EnsureIsOpen();
            _innerConnection.UpdateSecret(newSecret, reason);
            _factory.Password = newSecret;
        }

        ///<summary>API-side invocation of connection.close with timeout.</summary>
        public void Close(ushort reasonCode, string reasonText, TimeSpan timeout, bool abort)
        {
            ThrowIfDisposed();
            StopRecoveryLoop();
            if (_innerConnection.IsOpen)
            {
                _innerConnection.Close(reasonCode, reasonText, timeout, abort);
            }
        }

        public IModel CreateModel()
        {
            EnsureIsOpen();
            AutorecoveringModel m = new AutorecoveringModel(this, CreateNonRecoveringModel());
            RecordChannel(m);
            return m;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                this.Abort(TimeSpan.FromSeconds(15));
            }
            catch (Exception)
            {
                // TODO: log
            }
            finally
            {
                _models.Clear();
                _innerConnection = null;
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
